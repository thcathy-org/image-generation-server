using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ImageGenerationServer.Controllers;
using ImageGenerationServer.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using static Moq.It;

namespace ImageGenerationServer.UT.Controllers;

[TestClass]
public class ImageRemoveControllerTest : TestBase
{
    [TestMethod]
    public async Task Remove_WhenOtherImagesRemain_RewritesFileWithoutThatIndex()
    {
        var firebase = new Mock<IFirebaseService>();
        Stream? uploaded = null;
        string? uploadedPath = null;
        firebase.Setup(m => m.DownloadObjectAsync("th/the-cat.json"))
            .ReturnsAsync(JsonStream("""{ "images":["keep-0","drop-1","keep-2"], "isVerify":false }"""));
        firebase.Setup(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()))
            .Callback((string path, Stream body) =>
            {
                uploadedPath = path;
                uploaded = body;
            })
            .Returns(true);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "The cat",
            ImageIndex = 1
        });

        Assert.AreEqual(200, Status(result));
        Assert.AreEqual("th/the-cat.json", uploadedPath);
        uploaded!.Position = 0;
        var json = await new StreamReader(uploaded).ReadToEndAsync();
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(json)!;
        CollectionAssert.AreEqual(new[] { "keep-0", "keep-2" }, imagesObject.images);
        Assert.IsFalse(json.Contains("isVerify"));
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        await uploaded!.DisposeAsync();
    }

    [TestMethod]
    public async Task Remove_WhenOneImageWouldRemain_RewritesInsteadOfDeleting()
    {
        var firebase = new Mock<IFirebaseService>();
        Stream? uploaded = null;
        firebase.Setup(m => m.DownloadObjectAsync("ca/cat.json"))
            .ReturnsAsync(JsonStream("""{ "images":["drop","stay"], "isVerify":false }"""));
        firebase.Setup(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()))
            .Callback((string _, Stream body) => uploaded = body)
            .Returns(true);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual(200, Status(result));
        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploaded!)!;
        CollectionAssert.AreEqual(new[] { "stay" }, imagesObject.images);
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        await uploaded!.DisposeAsync();
    }

    [TestMethod]
    public async Task Remove_WhenIndexIsTheOnlyImage_DeletesPhraseFile()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync("ca/cat.json"))
            .ReturnsAsync(JsonStream("""{ "images":["only"], "isVerify":false }"""));
        firebase.Setup(m => m.DeleteObject("ca/cat.json")).ReturnsAsync(true);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual(200, Status(result));
        firebase.Verify(m => m.DeleteObject("ca/cat.json"), Times.Once);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenDeleteFails_Returns500()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync(IsAny<string>()))
            .ReturnsAsync(JsonStream("""{ "images":["only"], "isVerify":false }"""));
        firebase.Setup(m => m.DeleteObject(IsAny<string>())).ReturnsAsync(false);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual((int)HttpStatusCode.InternalServerError, Status(result));
    }

    [TestMethod]
    public async Task Remove_WhenRewriteFails_Returns500()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync(IsAny<string>()))
            .ReturnsAsync(JsonStream("""{ "images":["a","b"], "isVerify":false }"""));
        firebase.Setup(m => m.UploadObject(IsAny<string>(), IsAny<Stream>())).Returns(false);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual((int)HttpStatusCode.InternalServerError, Status(result));
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenPhraseFileIsMissing_Returns404()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync("ca/cat.json")).ReturnsAsync((Stream?)null);

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual((int)HttpStatusCode.NotFound, Status(result));
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenIndexIsPastTheOnlyImage_DoesNotDelete()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync(IsAny<string>()))
            .ReturnsAsync(JsonStream("""{ "images":["only"], "isVerify":false }"""));

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 1
        });

        Assert.AreEqual((int)HttpStatusCode.BadRequest, Status(result));
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
    }

    [DataTestMethod]
    [DataRow(null, 0)]
    [DataRow("", 0)]
    [DataRow("   ", 0)]
    public async Task Remove_WhenPhraseIsBlank_Returns400(string? phrase, int imageIndex)
    {
        var firebase = new Mock<IFirebaseService>();

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = phrase,
            ImageIndex = imageIndex
        });

        Assert.AreEqual((int)HttpStatusCode.BadRequest, Status(result));
        firebase.Verify(m => m.DownloadObjectAsync(IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenImageIndexIsMissingOrNegative_Returns400()
    {
        var firebase = new Mock<IFirebaseService>();
        var controller = Controller(firebase);

        var missing = await controller.Remove(new RemoveImageRequest { Phrase = "cat" });
        var negative = await controller.Remove(new RemoveImageRequest { Phrase = "cat", ImageIndex = -1 });

        Assert.AreEqual((int)HttpStatusCode.BadRequest, Status(missing));
        Assert.AreEqual((int)HttpStatusCode.BadRequest, Status(negative));
        firebase.Verify(m => m.DownloadObjectAsync(IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task Remove_WhenPhraseFileIsNotJson_Returns400()
    {
        var firebase = new Mock<IFirebaseService>();
        firebase.Setup(m => m.DownloadObjectAsync(IsAny<string>()))
            .ReturnsAsync(new MemoryStream("{"u8.ToArray()));

        var result = await Controller(firebase).Remove(new RemoveImageRequest
        {
            Phrase = "cat",
            ImageIndex = 0
        });

        Assert.AreEqual((int)HttpStatusCode.BadRequest, Status(result));
        firebase.Verify(m => m.DeleteObject(IsAny<string>()), Times.Never);
        firebase.Verify(m => m.UploadObject(IsAny<string>(), IsAny<Stream>()), Times.Never);
    }

    private static ImageController Controller(Mock<IFirebaseService> firebase)
    {
        return new ImageController(
            Channel.CreateUnbounded<string>(),
            new VerifyService(firebase.Object));
    }

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));

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
