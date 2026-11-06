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
    public async Task Verify_RemoveImageIndex_RemovesOnlyThatIndex()
    {
        Stream? uploadedStreamCaptured = null;
        string? uploadedPath = null;
        var stream = new MemoryStream("""{ "images":["keep-0","drop-1","keep-2"], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);
        _firebaseServiceMock.Setup(UploadObjectExpression)
            .Callback((string path, Stream uploaded) =>
            {
                uploadedPath = path;
                uploadedStreamCaptured = uploaded;
            });

        await _service.Verify(new VerifiedPhrase
        {
            Phrase = "cat",
            RemoveImageIndex = new List<int> { 1 }
        });

        Assert.AreEqual("ca/cat.json", uploadedPath);
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStreamCaptured!)!;
        CollectionAssert.AreEqual(new[] { "keep-0", "keep-2" }, imagesObject.images);
        _firebaseServiceMock.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        await uploadedStreamCaptured!.DisposeAsync();
    }

    [TestMethod]
    public async Task Verify_RemoveFirstIndex_LeavesTheOtherImage()
    {
        Stream? uploadedStreamCaptured = null;
        var stream = new MemoryStream("""{ "images":["drop","stay"], "isVerify":false }"""u8.ToArray());
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);
        _firebaseServiceMock.Setup(UploadObjectExpression)
            .Callback((string _, Stream uploaded) => uploadedStreamCaptured = uploaded);

        await _service.Verify(new VerifiedPhrase
        {
            Phrase = "dog",
            RemoveImageIndex = new List<int> { 0 }
        });

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStreamCaptured!)!;
        CollectionAssert.AreEqual(new[] { "stay" }, imagesObject.images);
        _firebaseServiceMock.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        await uploadedStreamCaptured!.DisposeAsync();
    }
    
    static readonly Expression<Action<IFirebaseService>> UploadObjectExpression = m => m.UploadObject(IsAny<string>(), IsAny<Stream>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Stream?>>> DownloadObjectAsyncExpression = 
        m => m.DownloadObjectAsync(IsAny<string>());
}