namespace Zalihe.Domain.Tenants;

/// <summary>A company using the application. Root of all company data.</summary>
public class Tenant
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Whether employees see purchase prices and what is computed from them (SPEC 11.2); admins and
    /// managers always do. Off by default, so a careful owner is protected from the start.
    /// </summary>
    public bool EmployeesSeePurchasePrices { get; private set; }

    public Tenant(string name, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Name = ValidName(name);
        CreatedAt = createdAt;
    }

    /// <summary>Changes the company's name; the same rules apply as at registration.</summary>
    public void Rename(string name) => Name = ValidName(name);

    public void SetEmployeesSeePurchasePrices(bool value) => EmployeesSeePurchasePrices = value;

    private static string ValidName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
        return name;
    }

    // For EF Core.
    private Tenant()
    {
        Name = null!;
    }
}
