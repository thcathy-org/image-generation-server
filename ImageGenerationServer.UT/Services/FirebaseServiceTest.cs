using System.Text.Json;
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
        _storageClientMock.Setup(m => m.GetObjectAsync(IsAny<string>(), IsAny<string>(), null, default))
            .ReturnsAsync(new Object());
        var result = await _firebaseService.GetObject("apple");
        Assert.IsNotNull(result);
    }
    
    [TestMethod]
    public async Task GetObject_CannotGetFromFirebase_ReturnNull()
    {
        _storageClientMock.Setup(m => m.GetObjectAsync(IsAny<string>(), IsAny<string>(), null, default))
            .Throws<Exception>();
        Assert.IsNull(await _firebaseService.GetObject("apple"));
    }

}