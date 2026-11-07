using System.Threading.Channels;
using ImageGenerationServer.Middleware;
using ImageGenerationServer.Services;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
Log.Information("Logger setup completed");

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiKeyMiddlewareOptions>(builder.Configuration.GetSection(nameof(ApiKeyMiddlewareOptions)));
builder.Services.Configure<ReplicateAiServiceOptions>(builder.Configuration.GetSection(nameof(ReplicateAiServiceOptions)));
builder.Services.Configure<LocalAiImageServiceOptions>(builder.Configuration.GetSection(nameof(LocalAiImageServiceOptions)));
builder.Services.Configure<ImageGenerationServiceOptions>(builder.Configuration.GetSection(nameof(ImageGenerationServiceOptions)));
builder.Services.Configure<FirebaseServiceOptions>(builder.Configuration.GetSection(nameof(FirebaseServiceOptions)));

builder.Services.AddSingleton(Channel.CreateUnbounded<string>());
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();
builder.Services.AddSingleton<IReplicateAiService, ReplicateAiService>();
builder.Services.AddSingleton<ILocalAiImageService, LocalAiImageService>();
builder.Services.AddSingleton<IImageRemovalService, ImageRemovalService>();
builder.Services.AddHostedService<ImageGenerationService>();

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", builder.Configuration.GetValue<string>("GoogleApplicationCredentials"));

var app = builder.Build();

app.UseAuthorization();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();
