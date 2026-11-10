using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Admin;
using SpendScope.Contracts.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Admin.ListUsers;

public sealed record ListUsersQuery(int? Page, int? PageSize, string? Search);

/// <summary>Spec 15.20. No expense data is ever returned. FR-ADM-01 and 04.</summary>
public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search).MaximumLength(256).WithErrorCode("search_invalid");
    }
}

public sealed class ListUsersHandler(IUserAccounts accounts)
{
    public async Task<PagedResponse<AdminUserDto>> HandleAsync(ListUsersQuery query, CancellationToken ct)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? Pagination.DefaultPageSize;
        var users = await accounts.ListAsync(page, pageSize, query.Search, ct);

        return new PagedResponse<AdminUserDto>(
            users.Items.Select(u => new AdminUserDto(u.Id, u.Email, u.DisplayName, u.Roles, u.IsLocked, u.CreatedAtUtc)).ToList(),
            users.Page,
            users.PageSize,
            users.TotalCount);
    }
}

public sealed class ListUsersEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/admin/users", async ([AsParameters] ListUsersQuery query, ListUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(query, ct)))
            .WithValidation<ListUsersQuery>()
            .RequireAuthorization(Policies.RequireAdmin)
            .WithTags("Admin")
            .WithName("ListUsers")
            .Produces<PagedResponse<AdminUserDto>>();
}
