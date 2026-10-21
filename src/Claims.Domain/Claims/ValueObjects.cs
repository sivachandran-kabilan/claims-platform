using Claims.Domain.Common;

namespace Claims.Domain.Claims;

/// <summary>An amount with its ISO currency. Used wherever an amount leaves its claim (events, DTOs, reports).</summary>
public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        }

        Amounts.EnsureTwoDecimals(amount);
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }
}

/// <summary>Claim reference shown to users, e.g. CLM-2026-000123. The sequence comes from the database.</summary>
public sealed record ClaimNumber
{
    private ClaimNumber(string value) => Value = value;

    public string Value { get; }

    public static ClaimNumber Create(int year, long sequence)
    {
        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        return new ClaimNumber($"CLM-{year}-{sequence:D6}");
    }

    /// <summary>Rehydrates a stored number (persistence only).</summary>
    public static ClaimNumber FromStored(string value) => new(value);

    public override string ToString() => Value;
}

/// <summary>Who performed an action. Always built from the validated token, never from a request body.</summary>
public sealed record Actor
{
    public Actor(Guid userId, string role)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Actor id is required.", nameof(userId));
        }

        if (!Roles.All.Contains(role))
        {
            throw new ArgumentException($"Unknown role '{role}'.", nameof(role));
        }

        UserId = userId;
        Role = role;
    }

    public Guid UserId { get; }
    public string Role { get; }
}

internal static class Amounts
{
    public static void EnsureTwoDecimals(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new BusinessRuleException("Amounts may have at most 2 decimal places.");
        }
    }

    public static void EnsurePositive(decimal amount, string what)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException($"{what} must be greater than zero.");
        }

        EnsureTwoDecimals(amount);
    }
}
