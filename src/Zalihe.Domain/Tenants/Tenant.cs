namespace Zalihe.Domain.Tenants;

/// <summary>A company using the application. Root of all company data.</summary>
public class Tenant
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Tenant(string name, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));

        Id = Guid.CreateVersion7();
        Name = name;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private Tenant()
    {
        Name = null!;
    }
}
