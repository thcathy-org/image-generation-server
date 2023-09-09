using System.Threading.Channels;
using ImageGenerationServer.Controllers;
using ImageGenerationServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace ImageGenerationServer.UT.Controllers;

[TestClass]
public class ImageControllerTest : TestBase
{
    private readonly Channel<string> _channel;
    private readonly ImageController _controller;

    public ImageControllerTest()
    {
        _channel = Channel.CreateUnbounded<string>();
        _controller = new ImageController(_channel, new DataContext());
    }

    [TestMethod]
    public void Generate_ReturnOk()
    {
        string phrase = "hello world";
        
        var result = _controller.Generate(phrase);
        
        Assert.IsInstanceOfType<OkResult>(result);
    }

    [TestMethod]
    public void Generate_ChannelCompleted_ReturnBadRequest()
    {
        string phrase = "";
        _channel.Writer.Complete();
        
        var result = _controller.Generate(phrase);
        
        Assert.IsInstanceOfType<BadRequestResult>(result);
    }
}