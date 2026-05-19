using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IEmailService
    {
        public Task<Result> SendEmailAsync(string toEmail, string subject, string body);
    }
}
