using System.Text;
using System.Text.Json;
using Serilog;

namespace ImageGenerationServer.Services;

public interface IImageRemovalService
{
    Task<ImageRemoveResult> RemoveImageAsync(string phrase, int imageIndex);
}

public enum ImageRemoveResult
{
    Succeeded,
    NotFound,
    Failed
}

public class ImageRemovalService : IImageRemovalService
{
    private readonly IFirebaseService _firebaseService;

    public ImageRemovalService(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task<ImageRemoveResult> RemoveImageAsync(string phrase, int imageIndex)
    {
        if (imageIndex < 0)
            return ImageRemoveResult.NotFound;

        var imageFilePath = phrase.GetImageFilePath();
        var downloaded = await _firebaseService.DownloadObjectAsync(imageFilePath);
        if (downloaded == null)
        {
            Log.Information("Phrase file not found at {Path}", imageFilePath);
            return ImageRemoveResult.NotFound;
        }

        await using (downloaded)
        {
            ImagesObject? imagesObject;
            try
            {
                imagesObject = JsonSerializer.Deserialize<ImagesObject>(downloaded);
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

            var uploaded = RemoveImage(imageFilePath, imagesObject, imageIndex);
            if (!uploaded)
            {
                Log.Error("Failed to remove image {Index} from {Path}", imageIndex, imageFilePath);
                return ImageRemoveResult.Failed;
            }

            Log.Information("Removed image {Index} from {Path}", imageIndex, imageFilePath);
            return ImageRemoveResult.Succeeded;
        }
    }

    private bool RemoveImage(string imageFilePath, ImagesObject imagesObject, int imageIndex)
    {
        imagesObject.images = imagesObject.images.Where((_, index) => index != imageIndex).ToArray();

        using var newStream = new MemoryStream(Encoding.ASCII.GetBytes(
            JsonSerializer.Serialize(imagesObject)));
        return _firebaseService.UploadObject(imageFilePath, newStream);
    }
}
