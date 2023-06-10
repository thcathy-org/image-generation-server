using System.Net;
using System.Text;
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
        _mockHttp.When(_options.Value.BaseUrl).Respond("application/json", "{\"id\":\"testId\"}");
        _mockHttp.When($"{_options.Value.BaseUrl}/testId").Respond("application/json", """
        {
            "id": "testId",
            "status": "succeeded",
            "input":{"image_dimensions":"512x512","negative_prompt":"english characters, alphabet, realistic","num_outputs":4,"prompt":"abc, clip art"},
            "output": ["https://replicate.delivery/123.png", "https://replicate.delivery/456.png"]
        }
        """);
        _mockHttp.When("https://replicate.delivery/*").Respond("text/plain","bytes of image");
        var imageAsString = "bytes of image";
        var base64Image = "data:image/png;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes(imageAsString));
        
        var result = await ReplicateAiService.GenerateImage("any");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(base64Image, result[0]);
        Assert.AreEqual(base64Image, result[1]);
    }

}