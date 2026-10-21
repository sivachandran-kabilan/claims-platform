using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Claims.Application.Claims.Common;
using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Claims.Submit;

/// <summary>Request body for POST /claims. There is no currency field: it comes from the market.</summary>
public sealed record SubmitClaimRequest(
    ClaimType Type, Market Market, string PolicyNumber, DateOnly IncidentDate,
    string Description, string ContactPhone, decimal ClaimedAmount);

public sealed record SubmitClaimCommand(
    string? IdempotencyKey, ClaimType Type, Market Market, string PolicyNumber, DateOnly IncidentDate,
    string Description, string ContactPhone, decimal ClaimedAmount) : IRequest<ClaimDetailResult>
{
    public static SubmitClaimCommand From(string? idempotencyKey, SubmitClaimRequest r) =>
        new(idempotencyKey, r.Type, r.Market, r.PolicyNumber, r.IncidentDate, r.Description, r.ContactPhone, r.ClaimedAmount);
}

public sealed class SubmitClaimCommandValidator : AbstractValidator<SubmitClaimCommand>
{
    public SubmitClaimCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.IdempotencyKey).NotEmpty().MaximumLength(100)
            .WithMessage("The Idempotency-Key header is required (max 100 characters).");
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.Market).IsInEnum();
        RuleFor(c => c.PolicyNumber).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9/-]+$")
            .WithMessage("Policy number may contain letters, digits, '-' and '/' (max 30).");
        RuleFor(c => c.IncidentDate).NotEmpty()
            .Must(date => date <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("Incident date cannot be in the future.");
        RuleFor(c => c.Description).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.ContactPhone).NotEmpty().Matches(@"^\+?[0-9]{6,20}$")
            .WithMessage("Contact phone must be 6-20 digits, optionally starting with '+'.");
        RuleFor(c => c.ClaimedAmount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public sealed class SubmitClaimCommandHandler(
    IClaimsDbContext db,
    IClaimNumberGenerator numbers,
    ICurrentUser currentUser,
    TimeProvider clock) : IRequestHandler<SubmitClaimCommand, ClaimDetailResult>
{
    public async Task<ClaimDetailResult> Handle(SubmitClaimCommand request, CancellationToken cancellationToken)
    {
        var key = request.IdempotencyKey!;
        var hash = Hash(request with { IdempotencyKey = null });

        var previous = await FindRecordAsync(key, cancellationToken);
        if (previous is not null)
        {
            return await ReplayAsync(previous, hash, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var number = ClaimNumber.Create(now.Year, await numbers.NextAsync(cancellationToken));
        var claim = Claim.Submit(
            number, currentUser.ToActor(), request.Type, request.Market, request.PolicyNumber,
            request.IncidentDate, request.Description, request.ContactPhone, request.ClaimedAmount, now);

        db.Claims.Add(claim);
        db.IdempotencyRecords.Add(new IdempotencyRecord(currentUser.UserId, key, hash, claim.Id, now));

        try
        {
            // One SaveChanges: claim, history, outbox row and idempotency record commit together.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request with the same key won the race: return its claim.
            var winner = await FindRecordAsync(key, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return await ReplayAsync(winner, hash, cancellationToken);
        }

        return await LoadAsync(claim.Id, cancellationToken);
    }

    private Task<IdempotencyRecord?> FindRecordAsync(string key, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);
    }

    private async Task<ClaimDetailResult> ReplayAsync(IdempotencyRecord record, string hash, CancellationToken cancellationToken)
    {
        if (record.RequestHash != hash)
        {
            throw new UnprocessableException("This Idempotency-Key was already used with a different request.");
        }

        return await LoadAsync(record.ClaimId, cancellationToken);
    }

    private async Task<ClaimDetailResult> LoadAsync(Guid claimId, CancellationToken cancellationToken) =>
        await ClaimReadModel.GetDetailAsync(db, claimId, currentUser, cancellationToken)
        ?? throw new InvalidOperationException("Submitted claim could not be read back.");

    private static string Hash(SubmitClaimCommand request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
}
