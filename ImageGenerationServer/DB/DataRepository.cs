using ImageGenerationServer.DTO;
using Microsoft.EntityFrameworkCore;

namespace ImageGenerationServer.DB;

public interface IDataRepository
{
    Task AddOrUpdate(PendingVerifyPhrase pendingPhrase);
}

public class DataRepository : IDataRepository
{
    private readonly DataContext _context;

    public DataRepository(DataContext context) => _context = context;

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    
    public void AddPendingVerifyPhrases(PendingVerifyPhrase pendingPhrase) => _context.PendingVerifyPhrases.Add(pendingPhrase);
    
    public async Task AddOrUpdate(PendingVerifyPhrase pendingVerifyPhrase)
    {
        var exists = await _context.PendingVerifyPhrases.AnyAsync(e => e.Phrase.Equals(pendingVerifyPhrase.Phrase));

        if (!exists)
        {
            await _context.PendingVerifyPhrases.AddAsync(pendingVerifyPhrase);
        }
        else
        {
            _context.PendingVerifyPhrases.Update(pendingVerifyPhrase);
        }
        await _context.SaveChangesAsync();
    }
}

