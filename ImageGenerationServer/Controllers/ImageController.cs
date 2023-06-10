using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ImageGenerationServer.Controllers;

[ApiController]
[Route("image")]
public class ImageController : ControllerBase
{
    private readonly ChannelWriter<string> _channelWriter;
    public ImageController(Channel<string> channel)
    {
        _channelWriter = channel.Writer;
    }

    [HttpPost("generate/{phrase}")]
    public IActionResult Generate(string phrase)
    {
        Log.Information("request to generate phrase={Phrase}", phrase);
        var sent = _channelWriter.TryWrite(phrase);
        return sent ? Ok() : new BadRequestResult();
    }
}