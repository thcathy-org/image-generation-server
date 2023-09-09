using System.Text.RegularExpressions;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Serilog;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace ImageGenerationServer.Services;

public class FirebaseServiceOptions
{
    public string BucketName { get; set; } = null!;
    public string FirebaseBaseFolder { get; set; } = null!;
}

public interface IFirebaseService
{
    ValueTask<Object?> GetObject(string filename);
    void UploadObject(string filename, Stream source);
    ValueTask<Stream> DownloadObjectAsync(Object obj);
}

public class FirebaseService : IFirebaseService
{
    private readonly FirebaseServiceOptions _options;
    public Func<StorageClient> StorageClientBuilder { get; set; } = StorageClient.Create;

    public FirebaseService(IOptions<FirebaseServiceOptions> config)
    {
        _options = config.Value;
    }

    public async ValueTask<Object?> GetObject(string filename)
    {
        var storage = StorageClientBuilder.Invoke();
        var objectName = GetObjectName(filename);
        try
        {
            return await storage.GetObjectAsync(_options.BucketName, objectName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async ValueTask<Stream> DownloadObjectAsync(Object obj)
    {
        var storage = StorageClientBuilder.Invoke();
        var stream = new MemoryStream();
        await storage.DownloadObjectAsync(obj, stream);
        stream.Position = 0;
        return stream;
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