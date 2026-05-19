using ArbiScannerWeb.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace ArbiScannerWeb.Infrastructure.EntityConfigurations;

public class TradeOpportunityModelConfiguration : IEntityTypeConfiguration<TradeOpportunityModel>
{
    public void Configure(EntityTypeBuilder<TradeOpportunityModel> builder)
    {
        var jsonConverter = new ValueConverter<List<OrderLevelModel>?, string>(
            v => JsonSerializer.Serialize(v ?? new List<OrderLevelModel>(), new JsonSerializerOptions()),
            v => JsonSerializer.Deserialize<List<OrderLevelModel>>(v, new JsonSerializerOptions()) ?? new List<OrderLevelModel>()
        );

        builder.HasKey(p => p.Guid);

        builder.Property(p => p.BidsExchangeA)
            .HasConversion(jsonConverter)
            .HasColumnType("jsonb");

        builder.Property(p => p.AsksExchangeA)
            .HasConversion(jsonConverter)
            .HasColumnType("jsonb");

        builder.Property(p => p.BidsExchangeB)
            .HasConversion(jsonConverter)
            .HasColumnType("jsonb");

        builder.Property(p => p.AsksExchangeB)
            .HasConversion(jsonConverter)
            .HasColumnType("jsonb");
    }
}
