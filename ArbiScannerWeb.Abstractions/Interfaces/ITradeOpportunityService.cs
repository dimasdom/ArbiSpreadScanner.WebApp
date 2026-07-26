using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface ITradeOpportunityService
    {
        public Task<Result<List<TradeOpportunityModel>>> GetAllSpreads();
        public Task<Result<List<TradeOpportunityDetailsDto>>> GetSpreadsForUser(string userId);
        public Task<Result> AddSpread(TradeOpportunityModel model);
        public Task<Result> UpdateSpread(TradeOpportunityModel model);
        public Task<Result> CloseSpread(TradeOpportunityModel model);
        public Task<Result<TradeOpportunityDetailsDto>> GetSpreadInfo(string id);
    }
}
