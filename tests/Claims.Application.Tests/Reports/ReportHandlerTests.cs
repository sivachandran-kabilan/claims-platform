using Claims.Application.Common.Security;
using Claims.Application.Reports;
using Claims.Application.Tests.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace Claims.Application.Tests.Reports;

public sealed class GetWorkloadQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 4, 0, 0, TimeSpan.Zero);
    private readonly IReportQueries _reports = Substitute.For<IReportQueries>();

    private Task Run(Guid userId, string role)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId);
        user.Role.Returns(role);
        return new GetWorkloadQueryHandler(_reports, user, new FixedTimeProvider(Now)).Handle(new GetWorkloadQuery(), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_AsStaff_QueriesOnlyOwnUserId()
    {
        var me = Guid.NewGuid();

        await Run(me, Roles.ClaimStaff);

        await _reports.Received(1).GetWorkloadAsync(me, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AsManager_QueriesAllStaff()
    {
        await Run(Guid.NewGuid(), Roles.ClaimManager);

        await _reports.Received(1).GetWorkloadAsync(null, Now, Arg.Any<CancellationToken>());
    }
}
