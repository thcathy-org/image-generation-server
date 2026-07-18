using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.DTO;
using ImageGenerationServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using static Moq.It;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace ImageGenerationServer.UT.Services;

[TestClass]
public class ImageGenerationServiceTest : TestBase
{
    private ImageGenerationService _service = null!;
    private Channel<string> _channel = Channel.CreateUnbounded<string>();

    private Mock<IFirebaseService> _firebaseServiceMock = new();
    private Mock<IReplicateAiService> _replicateAiServiceMock = new();
    private Mock<ILocalAiImageService> _localAiImageServiceMock = new();
    private Mock<IDataRepository> _repoMock = new();

    private ImageGenerationService CreateService(string imageProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_firebaseServiceMock.Object);
        services.AddSingleton(_replicateAiServiceMock.Object);
        services.AddSingleton(_localAiImageServiceMock.Object);
        services.AddSingleton(Options.Create(new ImageGenerationServiceOptions { ImageProvider = imageProvider }));
        services.AddSingleton(_channel);
        services.AddSingleton<ImageGenerationService>();
        services.AddScoped<IDataRepository>(r => _repoMock.Object);

        return services.BuildServiceProvider().GetRequiredService<ImageGenerationService>();
    }

    [TestInitialize]
    public void init()
    {
        _service = CreateService("localai");
    }

    [TestMethod]
    public async Task ExecuteAsync_LocalAiProvider_UsesLocalAiForImages()
    {
        _service = CreateService("localai");
        _replicateAiServiceMock.Setup(m => m.GenerateImagePrompts(IsAny<string>()))
            .ReturnsAsync(new List<string> { "prompt one", "prompt two", "prompt three" });
        _localAiImageServiceMock.Setup(m => m.GenerateBase64ImageAsync(IsAny<string>()))
            .ReturnsAsync((string prompt) => $"localai:{prompt}");
        Stream? uploadedStream = null;
        _firebaseServiceMock.Setup(UploadObjectExpression).Callback((string parameter, Stream stream) => uploadedStream = stream);

        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);

        _localAiImageServiceMock.Verify(m => m.GenerateBase64ImageAsync(IsAny<string>()), Times.Exactly(3));
        _replicateAiServiceMock.Verify(m => m.GenerateImageFromPrompt(IsAny<string>()), Times.Never);

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStream!)!;
        Assert.AreEqual(3, imagesObject.images.Length);
        Assert.AreEqual("localai:prompt one", imagesObject.images[0]);
        Assert.AreEqual(false, imagesObject.isVerify);
        await uploadedStream!.DisposeAsync();
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplicateProvider_UsesReplicateForImages()
    {
        _service = CreateService("replicate");
        _replicateAiServiceMock.Setup(m => m.GenerateImagePrompts(IsAny<string>()))
            .ReturnsAsync(new List<string> { "prompt one", "prompt two", "prompt three" });
        _replicateAiServiceMock.Setup(m => m.GenerateImageFromPrompt(IsAny<string>()))
            .ReturnsAsync((string prompt) => $"replicate:{prompt}");
        Stream? uploadedStream = null;
        _firebaseServiceMock.Setup(UploadObjectExpression).Callback((string parameter, Stream stream) => uploadedStream = stream);

        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);

        _replicateAiServiceMock.Verify(m => m.GenerateImageFromPrompt(IsAny<string>()), Times.Exactly(3));
        _localAiImageServiceMock.Verify(m => m.GenerateBase64ImageAsync(IsAny<string>()), Times.Never);

        var imagesObject = JsonSerializer.Deserialize<ImagesObject>(uploadedStream!)!;
        Assert.AreEqual(3, imagesObject.images.Length);
        Assert.AreEqual("replicate:prompt one", imagesObject.images[0]);
        await uploadedStream!.DisposeAsync();
    }

    [TestMethod]
    public async Task GenerateImagesAsync_PromptGenerationFailure_ReturnsEmptyList()
    {
        _replicateAiServiceMock.Setup(m => m.GenerateImagePrompts("apple"))
            .ThrowsAsync(new InvalidOperationException("Expected exactly 3 image prompts"));

        var result = await _service.GenerateImagesAsync("apple");

        Assert.AreEqual(0, result.Count);
        _localAiImageServiceMock.Verify(m => m.GenerateBase64ImageAsync(IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task GenerateImagesAsync_SingleImageFailure_SkipsFailedIndex()
    {
        _service = CreateService("localai");
        _replicateAiServiceMock.Setup(m => m.GenerateImagePrompts("apple"))
            .ReturnsAsync(new List<string> { "prompt one", "prompt two", "prompt three" });
        _localAiImageServiceMock.Setup(m => m.GenerateBase64ImageAsync("prompt one"))
            .ReturnsAsync("image-one");
        _localAiImageServiceMock.Setup(m => m.GenerateBase64ImageAsync("prompt two"))
            .ThrowsAsync(new HttpRequestException("failed"));
        _localAiImageServiceMock.Setup(m => m.GenerateBase64ImageAsync("prompt three"))
            .ReturnsAsync("image-three");

        var result = await _service.GenerateImagesAsync("apple");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("image-one", result[0]);
        Assert.AreEqual("image-three", result[1]);
    }

    [TestMethod]
    public void ToLowerCaseAndReplaceNonAlphabeticCharacters_CanReplace()
    {
        Assert.AreEqual("ap/apple.json", "Apple".GetImageFilePath());
        Assert.AreEqual("th/the-is-not-a-----correct-phrase.json", "The is not a !@# correct phrase".GetImageFilePath());
        Assert.AreEqual("do/download-an-image.json", "download-an-image".GetImageFilePath());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_IsPhraseVerify_DoNotAddToPending(bool isPhraseVerify)
    {
        var stream = new MemoryStream(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(new ImagesObject
        {
            isVerify = isPhraseVerify
        })));
        _firebaseServiceMock.Setup(DownloadObjectAsyncExpression).ReturnsAsync(stream);

        await _service.StartAsync(CancellationToken.None);
        _channel.Writer.TryWrite("test");
        await Task.Delay(100);
        await _service.StopAsync(CancellationToken.None);

        _repoMock.Verify(AddOrUpdateExpression, isPhraseVerify ? Times.Never : Times.Once);
    }

    static readonly Expression<Action<IDataRepository>> AddOrUpdateExpression = m => m.AddOrUpdate(IsAny<PendingVerifyPhrase>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Object?>>> GetObjectAsyncExpression = m => m.GetObject(IsAny<string>());
    static readonly Expression<Action<IFirebaseService>> UploadObjectExpression = m => m.UploadObject(IsAny<string>(), IsAny<Stream>());
    static readonly Expression<Func<IFirebaseService, ValueTask<Stream?>>> DownloadObjectAsyncExpression = m => m.DownloadObjectAsync(IsAny<string>());
}
