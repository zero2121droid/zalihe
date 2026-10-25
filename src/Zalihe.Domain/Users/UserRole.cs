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
