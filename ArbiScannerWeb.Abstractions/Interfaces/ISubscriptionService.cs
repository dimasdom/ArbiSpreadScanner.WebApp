using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentResults;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface ISubscriptionService
    {
        Task<Result<List<SubscriptionModel>>> GetAllSubscriptionsAsync();
        Task<Result<UserSubscriptionPayment>> GetPaymentStatusAsync(int userSubscriptionPaymentId);
        Task<Result<SubscriptionModel>> GetSubscriptionDetailsAsync(int subscriptionId);
        Task<Result<UserSubscriptionPayment>> CreatePaymentAsync(int subscriptionId);
        Task<Result<UserSubscriptionModelDto>> GetUserActiveSubscriptionsAsync();
        Task<Result<UserSubscriptionPayment>> GetUserActivePaymentsAsync();
        Task<Result> CancelPayment(int userSubscriptionPaymentId);
        Task<bool> CheckIfUserHasActiveSubscriptionAsync();
    }
}
