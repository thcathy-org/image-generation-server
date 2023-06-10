using System.ComponentModel.DataAnnotations;

namespace ImageGenerationServer.DTO;

public class PendingVerifyPhrase
{
    [Key]
    public string Phrase { get; set; }

    public PendingVerifyPhrase(string phrase)
    {
        Phrase = phrase;
    }
}