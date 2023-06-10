using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.DTO;
using Serilog;

namespace ImageGenerationServer.Services;

public partial class ImageGenerationService : BackgroundService
{

    [GeneratedRegex("[^a-zA-Z]")]
    private static partial Regex NonAlphabeticCharactersRegex();
    
    private readonly ChannelReader<string> _channelReader;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFirebaseService _firebaseService;
    private readonly IReplicateAiService _replicateAiService; 
    
    public ImageGenerationService(IServiceProvider serviceProvider,
        Channel<string> channel, IFirebaseService firebaseService, 
        IReplicateAiService replicateAiService)
    {
        _channelReader = channel.Reader;
        _serviceProvider = serviceProvider;
        _firebaseService = firebaseService;
        _replicateAiService = replicateAiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("ImageGenerationService is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            var phrase = await _channelReader.ReadAsync(stoppingToken);
            var filePath = GetImageFilePath(phrase);
            Log.Information("process [{Phrase}], filePath={FilePath}", phrase, filePath);
            
            if (await _firebaseService.IsExists(filePath))
            {
                Log.Information("[{Phrase}] already generated. skipping", phrase);
                continue;
            }

            var base64Images = await _replicateAiService.GenerateImage(phrase);
            if (!base64Images.Any())
            {
                Log.Information("No images generated for [{Phrase}]", phrase);
                continue;
            }
            
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetService<DataContext>();
                context!.PendingVerifyPhrases.Add(new PendingVerifyPhrase(phrase));
            }

            var stream = new MemoryStream(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(base64Images))); 
            _firebaseService.UploadObject(filePath, stream);
            Log.Information("Complete process [{Phrase}]", phrase);
        }

        Log.Information("ImageGenerationService background task is stopping");
    }
    
    public static string GetImageFilePath(string phrase)
    {
        string filename = phrase.ToLower();
        filename = $"{NonAlphabeticCharactersRegex().Replace(filename, "-")}.json";
        var folder = filename.Length < 2 ? filename : filename[..2];
        return $"{folder}/{filename}";
    }

}