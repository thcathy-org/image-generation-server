using System.Text;
using System.Text.Json;
using Serilog;

namespace ImageGenerationServer.Services;

public interface IVerifyService
{
    Task<ImageRemoveResult> RemoveImageAsync(string phrase, int imageIndex);
}

public enum ImageRemoveResult
{
    Succeeded,
    NotFound,
    Failed
}

public class VerifyService : IVerifyService
{
    private readonly IFirebaseService _firebaseService;

    public VerifyService(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task<ImageRemoveResult> RemoveImageAsync(string phrase, int imageIndex)
    {
        if (imageIndex < 0)
            return ImageRemoveResult.NotFound;

        var imageFilePath = phrase.GetImageFilePath();
        var stream = await _firebaseService.DownloadObjectAsync(imageFilePath);
        if (stream == null)
        {
            Log.Information("Phrase file not found at {Path}", imageFilePath);
            return ImageRemoveResult.NotFound;
        }

        ImagesObject? imagesObject;
        try
        {
            imagesObject = JsonSerializer.Deserialize<ImagesObject>(stream);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Phrase file is not valid image JSON at {Path}", imageFilePath);
            return ImageRemoveResult.NotFound;
        }

        if (imagesObject?.images == null || imageIndex >= imagesObject.images.Length)
        {
            Log.Information("Image index {Index} was not found at {Path}", imageIndex, imageFilePath);
            return ImageRemoveResult.NotFound;
        }

        if (imagesObject.images.Length == 1)
        {
            var deleted = await _firebaseService.DeleteObject(imageFilePath);
            if (!deleted)
            {
                Log.Error("Failed to delete phrase file {Path}", imageFilePath);
                return ImageRemoveResult.Failed;
            }

            Log.Information("Deleted phrase file {Path}", imageFilePath);
            return ImageRemoveResult.Succeeded;
        }

        var uploaded = RewriteWithoutIndexes(imageFilePath, imagesObject, new[] { imageIndex });
        if (!uploaded)
        {
            Log.Error("Failed to rewrite phrase file {Path} without index {Index}", imageFilePath, imageIndex);
            return ImageRemoveResult.Failed;
        }

        Log.Information("Removed image {Index} from {Path}", imageIndex, imageFilePath);
        return ImageRemoveResult.Succeeded;
    }

    private bool RewriteWithoutIndexes(string imageFilePath, ImagesObject imagesObject, IReadOnlyCollection<int> removeImageIndex)
    {
        imagesObject.images = imagesObject.images.Where((_, index) => !removeImageIndex.Contains(index)).ToArray();

        var newStream = new MemoryStream(Encoding.ASCII.GetBytes(
            JsonSerializer.Serialize(imagesObject)));
        return _firebaseService.UploadObject(imageFilePath, newStream);
    }
}