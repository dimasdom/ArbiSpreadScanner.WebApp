using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace ArbiScannerWeb.Infrastructure.Repositories
{
    public class ExchangeLinkRepository : IExchangeLinkRepository
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public ExchangeLinkRepository(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<List<ExchangeLinkModel>> GetAllAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.ExchangeLinks.AsNoTracking().OrderBy(x => x.Exchange).ToListAsync();
        }

        public async Task<ExchangeLinkModel?> GetByExchangeNameAsync(string exchangeName)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.ExchangeLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Exchange.ToLower() == exchangeName.ToLower());
        }
    }
}
