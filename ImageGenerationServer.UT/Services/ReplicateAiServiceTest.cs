using System.Net;
using System.Text;
using System.Text.Json;
using ImageGenerationServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class ReplicateAiServiceTest : TestBase
{
    private IReplicateAiService ReplicateAiService { get; set; }
    private readonly MockHttpMessageHandler _mockHttp = new();
    private IOptions<ReplicateAiServiceOptions> _options = null!;


    [TestInitialize]
    public void Init()
    {
        _options = Options.Create(Config.GetSection(nameof(ReplicateAiServiceOptions)).Get<ReplicateAiServiceOptions>());
        ReplicateAiService = new ReplicateAiService(_options, _mockHttp.ToHttpClient());
    }

    [TestMethod]
    public async Task GenerateImagePrompts_SuccessfulResponse_ReturnsThreePrompts()
    {
        var expectedOutput = new List<string>
        {
            "1. A colorful cartoon apple on a desk. ",
            "2. A red apple in a picnic basket outdoors. ",
            "3. A teacher holding an apple in a classroom."
        };
        var responseJson = $"{{ \"output\": {JsonSerializer.Serialize(expectedOutput)} }}";
        _mockHttp.When(HttpMethod.Post, $"{_options.Value.BaseUrl}/models/{_options.Value.PromptModel}/predictions")
            .Respond("application/json", responseJson);

        var result = await ReplicateAiService.GenerateImagePrompts("apple");

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("A colorful cartoon apple on a desk.", result[0]);
        Assert.AreEqual("A red apple in a picnic basket outdoors.", result[1]);
        Assert.AreEqual("A teacher holding an apple in a classroom.", result[2]);
    }

    [TestMethod]
    public async Task GenerateImagePrompts_InvalidCount_ThrowsInvalidOperationException()
    {
        var expectedOutput = new List<string> { "1. Only one prompt." };
        var responseJson = $"{{ \"output\": {JsonSerializer.Serialize(expectedOutput)} }}";
        _mockHttp.When(HttpMethod.Post, $"{_options.Value.BaseUrl}/models/{_options.Value.PromptModel}/predictions")
            .Respond("application/json", responseJson);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ReplicateAiService.GenerateImagePrompts("apple"));
    }

    [TestMethod]
    public async Task GenerateImageFromPrompt_Success_ReturnsBase64Image()
    {
        _mockHttp.When($"{_options.Value.BaseUrl}/models/{_options.Value.ImageModel}/predictions")
            .Respond("application/json", "{\"id\":\"testId\"}");
        _mockHttp.When($"{_options.Value.BaseUrl}/predictions/testId").Respond("application/json", """
        {
            "id": "testId",
            "status": "succeeded",
            "input":{"prompt":"abc, clip art"},
            "output": ["https://replicate.delivery/123.png"]
        }
        """);
        _mockHttp.When("https://replicate.delivery/*").Respond("text/plain", "bytes of image");

        var imageAsString = "bytes of image";
        var base64Image = "data:image/png;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes(imageAsString));

        var result = await ReplicateAiService.GenerateImageFromPrompt("abc, clip art");

        Assert.AreEqual(base64Image, result);
    }
}
