using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IUrlService
    {
        Task SetUpTunelAndAddToDb();
        Task AddNewUrl(string url);
    }
}
