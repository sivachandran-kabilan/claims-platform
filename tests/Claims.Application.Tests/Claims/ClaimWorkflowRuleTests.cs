using Claims.Application.Claims.AddDocument;
using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace Claims.Application.Tests.Claims;

public sealed class ActRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid AssignedStaffId = Guid.NewGuid();

    private static Claim AssignedClaim()
    {
        var claim = Claim.Submit(ClaimNumber.Create(2026, 1), new Actor(OwnerId, Roles.Claimant), ClaimType.Motor,
            Market.Singapore, "POL-1", new DateOnly(2026, 10, 1), "Dent", "+6591234567", 10m, Now);
        claim.Assign(AssignedStaffId, new Actor(AssignedStaffId, Roles.ClaimStaff), Now);
        return claim;
    }

    private static ICurrentUser User(Guid id, string role)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(id);
        user.Role.Returns(role);
        return user;
    }

    [Fact]
    public void AssignedStaffOrManager_AllowsAssignedStaffAndAnyManager_DeniesOthers()
    {
        var claim = AssignedClaim();

        ClaimAccess.AssignedStaffOrManager(claim, User(AssignedStaffId, Roles.ClaimStaff)).Should().BeTrue();
        ClaimAccess.AssignedStaffOrManager(claim, User(Guid.NewGuid(), Roles.ClaimManager)).Should().BeTrue();
        ClaimAccess.AssignedStaffOrManager(claim, User(Guid.NewGuid(), Roles.ClaimStaff)).Should().BeFalse();
        ClaimAccess.AssignedStaffOrManager(claim, User(OwnerId, Roles.Claimant)).Should().BeFalse();
    }

    [Fact]
    public void Owner_AllowsOnlyTheOwningClaimant()
    {
        var claim = AssignedClaim();

        ClaimAccess.Owner(claim, User(OwnerId, Roles.Claimant)).Should().BeTrue();
        ClaimAccess.Owner(claim, User(Guid.NewGuid(), Roles.Claimant)).Should().BeFalse();
        ClaimAccess.Owner(claim, User(AssignedStaffId, Roles.ClaimStaff)).Should().BeFalse();
    }

    [Fact]
    public void CanAssign_StaffOnlySelf_ManagerAnyone()
    {
        var claim = AssignedClaim();
        var staffId = Guid.NewGuid();

        ClaimAccess.CanAssign(staffId)(claim, User(staffId, Roles.ClaimStaff)).Should().BeTrue();
        ClaimAccess.CanAssign(Guid.NewGuid())(claim, User(staffId, Roles.ClaimStaff)).Should().BeFalse();
        ClaimAccess.CanAssign(Guid.NewGuid())(claim, User(Guid.NewGuid(), Roles.ClaimManager)).Should().BeTrue();
    }

    [Fact]
    public void DocumentUploader_AllowsOwnerAssignedStaffAndManager()
    {
        var claim = AssignedClaim();

        ClaimAccess.DocumentUploader(claim, User(OwnerId, Roles.Claimant)).Should().BeTrue();
        ClaimAccess.DocumentUploader(claim, User(AssignedStaffId, Roles.ClaimStaff)).Should().BeTrue();
        ClaimAccess.DocumentUploader(claim, User(Guid.NewGuid(), Roles.ClaimManager)).Should().BeTrue();
        ClaimAccess.DocumentUploader(claim, User(Guid.NewGuid(), Roles.ClaimStaff)).Should().BeFalse();
    }

    [Fact]
    public void EnsureCanAct_RuleFails_ThrowsForbidden()
    {
        var act = () => ClaimAccess.EnsureCanAct(AssignedClaim(), User(Guid.NewGuid(), Roles.ClaimStaff), ClaimAccess.AssignedStaffOrManager);

        act.Should().Throw<ForbiddenAccessException>();
    }
}

public sealed class ETagTests
{
    [Fact]
    public void ParseIfMatch_RoundTripsFormattedETag()
    {
        byte[] version = [0, 0, 0, 0, 0, 0, 7, 210];

        ETags.ParseIfMatch(ETags.Format(version)).Should().Equal(version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseIfMatch_Missing_ThrowsPreconditionRequired(string? ifMatch)
    {
        var act = () => ETags.ParseIfMatch(ifMatch);

        act.Should().Throw<PreconditionRequiredException>();
    }

    [Theory]
    [InlineData("AAAAAAAAB9I=")]          // unquoted
    [InlineData("W/\"AAAAAAAAB9I=\"")]    // weak
    [InlineData("*")]
    [InlineData("\"not base64!\"")]
    public void ParseIfMatch_Malformed_ReturnsNull(string ifMatch)
    {
        ETags.ParseIfMatch(ifMatch).Should().BeNull();
    }
}

public sealed class ActionValidatorTests
{
    [Theory]
    [InlineData("application/pdf", 1, true)]
    [InlineData("image/png", Claim.MaxDocumentBytes, true)]
    [InlineData("text/html", 100, false)]
    [InlineData("image/jpeg", Claim.MaxDocumentBytes + 1, false)]
    public void AddDocumentValidator_ContentTypeAndSize(string contentType, long size, bool valid)
    {
        var command = new AddDocumentCommand { FileName = "a", ContentType = contentType, SizeBytes = size, StorageRef = "ref" };

        new AddDocumentCommandValidator().Validate(command).IsValid.Should().Be(valid);
    }
}
