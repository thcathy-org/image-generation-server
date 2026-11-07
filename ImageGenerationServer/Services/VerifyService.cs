using System.Text;
using System.Text.Json;
using Serilog;

namespace ImageGenerationServer.Services;

public interface IVerifyService
{
    Task<ImageRemoveOutcome> RemoveImageAsync(string phrase, int imageIndex);
}

public enum ImageRemoveOutcome
{
    Updated,
    Deleted,
    NotFound,
    InvalidIndex,
    InvalidFile,
    Failed
}

public class VerifyService : IVerifyService
{
    private readonly IFirebaseService _firebaseService;

    public VerifyService(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task<ImageRemoveOutcome> RemoveImageAsync(string phrase, int imageIndex)
    {
        if (imageIndex < 0)
            return ImageRemoveOutcome.InvalidIndex;

        var imageFilePath = phrase.GetImageFilePath();
        var stream = await _firebaseService.DownloadObjectAsync(imageFilePath);
        if (stream == null)
        {
            Log.Information("Phrase file not found at {Path}", imageFilePath);
            return ImageRemoveOutcome.NotFound;
        }

        ImagesObject? imagesObject;
        try
        {
            imagesObject = JsonSerializer.Deserialize<ImagesObject>(stream);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Phrase file is not valid image JSON at {Path}", imageFilePath);
            return ImageRemoveOutcome.InvalidFile;
        }

        if (imagesObject?.images == null)
            return ImageRemoveOutcome.InvalidFile;

        if (imageIndex >= imagesObject.images.Length)
        {
            Log.Information("Image index {Index} is outside {Count} images for {Path}", imageIndex, imagesObject.images.Length, imageFilePath);
            return ImageRemoveOutcome.InvalidIndex;
        }

        if (imagesObject.images.Length == 1)
        {
            var deleted = await _firebaseService.DeleteObject(imageFilePath);
            if (!deleted)
            {
                Log.Error("Failed to delete phrase file {Path}", imageFilePath);
                return ImageRemoveOutcome.Failed;
            }

            Log.Information("Deleted phrase file {Path}", imageFilePath);
            return ImageRemoveOutcome.Deleted;
        }

        var uploaded = RewriteWithoutIndexes(imageFilePath, imagesObject, new[] { imageIndex });
        if (!uploaded)
        {
            Log.Error("Failed to rewrite phrase file {Path} without index {Index}", imageFilePath, imageIndex);
            return ImageRemoveOutcome.Failed;
        }

        Log.Information("Removed image {Index} from {Path}", imageIndex, imageFilePath);
        return ImageRemoveOutcome.Updated;
    }

    private bool RewriteWithoutIndexes(string imageFilePath, ImagesObject imagesObject, IReadOnlyCollection<int> removeImageIndex)
    {
        imagesObject.images = imagesObject.images.Where((_, index) => !removeImageIndex.Contains(index)).ToArray();

        var newStream = new MemoryStream(Encoding.ASCII.GetBytes(
            JsonSerializer.Serialize(imagesObject)));
        return _firebaseService.UploadObject(imageFilePath, newStream);
    }
}