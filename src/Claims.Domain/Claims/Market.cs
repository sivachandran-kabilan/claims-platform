namespace Claims.Domain.Claims;

/// <summary>The six markets. One market (and therefore one currency) per claim; no per-market rules.</summary>
public enum Market
{
    Singapore,
    Australia,
    HongKong,
    Malaysia,
    Thailand,
    Philippines,
}

public static class MarketExtensions
{
    /// <summary>ISO 4217 currency of the market. Claims never take a currency from the client.</summary>
    public static string Currency(this Market market) => market switch
    {
        Market.Singapore => "SGD",
        Market.Australia => "AUD",
        Market.HongKong => "HKD",
        Market.Malaysia => "MYR",
        Market.Thailand => "THB",
        Market.Philippines => "PHP",
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, "Unknown market."),
    };
}
