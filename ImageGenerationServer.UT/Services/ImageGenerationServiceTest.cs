using System.Text.Json;
using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static ImageGenerationServer.Services.ImageGenerationService;
using static Moq.It;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class ImageGenerationServiceTest : TestBase
{
    private ImageGenerationService _service;
    private Channel<string> _channel = Channel.CreateUnbounded<string>();

    private Mock<IFirebaseService> _firebaseServiceMock = new();
    private Mock<IReplicateAiService> _replicateAiServiceMock = new(); 
    
    [TestInitialize]
    public void init()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_firebaseServiceMock.Object);
        services.AddSingleton(_replicateAiServiceMock.Object);
        services.AddSingleton(_channel);
        services.AddSingleton<ImageGenerationService>();
        services.AddDbContext<DataContext>();

        var provider = services.BuildServiceProvider();
        _service = provider.GetRequiredService<ImageGenerationService>();
    }
    
    [TestMethod]
    public async Task ExecuteAsync_NormalFlow()
    {
        _replicateAiServiceMock.Setup(m => m.GenerateImage(IsAny<string>()))
            .Returns(Task.FromResult(new List<string> { "base 64 image"}));
        Stream uploadedStream = null;
        _firebaseServiceMock.Setup(x => x.UploadObject(IsAny<string>(), IsAny<Stream>()))
            .Callback((string parameter, Stream stream) => uploadedStream = stream);
        
        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);
        
        _firebaseServiceMock.Verify(m => m.IsExists(IsAny<string>()), Times.Once);
        _firebaseServiceMock.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Once);

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStream!)!;
        Assert.AreEqual(1, imagesObject.images.Length);
        Assert.AreEqual(true, imagesObject.isVerify);
        await uploadedStream.DisposeAsync();
    }
    
    [TestMethod]
    public void ToLowerCaseAndReplaceNonAlphabeticCharacters_CanReplace()
    {
        Assert.AreEqual("ap/apple.json", GetImageFilePath("Apple"));
        Assert.AreEqual("th/the-is-not-a-----correct-phrase.json", GetImageFilePath("The is not a !@# correct phrase"));
        Assert.AreEqual("do/download-an-image.json", GetImageFilePath("download-an-image"));
    }
}