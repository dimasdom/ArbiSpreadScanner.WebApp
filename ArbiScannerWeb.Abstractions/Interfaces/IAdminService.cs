using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArbiScannerWeb.Domain.Models;
using FluentResults;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IAdminService
    {
        Task<Result<UserSubscriptionPayment>> GetPaymentStatusAsync(int paymentId);
        Task<Result<SubscriptionModel>> GetSubscriptionDetailsAsync(int subscriptionId);
        Task<Result<UserSubscriptionPayment>> CreatePaymentAsync(string userId, int subscriptionId);
        Task<Result<UserSubscriptionModel>> GetUserActiveSubscriptionsAsync(string userId);
        Task<Result<UserSubscriptionPayment>> GetUserActivePaymentsAsync(string userId);
        Task<Result<List<SubscriptionModel>>> GetAllSubscriptionsAsync();
        Task<Result> CancelPayment(int userSubscriptionPaymentId);
    }
}
