using System.Net;
using ImageGenerationServer.Controllers;
using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using static Moq.It;

namespace ImageGenerationServer.UT.Controllers;

[TestClass]
public class VerifyControllerTest : TestBase
{
    [TestMethod]
    public async Task Remove_DeletesPhraseFile()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DeleteObject("ca/cat.json")).ReturnsAsync(true);
        var controller = new VerifyController(new Mock<IVerifyService>().Object, firebase.Object);

        var result = await controller.Remove(new PhraseRequest { Phrase = "cat" });

        Assert.AreEqual(200, Status(result));
        firebase.Verify(m => m.DeleteObject("ca/cat.json"), Times.Once);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenDeleteFails_Returns500()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DeleteObject(IsAny<string>())).ReturnsAsync(false);
        var controller = new VerifyController(new Mock<IVerifyService>().Object, firebase.Object);

        var result = await controller.Remove(new PhraseRequest { Phrase = "cat" });

        Assert.AreEqual((int)HttpStatusCode.InternalServerError, Status(result));
    }

    [TestMethod]
    public async Task Verified_RemoveImageIndex_RewritesAndDoesNotDeletePhraseFile()
    {
        var firebase = new Mock<IFirebaseService>();
        var verify = new Mock<IVerifyService>();
        verify.Setup(m => m.Verify(Is<VerifiedPhrase>(phrase =>
            phrase.Phrase == "cat" && phrase.RemoveImageIndex.SequenceEqual(new[] { 1 }))))
            .ReturnsAsync(true);
        var controller = new VerifyController(verify.Object, firebase.Object);

        var result = await controller.Verified(new List<VerifiedPhrase>
        {
            new()
            {
                Phrase = "cat",
                RemoveImageIndex = new List<int> { 1 }
            }
        });

        Assert.AreEqual(200, Status(result));
        verify.Verify(m => m.Verify(Is<VerifiedPhrase>(phrase =>
            phrase.Phrase == "cat" && phrase.RemoveImageIndex.SequenceEqual(new[] { 1 }))), Times.Once);
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task Verified_WhenRewriteFails_Returns500()
    {
        var verify = new Mock<IVerifyService>();
        verify.Setup(m => m.Verify(IsAny<VerifiedPhrase>())).ReturnsAsync(false);
        var controller = new VerifyController(verify.Object, new Mock<IFirebaseService>().Object);

        var result = await controller.Verified(new List<VerifiedPhrase>
        {
            new()
            {
                Phrase = "cat",
                RemoveImageIndex = new List<int> { 1 }
            }
        });

        Assert.AreEqual((int)HttpStatusCode.InternalServerError, Status(result));
    }

    private static int Status(IActionResult result)
    {
        if (result is StatusCodeResult status)
            return status.StatusCode;
        if (result is ObjectResult obj && obj.StatusCode is int code)
            return code;
        Assert.Fail($"Unexpected result {result.GetType().Name}");
        return 0;
    }
}
