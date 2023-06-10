using System.Net;
using Microsoft.Extensions.Options;

namespace ImageGenerationServer.Middleware;

public class ApiKeyMiddlewareOptions
{
    public const string Separator = ",";
    public string ApiKeyHeaderName { get; set; } = string.Empty;
    public string ApiKeys { get; set; } = string.Empty;
}
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKeyHeaderName;
    private readonly string[] _apiKeys;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyMiddlewareOptions> options)
    {
        _next = next;
        _apiKeyHeaderName = options.Value.ApiKeyHeaderName;
        _apiKeys = options.Value.ApiKeys.Split(ApiKeyMiddlewareOptions.Separator);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? apiKey = context.Request.Headers[_apiKeyHeaderName];
        
        if (!_apiKeys.Contains(apiKey))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("API key is required.");
            return;
        }
        
        await _next(context);
    }
}