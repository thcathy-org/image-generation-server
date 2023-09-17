using System.Threading.Channels;
using ImageGenerationServer.DB;
using ImageGenerationServer.Middleware;
using ImageGenerationServer.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
Log.Information("Logger setup completed");

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiKeyMiddlewareOptions>(builder.Configuration.GetSection(nameof(ApiKeyMiddlewareOptions)));
builder.Services.Configure<ReplicateAiServiceOptions>(builder.Configuration.GetSection(nameof(ReplicateAiServiceOptions)));
builder.Services.Configure<FirebaseServiceOptions>(builder.Configuration.GetSection(nameof(FirebaseServiceOptions)));

builder.Services.AddSingleton(Channel.CreateUnbounded<string>());
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();
builder.Services.AddSingleton<IReplicateAiService, ReplicateAiService>();
builder.Services.AddSingleton<IVerifyService, VerifyService>();
builder.Services.AddHostedService<ImageGenerationService>();
builder.Services.AddScoped<IDataRepository, DataRepository>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.WithOrigins("https://localhost:44351", "http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LocalDatabase")));
builder.Services.AddHealthChecks().AddDbContextCheck<DataContext>();

// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", builder.Configuration.GetValue<string>("GoogleApplicationCredentials"));

var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<DataContext>();
    context.Database.Migrate();
}

// app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();
