using System.Linq.Expressions;
using System.Text.Json;
using ImageGenerationServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Moq.It;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class VerifyServiceTest : TestBase
{
    private VerifyService _service;

    private Mock<IFirebaseService> _firebaseServiceMock = new();

    [TestInitialize]
    public void init()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_firebaseServiceMock.Object);
        services.AddSingleton<VerifyService>();
        
        var provider = services.BuildServiceProvider();
        _service = provider.GetRequiredService<VerifyService>();
    }
    
    [TestMethod]
    public async Task ExecuteAsync_NormalFlow()
    {
        Stream? uploadedStreamCaptured = null;
        string? firebaseFilenameCaptured = null;
        var stream = new MemoryStream("""{ "images":[], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression)
            .Callback((string filename) => firebaseFilenameCaptured = filename).ReturnsAsync(stream);
        _firebaseServiceMock.Setup(UploadObjectExpression).Callback((string _, Stream stream) => uploadedStreamCaptured = stream);

        await _service.Verify(new VerifiedPhrase
        {
            Phrase = "test phrase", RemoveImageIndex = new List<int>()
        });
        
        _firebaseServiceMock.Verify(DownloadObjectAsyncExpression, Times.Once);

        Assert.AreEqual("te/test-phrase.json", firebaseFilenameCaptured!);
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStreamCaptured!)!;
        Assert.AreEqual(true, imagesObject.isVerify);
        await uploadedStreamCaptured.DisposeAsync();
    }
    
    static readonly Expression<Action<IFirebaseService>> UploadObjectExpression = m => m.UploadObject(IsAny<string>(), IsAny<Stream>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Stream?>>> DownloadObjectAsyncExpression = 
        m => m.DownloadObjectAsync(IsAny<string>());
}