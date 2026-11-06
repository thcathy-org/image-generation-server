using ImageGenerationServer.Controllers;
using ImageGenerationServer.DB;
using ImageGenerationServer.DTO;
using ImageGenerationServer.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using static Moq.It;

namespace ImageGenerationServer.UT.Controllers;

[TestClass]
public class VerifyControllerTest : TestBase
{
    [TestMethod]
    public void Remove_DeletesPhraseFileWhenPhraseIsNotPending()
    {
        using var context = CreateContext();
        var firebase = new Mock<IFirebaseService>();
        var verify = new Mock<IVerifyService>();
        var controller = new VerifyController(context, verify.Object, firebase.Object);

        controller.Remove(new PendingVerifyPhrase("cat"));

        firebase.Verify(m => m.DeleteObject("ca/cat.json"), Times.Once);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
        verify.Verify(m => m.Verify(IsAny<VerifiedPhrase>()), Times.Never);
        Assert.AreEqual(0, context.PendingVerifyPhrases.Count());
    }

    [TestMethod]
    public void Verified_RemoveImageIndex_ForwardsIndexAndDoesNotDeletePhraseFile()
    {
        using var context = CreateContext();
        var firebase = new Mock<IFirebaseService>();
        var verify = new Mock<IVerifyService>();
        var controller = new VerifyController(context, verify.Object, firebase.Object);

        controller.Verified(new List<VerifiedPhrase>
        {
            new()
            {
                Phrase = "cat",
                RemoveImageIndex = new List<int> { 1 }
            }
        });

        verify.Verify(m => m.Verify(Is<VerifiedPhrase>(phrase =>
            phrase.Phrase == "cat" && phrase.RemoveImageIndex.SequenceEqual(new[] { 1 }))), Times.Once);
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        Assert.AreEqual(0, context.PendingVerifyPhrases.Count());
    }

    private static DataContext CreateContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"verify-controller-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalDatabase"] = $"Data Source={path}"
            })
            .Build();
        var context = new DataContext(configuration);
        context.Database.EnsureCreated();
        return context;
    }
}
