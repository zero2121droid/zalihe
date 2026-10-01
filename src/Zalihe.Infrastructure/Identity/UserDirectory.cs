using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Identity;

public class UserDirectory(AppDbContext db, ITenantContext tenant) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        // Users have no global query filter (sign-in needs them before the company is known),
        // so this is the one place that limits them to the current company explicitly.
        await db.Users
            .Where(u => userIds.Contains(u.Id) && u.TenantId == tenant.TenantId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
}
