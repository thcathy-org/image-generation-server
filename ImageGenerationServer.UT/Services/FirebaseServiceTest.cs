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
    public async Task CheckFile_CanGetFromFirebase_ReturnTrue()
    {
        _storageClientMock.Setup(m => m.GetObjectAsync(IsAny<string>(), IsAny<string>(), null, default))
            .ReturnsAsync(new Object());
        bool result = await _firebaseService.IsExists("apple");
        Assert.IsTrue(result);
    }
    
    [TestMethod]
    public async Task CheckFile_CannotGetFromFirebase_ReturnFalse()
    {
        _storageClientMock.Setup(m => m.GetObjectAsync(IsAny<string>(), IsAny<string>(), null, default))
            .Throws<Exception>();
        bool result = await _firebaseService.IsExists("apple");
        Assert.IsFalse(result);
    }

}