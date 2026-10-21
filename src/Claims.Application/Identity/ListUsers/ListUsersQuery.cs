using Claims.Application.Common.Paging;
using Claims.Domain.Common;
using FluentValidation;
using MediatR;

namespace Claims.Application.Identity.ListUsers;

public sealed record ListUsersQuery(int Page, int PageSize, string? Role) : IRequest<PagedResult<UserResponse>>;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
        RuleFor(q => q.Role)
            .Must(role => role is null || Roles.All.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
    }
}

public sealed class ListUsersQueryHandler(IIdentityService identity)
    : IRequestHandler<ListUsersQuery, PagedResult<UserResponse>>
{
    public Task<PagedResult<UserResponse>> Handle(ListUsersQuery request, CancellationToken cancellationToken) =>
        identity.ListUsersAsync(request.Page, request.PageSize, request.Role, cancellationToken);
}
