namespace Zalihe.Application.Common;

/// <summary>The signed-in user of the current request; null for webhooks and background jobs.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}

/// <summary>Display names of users, e.g. "who entered this movement".</summary>
public interface IUserDirectory
{
    /// <summary>Names of the given users. Ids come from the current company's own data.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
