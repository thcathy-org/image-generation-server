using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace ImageGenerationServer.Services;

public class LocalAiImageServiceOptions
{
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string ImageModel { get; init; } = "flux.2-klein-4b";
    public string Size { get; init; } = "512x512";
    public int RequestTimeoutSeconds { get; init; } = 60;
}

public interface ILocalAiImageService
{
    Task<string> GenerateBase64ImageAsync(string prompt);
}

public class LocalAiImageService : ILocalAiImageService
{
    private readonly LocalAiImageServiceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly bool _configured;

    public LocalAiImageService(IOptions<LocalAiImageServiceOptions> options, HttpClient httpClient)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);

        var baseUrl = _options.BaseUrl?.Trim();
        var apiKey = _options.ApiKey?.Trim();

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
        {
            Log.Warning("LocalAiImageService not configured: BaseUrl or ApiKey missing");
            _configured = false;
            _baseUrl = string.Empty;
            _apiKey = string.Empty;
            return;
        }

        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "https://" + baseUrl;
        }

        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _configured = true;
    }

    public async Task<string> GenerateBase64ImageAsync(string prompt)
    {
        if (!_configured)
        {
            throw new InvalidOperationException("LocalAiImageService is not configured");
        }

        var imageUrl = await RequestImageUrlAsync(prompt);
        return await DownloadBase64ImageAsync(imageUrl);
    }

    private async Task<string> RequestImageUrlAsync(string prompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/images/generations");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var payload = JsonConvert.SerializeObject(new
        {
            model = _options.ImageModel,
            prompt,
            size = _options.Size,
            n = 1
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        Log.Information("LocalAI image generation response: {Json}", json);
        response.EnsureSuccessStatusCode();

        var imageUrl = JObject.Parse(json)["data"]?[0]?["url"]?.Value<string>();
        if (string.IsNullOrEmpty(imageUrl))
        {
            throw new InvalidOperationException("LocalAI image generation response did not include data[0].url");
        }

        return imageUrl;
    }

    private async Task<string> DownloadBase64ImageAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var ext = Path.GetExtension(url).TrimStart('.');
        if (string.IsNullOrEmpty(ext))
        {
            ext = "png";
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:image/{ext};base64,{Convert.ToBase64String(bytes)}";
    }
}
