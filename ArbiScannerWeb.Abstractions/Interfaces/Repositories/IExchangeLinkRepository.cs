using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public interface IExchangeLinkRepository
    {
        Task<List<ExchangeLinkModel>> GetAllAsync();
        Task<ExchangeLinkModel?> GetByExchangeNameAsync(string exchangeName);
    }
}
