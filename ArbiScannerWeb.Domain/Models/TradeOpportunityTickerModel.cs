using ArbiScannerWeb.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArbiScannerWeb.Domain.Models
{
    public class TradeOpportunityTickerModel
    {
        public TradeOpportunityTickerModel() { }
        public TradeOpportunityTickerModel(TradeOpportunityModel possiblePosition)
        {
            Guid = possiblePosition.Guid;
            Symbol = possiblePosition.ExchangeRateA.Symbol;
            Spread = possiblePosition.Spread;
            EchangeA = possiblePosition.ExchangeRateA.Exchange;
            ExchangeB = possiblePosition.ExchangeRateB.Exchange;
            ExchangeLong = possiblePosition.ExchangeLong.Exchange;
            ExchangeShort = possiblePosition.ExchangeShort.Exchange;
            RateA = possiblePosition.ExchangeRateA.ExchangeRate;
            RateB = possiblePosition.ExchangeRateB.ExchangeRate;
            BidsExchangeA = possiblePosition.BidsExchangeA;
            AsksExchangeA = possiblePosition.AsksExchangeA;
            BidsExchangeB = possiblePosition.BidsExchangeB;
            AsksExchangeB = possiblePosition.AsksExchangeB;
            DateTime = DateTime.UtcNow;
        }
        [Key]
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public double Spread { get; set; }
        public string EchangeA { get; set; } = string.Empty;
        public string ExchangeB { get; set; } = string.Empty;
        public string ExchangeLong { get; set; } = string.Empty;
        public string ExchangeShort { get; set; } = string.Empty;
        public double RateA { get; set; }
        public double RateB { get; set; }
        public DateTime DateTime { get; set; }

        public List<OrderLevelModel>? BidsExchangeA { get; set; }

        public List<OrderLevelModel>? AsksExchangeA { get; set; }
        public List<OrderLevelModel>? BidsExchangeB { get; set; }

        public List<OrderLevelModel>? AsksExchangeB { get; set; }
    }

    public class TradeOpportunityTickerModelConfiguration : IEntityTypeConfiguration<TradeOpportunityTickerModel>
    {
        private const string JsonbColumnType = "jsonb";

        public void Configure(EntityTypeBuilder<TradeOpportunityTickerModel> builder)
        {
            var jsonConverter = new ValueConverter<List<OrderLevelModel>?, string>(
                v => JsonSerializer.Serialize(v ?? new List<OrderLevelModel>(), new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<OrderLevelModel>>(v, new JsonSerializerOptions())
            );

            builder.Property(p => p.BidsExchangeA)
                   .HasConversion(jsonConverter)
                   .HasColumnType(JsonbColumnType); // Changed from nvarchar(max)

            builder.Property(p => p.AsksExchangeA)
                   .HasConversion(jsonConverter)
                   .HasColumnType(JsonbColumnType);

            builder.Property(p => p.BidsExchangeB)
                   .HasConversion(jsonConverter)
                   .HasColumnType(JsonbColumnType);

            builder.Property(p => p.AsksExchangeB)
                   .HasConversion(jsonConverter)
                   .HasColumnType(JsonbColumnType);

            builder.HasOne<TradeOpportunityModel>()
                   .WithMany()
                   .HasForeignKey(p => p.Guid)
                   .HasPrincipalKey(p => p.Guid);
        }
    }
}