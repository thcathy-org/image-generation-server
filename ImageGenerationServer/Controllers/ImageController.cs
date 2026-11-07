using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ImageGenerationServer.Controllers;

[ApiController]
[Route("image")]
public class ImageController : ControllerBase
{
    private readonly DataContext _context;
    private readonly ChannelWriter<string> _channelWriter;
    private readonly IVerifyService _verifyService;
    
    public ImageController(Channel<string> channel, DataContext context, IVerifyService verifyService)
    {
        _context = context;
        _channelWriter = channel.Writer;
        _verifyService = verifyService;
    }

    [HttpPost("generate/{phrase}")]
    public IActionResult Generate(string phrase)
    {
        Log.Information("request to generate phrase={Phrase}", phrase);
        var sent = _channelWriter.TryWrite(phrase);
        return sent ? Ok() : new BadRequestResult();
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove(RemoveImageRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Phrase) || request.ImageIndex < 0)
        {
            return BadRequest();
        }

        Log.Information("remove image phrase={Phrase} index={Index}", request.Phrase, request.ImageIndex);
        var deleted = await _verifyService.RemoveImage(request.Phrase, request.ImageIndex);
        if (deleted)
        {
            _context.PendingVerifyPhrases
                .Where(p => p.Phrase.Equals(request.Phrase))
                .ExecuteDelete();
        }
        return Ok();
    }
}

public class RemoveImageRequest
{
    public string Phrase { get; set; } = "";
    public int ImageIndex { get; set; }
}