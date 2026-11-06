using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Serilog;

namespace ImageGenerationServer.Services;

public class ImageGenerationServiceOptions
{
    public string ImageProvider { get; init; } = "localai";
}

public class ImagesObject
{
    public string[] images { get; set; }
    public bool isVerify { get; set; }
}

public partial class ImageGenerationService : BackgroundService
{
    private readonly ChannelReader<string> _channelReader;
    private readonly IFirebaseService _firebaseService;
    private readonly IReplicateAiService _replicateAiService;
    private readonly ILocalAiImageService _localAiImageService;
    private readonly ImageGenerationServiceOptions _options;

    public ImageGenerationService(
        Channel<string> channel, IFirebaseService firebaseService,
        IReplicateAiService replicateAiService, ILocalAiImageService localAiImageService,
        IOptions<ImageGenerationServiceOptions> options)
    {
        _channelReader = channel.Reader;
        _firebaseService = firebaseService;
        _replicateAiService = replicateAiService;
        _localAiImageService = localAiImageService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("ImageGenerationService is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            var phrase = await _channelReader.ReadAsync(stoppingToken);
            var filePath = phrase.GetImageFilePath();
            Log.Information("process [{Phrase}], filePath={FilePath}", phrase, filePath);
            
            var obj = await _firebaseService.DownloadObjectAsync(filePath);
            if (obj != null)
            {
                Log.Information("[{Phrase}] already generated. skipping", phrase);
                continue;
            }

            var base64Images = await GenerateImagesAsync(phrase);
            if (!base64Images.Any())
            {
                Log.Information("No images generated for [{Phrase}]", phrase);
                continue;
            }

            var stream = new MemoryStream(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(new ImagesObject
            {
                images = base64Images.ToArray(),
                isVerify = false
            }))); 
            _firebaseService.UploadObject(filePath, stream);
            Log.Information("Complete process [{Phrase}]", phrase);
        }

        Log.Information("ImageGenerationService background task is stopping");
    }

    internal async Task<List<string>> GenerateImagesAsync(string phrase)
    {
        List<string> prompts;
        try
        {
            prompts = await _replicateAiService.GenerateImagePrompts(phrase);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate image prompts for [{Phrase}]", phrase);
            return new List<string>();
        }

        var base64Images = new List<string>();
        for (var i = 0; i < prompts.Count; i++)
        {
            try
            {
                var img = _options.ImageProvider == "replicate"
                    ? await _replicateAiService.GenerateImageFromPrompt(prompts[i])
                    : await _localAiImageService.GenerateBase64ImageAsync(prompts[i]);
                base64Images.Add(img);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "image {Index} failed for {Phrase}", i, phrase);
            }
        }

        return base64Images;
    }
}

public static partial class StringExtension
{
    [GeneratedRegex("[^a-zA-Z]")]
    private static partial Regex NonAlphabeticCharactersRegex();

    public static string GetImageFilePath(this string phrase)
    {
        string filename = phrase.ToLower();
        filename = $"{NonAlphabeticCharactersRegex().Replace(filename, "-")}.json";
        var folder = filename.Length < 2 ? filename : filename[..2];
        return $"{folder}/{filename}";
    }
}