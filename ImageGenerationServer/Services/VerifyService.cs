using System.Text;
using System.Text.Json;

namespace ImageGenerationServer.Services;

public interface IVerifyService
{
    Task Verify(VerifiedPhrase phrase);
}

public class VerifyService : IVerifyService
{
    private readonly IFirebaseService _firebaseService;

    public VerifyService(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task Verify(VerifiedPhrase phrase)
    {
        var imageFilePath = phrase.Phrase.GetImageFilePath();
        var stream = await _firebaseService.DownloadObjectAsync(imageFilePath.GetImageFilePath());
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(stream);
        
        imagesObject.images = imagesObject.images.Where((_, index) => !phrase.RemoveImageIndex.Contains(index)).ToArray();
        imagesObject.isVerify = true;
        
        var newStream = new MemoryStream(Encoding.ASCII.GetBytes(
            JsonSerializer.Serialize(imagesObject)));
        _firebaseService.UploadObject(imageFilePath, newStream);
    }
}

public class VerifiedPhrase {
    public string Phrase { get; set; }
    public List<int> RemoveImageIndex { get; set; }
}