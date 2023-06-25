using System.Text.RegularExpressions;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Serilog;

namespace ImageGenerationServer.Services;

public class FirebaseServiceOptions
{
    public string BucketName { get; set; } = null!;
    public string FirebaseBaseFolder { get; set; } = null!;
}

public interface IFirebaseService
{
    ValueTask<bool> IsExists(string filename);
    void UploadObject(string filename, Stream source);
}

public class FirebaseService : IFirebaseService
{
    private readonly FirebaseServiceOptions _options;
    public Func<StorageClient> StorageClientBuilder { get; set; } = StorageClient.Create;

    public FirebaseService(IOptions<FirebaseServiceOptions> config)
    {
        _options = config.Value;
    }

    public async ValueTask<bool> IsExists(string filename)
    {
        var storage = StorageClientBuilder.Invoke();
        var objectName = GetObjectName(filename);
        try
        {
            var obj = await storage.GetObjectAsync(_options.BucketName, objectName);
            return obj != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void UploadObject(string filename, Stream source)
    {
        var storage = StorageClientBuilder.Invoke();
        var objectName = GetObjectName(filename);
        try
        {
            storage.UploadObject(_options.BucketName, objectName, null, source);
            Log.Information("{ObjectName} uploaded to {Bucket}", objectName, _options.BucketName);
        } catch (Exception e)
        {
            Log.Error(e, "Error when upload {ObjectName}", objectName);
        }
    }
    
    private string GetObjectName(string filename)
    {
        return $"{_options.FirebaseBaseFolder}/{filename}";
    }
    
}