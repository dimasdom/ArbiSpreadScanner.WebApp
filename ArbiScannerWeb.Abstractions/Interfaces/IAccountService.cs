using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IAccountService
    {
        public Task<Result<AccountEditDto>> UpdateDetails(AccountEditDto account);
        public Task<Result<AccountDto>> GetUserData();
    }
}
