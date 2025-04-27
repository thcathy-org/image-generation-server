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
    public async Task GenerateImage_ErrorWhenSubmit_ReturnEmpty()
    {
        _mockHttp.When("https://*").Respond(HttpStatusCode.InternalServerError);
        var result = await ReplicateAiService.GenerateImage("any");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GenerateImage_SuccessdedRespond_ReturnBase64Images()
    {
        _mockHttp.When($"{_options.Value.BaseUrl}/models/black-forest-labs/flux-schnell/predictions").Respond("application/json", "{\"id\":\"testId\"}");
        _mockHttp.When($"{_options.Value.BaseUrl}/predictions/testId").Respond("application/json", """
        {
            "id": "testId",
            "status": "succeeded",
            "input":{"image_dimensions":"512x512","negative_prompt":"english characters, alphabet, realistic","num_outputs":4,"prompt":"abc, clip art"},
            "output": ["https://replicate.delivery/123.png", "https://replicate.delivery/456.png"]
        }
        """);
        var generatedPrompt = new List<string> { "A ", "colorful ", "cartoon ", "depicting ", "a ", "test ", "term" };
        _mockHttp.When(HttpMethod.Post, $"{_options.Value.BaseUrl}/models/meta/meta-llama-3-8b-instruct/predictions")
            .Respond("application/json", $"{{ \"output\": {JsonSerializer.Serialize(generatedPrompt)} }}");
        _mockHttp.When("https://replicate.delivery/*").Respond("text/plain","bytes of image");
        var imageAsString = "bytes of image";
        var base64Image = "data:image/png;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes(imageAsString));
        
        var result = await ReplicateAiService.GenerateImage("any");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(base64Image, result[0]);
        Assert.AreEqual(base64Image, result[1]);
    }
    
    [TestMethod]
    public async Task GenerateImagePrompt_SuccessfulResponse_ReturnPrompt()
    {
        var expectedOutput = new List<string> { "A ", "colorful ", "cartoon ", "depicting ", "a ", "test ", "term" };
        var responseJson = $"{{ \"output\": {JsonSerializer.Serialize(expectedOutput)} }}";
        _mockHttp.When(HttpMethod.Post, $"{_options.Value.BaseUrl}/models/meta/meta-llama-3-8b-instruct/predictions")
            .Respond("application/json", responseJson);
        
        // Act
        var result = await ReplicateAiService.GenerateImagePrompt("test term");
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("A colorful cartoon depicting a test term", result);
    }
    
    [TestMethod]
    public async Task GenerateImagePrompt_ErrorResponse_ThrowsException()
    {
        _mockHttp.When(HttpMethod.Post, $"{_options.Value.BaseUrl}/models/meta/meta-llama-3-8b-instruct/predictions")
            .Respond(HttpStatusCode.InternalServerError);
        await Assert.ThrowsExceptionAsync<HttpRequestException>(
            async () => await ReplicateAiService.GenerateImagePrompt("test term"));
    }
}
