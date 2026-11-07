using System.Threading.Channels;
using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ImageGenerationServer.Controllers;

[ApiController]
[Route("image")]
public class ImageController : ControllerBase
{
    private readonly ChannelWriter<string> _channelWriter;
    private readonly IVerifyService _verifyService;

    public ImageController(Channel<string> channel, IVerifyService verifyService)
    {
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
    public async Task<IActionResult> Remove(RemoveImageRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Phrase))
            return BadRequest("phrase is required.");
        if (request.ImageIndex is null or < 0)
            return BadRequest("imageIndex is invalid.");

        try
        {
            var outcome = await _verifyService.RemoveImageAsync(request.Phrase, request.ImageIndex.Value);
            return outcome switch
            {
                ImageRemoveOutcome.Updated => Ok(),
                ImageRemoveOutcome.Deleted => Ok(),
                ImageRemoveOutcome.NotFound => NotFound("phrase was not found."),
                ImageRemoveOutcome.InvalidIndex => BadRequest("imageIndex is invalid."),
                ImageRemoveOutcome.InvalidFile => BadRequest("phrase file is not valid image JSON."),
                ImageRemoveOutcome.Failed => StatusCode(StatusCodes.Status500InternalServerError),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to remove image {Index} for {Phrase}", request.ImageIndex, request.Phrase);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

public class RemoveImageRequest
{
    public string? Phrase { get; set; }
    public int? ImageIndex { get; set; }
}
