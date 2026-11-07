using ImageGenerationServer.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ImageGenerationServer.UT.Middleware;

[TestClass]
public class ApiKeyMiddlewareTest
{
    [TestMethod]
    public async Task ImageRemove_WithoutApiKey_Returns401()
    {
        var called = false;
        var middleware = Middleware(() => called = true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/image/remove";

        await middleware.InvokeAsync(context);

        Assert.IsFalse(called);
        Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task ImageRemove_WithApiKey_Continues()
    {
        var called = false;
        var middleware = Middleware(() => called = true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/image/remove";
        context.Request.Headers["X-API-KEY"] = "secret";

        await middleware.InvokeAsync(context);

        Assert.IsTrue(called);
    }

    private static ApiKeyMiddleware Middleware(Action onNext)
    {
        return new ApiKeyMiddleware(_ =>
        {
            onNext();
            return Task.CompletedTask;
        }, Options.Create(new ApiKeyMiddlewareOptions
        {
            ApiKeyHeaderName = "X-API-KEY",
            ApiKeys = "secret"
        }));
    }
}
