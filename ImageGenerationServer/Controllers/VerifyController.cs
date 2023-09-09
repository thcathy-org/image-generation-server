using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.DTO;
using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ImageGenerationServer.Controllers;

[ApiController]
[Route("verify")]
public class VerifyController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IVerifyService _verifyService;

    public VerifyController(DataContext context, IVerifyService verifyService)
    {
        _context = context;
        _verifyService = verifyService;
    }

    [HttpGet("total")]
    public int GetTotalPendingVerify() => _context.PendingVerifyPhrases.Count();

    [HttpGet("pending")]
    public List<PendingVerifyPhrase> GetPendingVerifyPhrases()
    {
        Log.Information("get pending verify phrases");
        return _context.PendingVerifyPhrases.ToList();
    }
    
    // for testing only
    [HttpPost("add/{phrase}")]
    public void Add(string phrase)
    {
        Log.Information($"add phrase={phrase} into db");
        _context.Add(new PendingVerifyPhrase(phrase));
        _context.SaveChanges();
    }

    [HttpPost("verified")]
    public void Verified(List<VerifiedPhrase> verifiedPhrases)
    {
        Log.Information($"total verified phrase={verifiedPhrases.Count}");

        var phrases = verifiedPhrases.Select(v => v.Phrase);
        foreach (var verifiedPhrase in verifiedPhrases)
        {
            Verify(verifiedPhrase);
        }
    }

    private void Verify(VerifiedPhrase verifiedPhrase)
    {
        try
        {
            _verifyService.Verify(verifiedPhrase);
            _context.PendingVerifyPhrases
                .Where(p => p.Phrase.Equals(verifiedPhrase.Phrase))
                .ExecuteDelete();
        }
        catch (Exception e)
        {
            Log.Error($"Unexpected exception when verifying phrase = {verifiedPhrase.Phrase}", e);
        }
    }
}
