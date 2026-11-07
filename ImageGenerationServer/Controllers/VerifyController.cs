using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ImageGenerationServer.Controllers;

[ApiController]
[Route("verify")]
public class VerifyController : ControllerBase
{
    private readonly IVerifyService _verifyService;
    private readonly IFirebaseService _firebaseService;

    public VerifyController(IVerifyService verifyService, IFirebaseService firebaseService)
    {
        _verifyService = verifyService;
        _firebaseService = firebaseService;
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove(PhraseRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Phrase))
            return BadRequest("phrase is required.");

        var deleted = await _firebaseService.DeleteObject(request.Phrase.GetImageFilePath());
        if (!deleted)
        {
            Log.Error("Failed to delete phrase file for {Phrase}", request.Phrase);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok();
    }

    [HttpPost("verified")]
    public async Task<IActionResult> Verified(List<VerifiedPhrase>? verifiedPhrases)
    {
        if (verifiedPhrases == null)
            return BadRequest("phrase list is required.");

        foreach (var verifiedPhrase in verifiedPhrases)
        {
            if (string.IsNullOrWhiteSpace(verifiedPhrase.Phrase))
                return BadRequest("phrase is required.");

            var uploaded = await _verifyService.Verify(verifiedPhrase);
            if (!uploaded)
            {
                Log.Error("Failed to update phrase file for {Phrase}", verifiedPhrase.Phrase);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        return Ok();
    }
}

public class PhraseRequest
{
    public string? Phrase { get; set; }
}
