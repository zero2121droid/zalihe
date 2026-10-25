namespace Zalihe.Domain.Users;

/// <summary>What a user may do in the company (SPEC 11.2).</summary>
public enum UserRole
{
    /// <summary>Everything: company settings, users and their roles, connecting the shop.</summary>
    Admin,

    /// <summary>Items, stock, imports, reports and maintenance, with purchase prices; not users or roles.</summary>
    Manager,

    /// <summary>Items, stock movements and orders.</summary>
    Employee,
}

public static class UserRoles
{
    /// <summary>
    /// Whether this role sees purchase prices and what is computed from them (SPEC 11.2): admins and managers
    /// always, employees when the company allows it (<see cref="Tenants.Tenant.EmployeesSeePurchasePrices"/>).
    /// </summary>
    public static bool SeesPurchasePrices(this UserRole role, bool employeesSeePurchasePrices) =>
        role != UserRole.Employee || employeesSeePurchasePrices;
}
