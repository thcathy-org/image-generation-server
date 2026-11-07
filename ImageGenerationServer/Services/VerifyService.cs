using System.Text;
using System.Text.Json;

namespace ImageGenerationServer.Services;

public interface IVerifyService
{
    Task Verify(VerifiedPhrase phrase);
    Task<bool> RemoveImage(string phrase, int imageIndex);
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
        var stream = await _firebaseService.DownloadObjectAsync(imageFilePath);
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(stream);
        
        imagesObject.images = imagesObject.images.Where((_, index) => !phrase.RemoveImageIndex.Contains(index)).ToArray();
        imagesObject.isVerify = true;
        
        var newStream = new MemoryStream(Encoding.ASCII.GetBytes(
            JsonSerializer.Serialize(imagesObject)));
        _firebaseService.UploadObject(imageFilePath, newStream);
    }

    public async Task<bool> RemoveImage(string phrase, int imageIndex)
    {
        var imageFilePath = phrase.GetImageFilePath();
        var stream = await _firebaseService.DownloadObjectAsync(imageFilePath);
        if (stream == null) return true;

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(stream);
        var images = imagesObject?.images ?? Array.Empty<string>();
        if (images.Length == 0)
        {
            await _firebaseService.DeleteObject(imageFilePath);
            return true;
        }
        if (imageIndex < 0 || imageIndex >= images.Length)
        {
            return false;
        }

        var kept = images.Where((_, index) => index != imageIndex).ToArray();
        if (kept.Length == 0)
        {
            await _firebaseService.DeleteObject(imageFilePath);
            return true;
        }

        imagesObject!.images = kept;
        var newStream = new MemoryStream(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(imagesObject)));
        _firebaseService.UploadObject(imageFilePath, newStream);
        return false;
    }
}

public class VerifiedPhrase {
    public string Phrase { get; set; }
    public List<int> RemoveImageIndex { get; set; }
}