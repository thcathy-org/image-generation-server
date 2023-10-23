using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.DTO;
using ImageGenerationServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Moq.It;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class ImageGenerationServiceTest : TestBase
{
    private ImageGenerationService _service;
    private Channel<string> _channel = Channel.CreateUnbounded<string>();

    private Mock<IFirebaseService> _firebaseServiceMock = new();
    private Mock<IReplicateAiService> _replicateAiServiceMock = new();
    private Mock<IDataRepository> _repoMock = new();
    

    [TestInitialize]
    public void init()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_firebaseServiceMock.Object);
        services.AddSingleton(_replicateAiServiceMock.Object);
        services.AddSingleton(_channel);
        services.AddSingleton<ImageGenerationService>();
        services.AddScoped<IDataRepository>(r => _repoMock.Object);
        
        var provider = services.BuildServiceProvider();
        _service = provider.GetRequiredService<ImageGenerationService>();
    }
    
    [TestMethod]
    public async Task ExecuteAsync_NormalFlow()
    {
        _replicateAiServiceMock.Setup(m => m.GenerateImage(IsAny<string>()))
            .Returns(Task.FromResult(new List<string> { "base 64 image"}));
        Stream uploadedStream = null;
        _firebaseServiceMock.Setup(UploadObjectExpression).Callback((string parameter, Stream stream) => uploadedStream = stream);
        
        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);
        
        _firebaseServiceMock.Verify(DownloadObjectAsyncExpression, Times.Once);
        _repoMock.Verify(AddOrUpdateExpression, Times.Once);

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStream!)!;
        Assert.AreEqual(1, imagesObject.images.Length);
        Assert.AreEqual(false, imagesObject.isVerify);
        await uploadedStream.DisposeAsync();
    }
    
    [TestMethod]
    public void ToLowerCaseAndReplaceNonAlphabeticCharacters_CanReplace()
    {
        Assert.AreEqual("ap/apple.json", "Apple".GetImageFilePath());
        Assert.AreEqual("th/the-is-not-a-----correct-phrase.json", "The is not a !@# correct phrase".GetImageFilePath());
        Assert.AreEqual("do/download-an-image.json", "download-an-image".GetImageFilePath());
    }
    
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_IsPhraseVerify_DoNotAddToPending(bool isPhraseVerify)
    {
        var stream = new MemoryStream(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(new ImagesObject
        {
            isVerify = isPhraseVerify
        })));
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);
        
        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);
        
        _repoMock.Verify(AddOrUpdateExpression, isPhraseVerify ? Times.Never : Times.Once);
    }

    static readonly Expression<Action<IDataRepository>> AddOrUpdateExpression = m => m.AddOrUpdate(IsAny<PendingVerifyPhrase>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Object?>>> GetObjectAsyncExpression = m => m.GetObject(IsAny<string>());
    static readonly Expression<Action<IFirebaseService>> UploadObjectExpression = m => m.UploadObject(IsAny<string>(), IsAny<Stream>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Stream?>>> DownloadObjectAsyncExpression = m => m.DownloadObjectAsync(IsAny<string>());
}