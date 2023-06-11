using ImageGenerationServer.DTO;
using Microsoft.EntityFrameworkCore;

namespace ImageGenerationServer.DB;

public class DataContext : DbContext
{
    private readonly IConfiguration? _configuration;

    public DataContext()
    {
    }

    public DataContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(_configuration?.GetConnectionString("LocalDatabase"));
    }
    
    public virtual DbSet<PendingVerifyPhrase> PendingVerifyPhrases { get; set; }
}