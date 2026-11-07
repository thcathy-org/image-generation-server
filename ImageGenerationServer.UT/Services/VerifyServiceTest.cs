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

    [TestMethod]
    public async Task RemoveImage_DropsOnlyThatIndexWhenOthersRemain()
    {
        Stream? uploaded = null;
        var stream = new MemoryStream("""{ "images":["a","b","c"], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);
        _firebaseServiceMock.Setup(UploadObjectExpression).Callback((string _, Stream written) => uploaded = written);

        var deleted = await _service.RemoveImage("apple", 1);

        Assert.IsFalse(deleted);
        _firebaseServiceMock.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploaded!)!;
        CollectionAssert.AreEqual(new[] { "a", "c" }, imagesObject.images);
        Assert.IsFalse(imagesObject.isVerify);
        await uploaded.DisposeAsync();
    }

    [TestMethod]
    public async Task RemoveImage_DeletesTheFileWhenItIsTheLastImage()
    {
        string? deletedPath = null;
        var stream = new MemoryStream("""{ "images":["only"], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);
        _firebaseServiceMock.Setup(m => m.DeleteObject(IsAny<string>()))
            .Callback((string path) => deletedPath = path)
            .Returns(Task.CompletedTask);

        var deleted = await _service.RemoveImage("apple", 0);

        Assert.IsTrue(deleted);
        Assert.AreEqual("ap/apple.json", deletedPath);
        _firebaseServiceMock.Verify(UploadObjectExpression, Times.Never);
    }

    [TestMethod]
    public async Task RemoveImage_LeavesOtherImagesWhenTheIndexIsMissing()
    {
        var stream = new MemoryStream("""{ "images":["a","b"], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);

        var deleted = await _service.RemoveImage("apple", 5);

        Assert.IsFalse(deleted);
        _firebaseServiceMock.Verify(UploadObjectExpression, Times.Never);
        _firebaseServiceMock.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
    }
    
    static readonly Expression<Action<IFirebaseService>> UploadObjectExpression = m => m.UploadObject(IsAny<string>(), IsAny<Stream>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Stream?>>> DownloadObjectAsyncExpression = 
        m => m.DownloadObjectAsync(IsAny<string>());
}