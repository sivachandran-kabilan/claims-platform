using Claims.Application.Claims.Common;
using Claims.Application.Claims.Submit;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace Claims.Application.Tests.Claims;

/// <summary>Fixed clock (ADR 007: hand-written instead of the TimeProvider.Testing package).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class SubmitClaimCommandValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
    private readonly SubmitClaimCommandValidator _validator = new(new FixedTimeProvider(Now));

    private static SubmitClaimCommand Valid() => new(
        "key-1", ClaimType.Motor, Market.Singapore, "POL-1/A", new DateOnly(2026, 10, 1), "Dent", "+6591234567", 100.5m);

    [Fact]
    public void Validate_ValidCommand_HasNoErrors() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(2)]   // two days after "today" in UTC
    [InlineData(30)]
    public void Validate_IncidentDateInFuture_Fails(int daysAhead)
    {
        var command = Valid() with { IncidentDate = new DateOnly(2026, 10, 21).AddDays(daysAhead) };

        _validator.Validate(command).Errors.Should().ContainSingle(e => e.PropertyName == "IncidentDate");
    }

    [Fact]
    public void Validate_IncidentDateTomorrowUtc_Passes()
    {
        var command = Valid() with { IncidentDate = new DateOnly(2026, 10, 22) };

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.234)]
    public void Validate_BadAmount_Fails(decimal amount)
    {
        _validator.Validate(Valid() with { ClaimedAmount = amount })
            .Errors.Should().Contain(e => e.PropertyName == "ClaimedAmount");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingIdempotencyKey_Fails(string? key)
    {
        _validator.Validate(Valid() with { IdempotencyKey = key })
            .Errors.Should().Contain(e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public void Validate_UndefinedMarket_Fails()
    {
        _validator.Validate(Valid() with { Market = (Market)99 })
            .Errors.Should().Contain(e => e.PropertyName == "Market");
    }
}

public sealed class ClaimAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    private static Claim ClaimOf(Guid claimantId) => Claim.Submit(
        ClaimNumber.Create(2026, 1), new Actor(claimantId, Roles.Claimant), ClaimType.Motor, Market.Singapore,
        "POL-1", new DateOnly(2026, 10, 1), "Dent", "+6591234567", 10m, Now);

    private static ICurrentUser User(Guid id, string role)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(id);
        user.Role.Returns(role);
        return user;
    }

    [Fact]
    public void VisibleTo_Claimant_SeesOnlyOwnClaims()
    {
        var me = Guid.NewGuid();
        var claims = new[] { ClaimOf(me), ClaimOf(Guid.NewGuid()) }.AsQueryable();

        claims.VisibleTo(User(me, Roles.Claimant)).Should().ContainSingle().Which.ClaimantUserId.Should().Be(me);
    }

    [Theory]
    [InlineData(Roles.ClaimStaff)]
    [InlineData(Roles.ClaimManager)]
    public void VisibleTo_StaffOrManager_SeesAllClaims(string role)
    {
        var claims = new[] { ClaimOf(Guid.NewGuid()), ClaimOf(Guid.NewGuid()) }.AsQueryable();

        claims.VisibleTo(User(Guid.NewGuid(), role)).Should().HaveCount(2);
    }
}
