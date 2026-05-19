using ArbiScannerWeb.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IRabbitMqService
    {
        Task StartConsumingAsync(CancellationToken cancellationToken);
        Task StopConsumingAsync();
        event Func<TradeOpportunityModel, Task> OnMessageReceived;
    }
}