using System.Linq.Expressions;
using Google.Apis.Download;
using Google.Cloud.Storage.V1;
using ImageGenerationServer.Services;
using Microsoft.Extensions.Options;
using Moq;
using static Moq.It;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class FirebaseServiceTest : TestBase
{
    private FirebaseService _firebaseService;
    private Mock<StorageClient> _storageClientMock;

    [TestInitialize]
    public void init()
    {
        _firebaseService = new FirebaseService(Options.Create(new FirebaseServiceOptions
        {
            BucketName = "testing",
            FirebaseBaseFolder = "testing"
        }));
        _storageClientMock = new Mock<StorageClient>();
        _firebaseService.StorageClientBuilder = () => _storageClientMock.Object;
    }

    [TestMethod]
    public async Task GetObject_CanGetFromFirebase()
    {
        _storageClientMock.Setup(GetObjectAsyncExpression)
            .ReturnsAsync(new Object());
        var result = await _firebaseService.GetObject("apple");
        Assert.IsNotNull(result);
    }
    
    [TestMethod]
    public async Task GetObject_CannotGetFromFirebase_ReturnNull()
    {
        _storageClientMock.Setup(GetObjectAsyncExpression)
            .Throws<Exception>();
        Assert.IsNull(await _firebaseService.GetObject("apple"));
    }
    
    [TestMethod]
    public async Task DownloadObjectAsync_WillGetObjectAndDownload()
    {
        var result = await _firebaseService.DownloadObjectAsync("apple");
        Assert.IsNotNull(result);
        _storageClientMock.Verify(GetObjectAsyncExpression, Times.Once);
        _storageClientMock.Verify(DownloadObjectAsyncExpression, Times.Once);
    }
    
    [TestMethod]
    public async Task DownloadObjectAsync_NoObject_ReturnNull()
    {
        _storageClientMock.Setup(GetObjectAsyncExpression).Throws<Exception>();
        var result = await _firebaseService.DownloadObjectAsync("apple");
        Assert.IsNull(result);
        _storageClientMock.Verify(GetObjectAsyncExpression, Times.Once);
        _storageClientMock.Verify(DownloadObjectAsyncExpression, Times.Never);
    }
    
    static readonly Expression<Func<StorageClient, Task<Object>>> GetObjectAsyncExpression = m => 
        m.GetObjectAsync(IsAny<string>(), IsAny<string>(), null, default);
    static readonly Expression<Func<StorageClient, Task<Object>>> DownloadObjectAsyncExpression = x =>
        x.DownloadObjectAsync(It.IsAny<Object>(), It.IsAny<Stream>(), It.IsAny<DownloadObjectOptions>(),
            It.IsAny<CancellationToken>(), It.IsAny<IProgress<IDownloadProgress>>());
}