using Microsoft.Extensions.Configuration;
using Serilog;

namespace ImageGenerationServer.UT;

public abstract class TestBase
{
    protected IConfiguration Config { get; init; }
    
    static TestBase()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
    }

    public TestBase()
    {
        Config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json")
                    .Build();
    }
}