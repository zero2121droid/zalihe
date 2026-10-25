using Zalihe.Domain.Users;

namespace Zalihe.Application.Common;

/// <summary>
/// Whether the current user may see purchase prices and anything computed from them, e.g. stock value
/// (SPEC 11.2). The one place that decides it: admins and managers always may, employees when the company
/// allows it. The company setting is read from the database (once per request), not from the cookie, so
/// turning it off applies at the next request. Background jobs and webhooks have no user and may.
/// </summary>
public class PurchasePriceAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public const string NotAllowed = "item.purchase_price_not_allowed";

    private bool? _canSee;

    public async Task<bool> CanSeeAsync(CancellationToken ct) =>
        _canSee ??= currentUser.Role is not { } role
            || role.SeesPurchasePrices(role == UserRole.Employee && await db.EmployeesSeePurchasePricesAsync(ct));
}
