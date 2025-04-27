using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace ImageGenerationServer.Services;

public class ReplicateAiServiceOptions
{
    public string? BaseUrl { get; init; }
    public string? Token { get; init; }
}

public interface IReplicateAiService
{
    Task<List<string>> GenerateImage(string keyword);
    Task<string> GenerateImagePrompt(string term);
}

internal class PredictionResponse
{
    public List<string>? Output { get; set; }
}

public class ReplicateAiService : IReplicateAiService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly ReplicateAiServiceOptions _options;
    private readonly HttpClient _httpClient;

    public ReplicateAiService(IOptions<ReplicateAiServiceOptions> options, HttpClient httpClient)
    {
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<List<string>> GenerateImage(string keyword)
    {
        var base64Images = new List<string>();
        try
        {
            for (var i = 0; i < 4; i++)
            {
                var imagePrompt = await GenerateImagePrompt(keyword);
                var response = await SubmitFluxRequest(imagePrompt);
                var urls = await PollResult(response);
                var base64Image = await ToBase64Image(urls.First());
                base64Images.Add(base64Image);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error when generate image");
            return base64Images;
        }

        return base64Images;
    }
    
    private async ValueTask<string> SubmitFluxRequest(string imagePrompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/models/black-forest-labs/flux-schnell/predictions");
        request.Headers.Add("Authorization", $"Token {_options.Token}");
        var payload = JsonConvert.SerializeObject(new
        {
            input = new
            {
                prompt = $"{imagePrompt}\nPrefer cartoon or clipart style.",
                output_format = "png",
                aspect_ratio = "1:1",
                num_outputs = 1,
                megapixels = "0.25",
            }
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        Log.Information("response: {json}", json);
        response.EnsureSuccessStatusCode();
        return JObject.Parse(json).Value<string>("id")!;
    }

    private async ValueTask<string> ToBase64Image(string url)
    {
        Console.WriteLine($"ToBase64Image: {url}");
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var ext = Path.GetExtension(url).Substring(1);
        var base64String = response.Content.ReadAsByteArrayAsync()
            .ContinueWith(t => Convert.ToBase64String(t.Result))
            .Result;

        return $"data:image/{ext};base64,{base64String}";
    }

    private async ValueTask<List<string>> PollResult(string id)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < Timeout)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/predictions/{id}");
            request.Headers.Add("Authorization", $"Token {_options.Token}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            Log.Information("response: {Json}", json);
            var jsonObject = JObject.Parse(json);
            if ("succeeded".Equals(jsonObject.Value<string>("status")))
            {
                Log.Information("Complete generate images for '{value}'", jsonObject.Value<JObject>("input")!.Value<string>("prompt"));
                return jsonObject.Value<JArray>("output")!.ToObject<List<string>>()!;
            }
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        throw new TimeoutException("Timeout when generating images");
    }

    public async Task<string> GenerateImagePrompt(string term)
    {
        Log.Information($"generated image prompt for: '{term}'");
        var prompt = $"You are a text to image prompt writer. Write a prompt for a text to image model (flux.1-schnell) to generate a image which represent the meaning of '{term}', the prompt must not quote the quoted text. The image should not contain any text or English character. Prefer in cartoon or clipart style. Only output the prompt you generated.";

        var input = new
        {
            prompt,
            maxTokens = 512,
            maxNewTokens = 512,
            temperature = 1.5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/models/meta/meta-llama-3-8b-instruct/predictions");
        request.Headers.Add("Prefer", "wait");
        request.Headers.Add("Authorization", $"Token {_options.Token}");
        request.Content = new StringContent(JsonConvert.SerializeObject(new { input }), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var predictionResponse = JsonConvert.DeserializeObject<PredictionResponse>(jsonResponse);
        var imagePrompt = string.Join("", predictionResponse.Output);
        Log.Information($"generated image prompt: {imagePrompt}");
        return imagePrompt;

    }
}