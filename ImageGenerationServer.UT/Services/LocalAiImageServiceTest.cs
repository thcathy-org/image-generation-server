using System.Net;
using System.Text;
using ImageGenerationServer.Services;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class LocalAiImageServiceTest : TestBase
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private ILocalAiImageService _service = null!;

    private static IOptions<LocalAiImageServiceOptions> CreateOptions(
        string? baseUrl = "https://local-ai.example",
        string? apiKey = "test-api-key")
    {
        return Options.Create(new LocalAiImageServiceOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey
        });
    }

    [TestInitialize]
    public void Init()
    {
        _service = new LocalAiImageService(CreateOptions(), _mockHttp.ToHttpClient());
    }

    [TestMethod]
    public async Task GenerateBase64ImageAsync_Success_ReturnsDataUri()
    {
        const string imageUrl = "https://cdn.example/generated.png";
        _mockHttp.When(HttpMethod.Post, "https://local-ai.example/v1/images/generations")
            .Respond("application/json", $$"""{"data":[{"url":"{{imageUrl}}"}]}""");

        const string imageContent = "bytes of image";
        _mockHttp.When(imageUrl).Respond("image/png", imageContent);

        var result = await _service.GenerateBase64ImageAsync("flat cartoon apple");

        var expected = "data:image/png;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes(imageContent));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public async Task GenerateBase64ImageAsync_PostError_ThrowsHttpRequestException()
    {
        _mockHttp.When(HttpMethod.Post, "https://local-ai.example/v1/images/generations")
            .Respond(HttpStatusCode.InternalServerError);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(
            async () => await _service.GenerateBase64ImageAsync("flat cartoon apple"));
    }

    [TestMethod]
    public async Task GenerateBase64ImageAsync_GetImageError_ThrowsHttpRequestException()
    {
        const string imageUrl = "https://cdn.example/generated.png";
        _mockHttp.When(HttpMethod.Post, "https://local-ai.example/v1/images/generations")
            .Respond("application/json", $$"""{"data":[{"url":"{{imageUrl}}"}]}""");
        _mockHttp.When(imageUrl).Respond(HttpStatusCode.NotFound);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(
            async () => await _service.GenerateBase64ImageAsync("flat cartoon apple"));
    }

    [TestMethod]
    public async Task GenerateBase64ImageAsync_NotConfigured_ThrowsInvalidOperationException()
    {
        var service = new LocalAiImageService(CreateOptions(baseUrl: null), _mockHttp.ToHttpClient());

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await service.GenerateBase64ImageAsync("flat cartoon apple"));
    }

    [TestMethod]
    public async Task GenerateBase64ImageAsync_NormalizesUrlWithoutScheme()
    {
        var service = new LocalAiImageService(CreateOptions(baseUrl: "local-ai.example"), _mockHttp.ToHttpClient());

        const string imageUrl = "https://cdn.example/generated.jpg";
        _mockHttp.When(HttpMethod.Post, "https://local-ai.example/v1/images/generations")
            .Respond("application/json", $$"""{"data":[{"url":"{{imageUrl}}"}]}""");
        _mockHttp.When(imageUrl).Respond("image/jpeg", "jpeg bytes");

        var result = await service.GenerateBase64ImageAsync("flat cartoon apple");

        Assert.IsTrue(result.StartsWith("data:image/jpg;base64,"));
    }
}
