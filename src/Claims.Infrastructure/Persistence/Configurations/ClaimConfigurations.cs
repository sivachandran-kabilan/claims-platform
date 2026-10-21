using Claims.Application.Common.Persistence;
using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Number)
            .HasConversion(n => n.Value, v => ClaimNumber.FromStored(v))
            .HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.HasIndex(c => c.Number).IsUnique();

        // Enums as strings: readable rows, and reports group by name.
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Market).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(c => c.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        builder.Property(c => c.PolicyNumber).HasMaxLength(30);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.ContactPhone).HasMaxLength(21);
        builder.Property(c => c.RejectionReason).HasMaxLength(1000);

        builder.Property(c => c.ClaimedAmount).HasPrecision(18, 2);
        builder.Property(c => c.Reserve).HasPrecision(18, 2);
        builder.Property(c => c.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(c => c.OutstandingReserve).HasPrecision(18, 2);
        builder.Property(c => c.SettledAmount).HasPrecision(18, 2);

        // Concurrency token and ETag source; a shadow property so the domain stays persistence-free.
        builder.Property<byte[]>(ClaimsDbConstants.RowVersion).IsRowVersion();

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.OpenInformationRequest);

        builder.HasMany(c => c.InformationRequests).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Documents).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.History).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.InformationRequests).HasField("_informationRequests").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes for the real queries: queue/workload, exposure, claimant's list, date sorting.
        builder.HasIndex(c => new { c.Status, c.AssignedToUserId }).IncludeProperties(c => c.SubmittedAt);
        builder.HasIndex(c => new { c.Market, c.Type, c.Status }).IncludeProperties(c => c.OutstandingReserve);
        builder.HasIndex(c => new { c.ClaimantUserId, c.SubmittedAt });
        builder.HasIndex(c => c.SubmittedAt);
    }
}

internal sealed class InformationRequestConfiguration : IEntityTypeConfiguration<InformationRequest>
{
    public void Configure(EntityTypeBuilder<InformationRequest> builder)
    {
        builder.ToTable("ClaimInformationRequests");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Question).HasMaxLength(2000);
        builder.Property(r => r.Response).HasMaxLength(2000);
        builder.Ignore(r => r.IsOpen);
    }
}

internal sealed class DocumentReferenceConfiguration : IEntityTypeConfiguration<DocumentReference>
{
    public void Configure(EntityTypeBuilder<DocumentReference> builder)
    {
        builder.ToTable("ClaimDocuments");
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.ContentType).HasMaxLength(100);
        builder.Property(d => d.StorageRef).HasMaxLength(500);
    }
}

internal sealed class ClaimHistoryEntryConfiguration : IEntityTypeConfiguration<ClaimHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ClaimHistoryEntry> builder)
    {
        builder.ToTable("ClaimHistory");
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ActorRole).HasMaxLength(20);
        builder.Property(h => h.Reason).HasMaxLength(1000);
        builder.Property(h => h.Amount).HasPrecision(18, 2);
        builder.HasIndex("ClaimId", nameof(ClaimHistoryEntry.Sequence)).IsUnique();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(100);
        builder.Property(m => m.LastError).HasMaxLength(2000);
        // The relay reads unprocessed rows in order.
        builder.HasIndex(m => m.OccurredAt).HasFilter("[ProcessedAt] IS NULL");
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => new { r.UserId, r.Key });
        builder.Property(r => r.Key).HasMaxLength(100);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsUnicode(false);
    }
}
