using IntegrationHub.Application.Common;
using IntegrationHub.Application.Users;
using IntegrationHub.Domain.Common;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Identity;

public sealed class EfIdentityUserManagementStore(IntegrationHubDbContext db, UserManager<IntegrationHubUser> users, RoleManager<IdentityRole> roles) : IUserManagementStore
{
    public async Task<PagedResult<UserDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var set = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) set = set.Where(x => EF.Functions.Like(x.Email!, $"%{query}%") || EF.Functions.Like(x.FullName, $"%{query}%"));
        var total = await set.CountAsync(ct);
        var rows = await set.OrderBy(x => x.Email).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var roleMap = await GetRoleMapAsync(rows.Select(x => x.Id).ToArray(), ct);
        var items = rows.Select(x => Map(x, roleMap.GetValueOrDefault(x.Id, []))).ToList();
        return new PagedResult<UserDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<UserDto?> GetAsync(string id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return null;
        return Map(user, (await users.GetRolesAsync(user)).ToArray());
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!await roles.RoleExistsAsync(request.Role)) throw new DomainException("Role is not configured.");
        var user = new IntegrationHubUser { UserName = request.Email, Email = request.Email, FullName = request.FullName, IsActive = true, EmailConfirmed = true };
        var result = await users.CreateAsync(user, request.Password);
        Ensure(result, "User could not be created.");
        Ensure(await users.AddToRoleAsync(user, request.Role), "User role could not be assigned.");
        return Map(user, [request.Role]);
    }

    public async Task<bool> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id); if (user is null) return false;
        if (!await roles.RoleExistsAsync(request.Role)) throw new DomainException("Role is not configured.");
        var duplicate = await users.FindByEmailAsync(request.Email);
        if (duplicate is not null && duplicate.Id != id) throw new BusinessConflictException("Email is already assigned to another user.");
        user.Email = request.Email; user.UserName = request.Email; user.FullName = request.FullName;
        Ensure(await users.UpdateAsync(user), "User could not be updated.");
        var currentRoles = await users.GetRolesAsync(user);
        Ensure(await users.RemoveFromRolesAsync(user, currentRoles), "Previous roles could not be removed.");
        Ensure(await users.AddToRoleAsync(user, request.Role), "New role could not be assigned.");
        return true;
    }

    public async Task<bool> SetActiveAsync(string id, bool active, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id); if (user is null) return false;
        user.IsActive = active;
        Ensure(await users.UpdateAsync(user), "User status could not be updated.");
        return true;
    }

    private async Task<Dictionary<string, string[]>> GetRoleMapAsync(string[] ids, CancellationToken ct)
    {
        var pairs = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where ids.Contains(userRole.UserId)
                           select new { userRole.UserId, role.Name }).ToListAsync(ct);
        return pairs.GroupBy(x => x.UserId).ToDictionary(group => group.Key, group => group.Select(x => x.Name!).Order().ToArray());
    }

    private static UserDto Map(IntegrationHubUser user, IReadOnlyList<string> userRoles) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, userRoles, user.CreatedAt);

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded) throw new DomainException($"{message} {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
