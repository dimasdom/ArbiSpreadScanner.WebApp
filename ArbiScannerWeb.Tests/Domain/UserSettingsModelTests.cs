using ArbiScannerWeb.Domain.Models;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Domain;

public class UserSettingsModelTests
{
    private static TradeOpportunityModel MakeOpportunity(
        double askA = 100, double bidA = 100,
        double askB = 100, double bidB = 100) =>
        new()
        {
            Symbol = "BTC/USDT",
            ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "ExA", VolumeAsk = askA, VolumeBid = bidA },
            ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "ExB", VolumeAsk = askB, VolumeBid = bidB },
            ExchangeShort = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "ExA" },
            ExchangeLong  = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "ExB" }
        };

    [Fact]
    public void IsVolumeValid_AllVolumesAboveThreshold_ReturnsTrue()
    {
        var user = new UserSettingsModel { PositionSize = 10 };
        UserSettingsModel.IsVolumeValid(user, MakeOpportunity(100, 100, 100, 100)).Should().BeTrue();
    }

    [Fact]
    public void IsVolumeValid_VolumeAskABelowThreshold_ReturnsFalse()
    {
        var user = new UserSettingsModel { PositionSize = 10 };
        UserSettingsModel.IsVolumeValid(user, MakeOpportunity(askA: 20)).Should().BeFalse();
    }

    [Fact]
    public void IsVolumeValid_VolumeBidBBelowThreshold_ReturnsFalse()
    {
        var user = new UserSettingsModel { PositionSize = 10 };
        UserSettingsModel.IsVolumeValid(user, MakeOpportunity(bidB: 29)).Should().BeFalse();
    }

    [Fact]
    public void IsVolumeValid_ThresholdEqualsPositionSizeTimesThree()
    {
        var user = new UserSettingsModel { PositionSize = 5 };
        UserSettingsModel.IsVolumeValid(user, MakeOpportunity(15, 15, 15, 15)).Should().BeFalse();
    }
}
