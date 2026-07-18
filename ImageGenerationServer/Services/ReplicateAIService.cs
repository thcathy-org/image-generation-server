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
    public string? PromptModel { get; init; }
    public int PromptMaxTokens { get; init; } = 512;
    public double PromptTemperature { get; init; } = 0.3;
    public string ImageModel { get; init; } = "black-forest-labs/flux-2-klein-4b";
    public string OutputMegapixels { get; init; } = "0.25";
}

public interface IReplicateAiService
{
    Task<List<string>> GenerateImagePrompts(string term);
    Task<string> GenerateImageFromPrompt(string prompt);
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

    private async ValueTask<string> SubmitFluxRequest(string imagePrompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/models/{_options.ImageModel}/predictions");
        request.Headers.Add("Authorization", $"Token {_options.Token}");
        var payload = JsonConvert.SerializeObject(new
        {
            input = new
            {
                prompt = imagePrompt,
                output_format = "jpg",
                aspect_ratio = "1:1",
                output_megapixels = _options.OutputMegapixels,
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

    public async Task<string> GenerateImageFromPrompt(string prompt)
    {
        var response = await SubmitFluxRequest(prompt);
        var urls = await PollResult(response);
        return await ToBase64Image(urls.First());
    }

    public async Task<List<string>> GenerateImagePrompts(string term)
    {
        Log.Information("Generating image prompts for: '{Term}'", term);
        var prompt =
            $"You write text-to-image prompts for a children's English-learning app. " +
            $"Produce exactly THREE numbered prompts (1., 2., 3.) that visually represent the meaning of '{term}'. " +
            $"Each prompt should be 1-2 sentences. " +
            $"Vary setting, viewpoint, or a safe related concept across the three prompts. " +
            $"Style: flat cartoon / clipart, bright friendly colors, simple clean composition. " +
            $"Structure each prompt as: main subject, then setting, then style. " +
            $"The scenes must be wholesome and classroom-appropriate, depicting only safe, " +
            $"everyday, friendly content suitable for young children. " +
            $"Describe only what SHOULD appear — never use negative phrasing like 'no X' " +
            $"(the image model does not support negatives). " +
            $"Do not include any text, letters, numbers, logos, or watermarks in the described image. " +
            $"If '{term}' cannot be shown in a child-safe way, depict a safe, neutral related concept instead. " +
            $"Return only the three numbered prompts, no quotes or preamble.";

        var input = new
        {
            prompt,
            max_tokens = _options.PromptMaxTokens,
            temperature = _options.PromptTemperature
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/models/{_options.PromptModel}/predictions");
        request.Headers.Add("Prefer", "wait");
        request.Headers.Add("Authorization", $"Token {_options.Token}");
        request.Content = new StringContent(JsonConvert.SerializeObject(new { input }), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var predictionResponse = JsonConvert.DeserializeObject<PredictionResponse>(jsonResponse);
        var rawOutput = string.Join("", predictionResponse?.Output ?? new List<string>());
        Log.Information("Generated image prompts: {Prompts}", rawOutput);
        return ParseNumberedPrompts(rawOutput);
    }

    internal static List<string> ParseNumberedPrompts(string rawOutput)
    {
        var prompts = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            var next = i + 1;
            var pattern = next <= 3
                ? $@"{i}\.\s*(.+?)(?=\s*{next}\.)"
                : $@"{i}\.\s*(.+)$";
            var match = System.Text.RegularExpressions.Regex.Match(
                rawOutput,
                pattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!match.Success)
            {
                throw new InvalidOperationException($"Expected exactly 3 image prompts, got {prompts.Count}");
            }

            prompts.Add(match.Groups[1].Value.Trim());
        }

        return prompts;
    }
}