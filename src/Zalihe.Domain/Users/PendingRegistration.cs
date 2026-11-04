using System.Security.Cryptography;
using System.Text;

namespace Zalihe.Domain.Users;

public enum CodeCheck
{
    Correct,
    Wrong,
    Expired,
    NoAttemptsLeft,
}

/// <summary>
/// A registration waiting for the emailed code (SPEC 2.7). The company and its owner don't exist until the
/// code is confirmed, so an unconfirmed address can't hold an account. The screen that registered holds a
/// random token: without it the code is worthless, so someone registering the same address at the same time
/// can't have the real owner confirm their password. Registering the address again replaces the token.
/// </summary>
public class PendingRegistration
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int MaxCodes = 5;
    public static readonly TimeSpan CodeValidFor = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);

    /// <summary>Also the period of <see cref="MaxCodes"/>: then the registration is deleted and the count starts over.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromHours(24);

    public Guid Id { get; private set; }

    /// <summary>Trimmed and lowercase, like <see cref="Invitation.Email"/>.</summary>
    public string Email { get; private set; }

    public string CompanyName { get; private set; }
    public string Name { get; private set; }
    public string Language { get; private set; }

    /// <summary>Hashed the same way as on an account, which takes it over unchanged.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>SHA-256 of the token, hex.</summary>
    public string TokenHash { get; private set; }

    /// <summary>SHA-256 of the code together with <see cref="TokenHash"/>, hex.</summary>
    public string CodeHash { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset CodeSentAt { get; private set; }
    public int CodesSent { get; private set; }
    public int FailedAttempts { get; private set; }

    /// <param name="token">The secret from <see cref="Invitation.NewToken"/>, given to the registering screen and never stored.</param>
    /// <param name="code">The code from <see cref="NewCode"/>, sent by email and never stored.</param>
    public PendingRegistration(string email, string companyName, string name, string language, string passwordHash,
        string token, string code, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        Email = Invitation.NormalizeEmail(email);
        CompanyName = null!;
        Name = null!;
        Language = null!;
        PasswordHash = null!;
        TokenHash = null!;
        CodeHash = null!;
        StartedAt = now;
        Replace(companyName, name, language, passwordHash, token, code, now);
    }

    // For EF Core.
    private PendingRegistration()
    {
        Email = null!;
        CompanyName = null!;
        Name = null!;
        Language = null!;
        PasswordHash = null!;
        TokenHash = null!;
        CodeHash = null!;
    }

    public bool IsAbandonedAt(DateTimeOffset now) => now >= StartedAt + KeptFor;

    /// <summary>Too many codes went to this address, by resending or by registering it again.</summary>
    public bool CodeLimitReachedAt(DateTimeOffset now) => !IsAbandonedAt(now) && CodesSent >= MaxCodes;

    public bool CanResendAt(DateTimeOffset now) => now >= CodeSentAt + ResendAfter;

    /// <summary>
    /// The address is registered again: the new details and token replace the old ones and a new code is
    /// sent, so whoever holds the old token can't finish. The count of codes carries on, or the same address
    /// could be flooded by registering it over and over.
    /// </summary>
    /// <exception cref="InvalidOperationException">The limit of codes is reached.</exception>
    public void Replace(string companyName, string name, string language, string passwordHash, string token, string code,
        DateTimeOffset now)
    {
        if (CodeLimitReachedAt(now)) throw new InvalidOperationException("Too many codes were sent to this address.");
        if (IsAbandonedAt(now))
        {
            StartedAt = now;
            CodesSent = 0;
        }

        CompanyName = companyName.Trim();
        Name = name.Trim();
        Language = language;
        PasswordHash = passwordHash;
        TokenHash = HashToken(token);
        SetCode(code, now);
    }

    /// <summary>Sends a new code to the same screen: the old code stops working and the attempts start over.</summary>
    /// <exception cref="InvalidOperationException">It's too soon, or the limit of codes is reached.</exception>
    public void Resend(string code, DateTimeOffset now)
    {
        if (!CanResendAt(now) || CodeLimitReachedAt(now)) throw new InvalidOperationException("A new code can't be sent yet.");
        SetCode(code, now);
    }

    /// <summary>Checks a typed code. Wrong ones are counted, and the last allowed one uses the code up.</summary>
    public CodeCheck CheckCode(string code, DateTimeOffset now)
    {
        if (FailedAttempts >= MaxAttempts) return CodeCheck.NoAttemptsLeft;
        if (now >= CodeSentAt + CodeValidFor) return CodeCheck.Expired;

        var correct = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(HashCode(code)), Encoding.UTF8.GetBytes(CodeHash));
        if (correct) return CodeCheck.Correct;

        FailedAttempts++;
        return FailedAttempts >= MaxAttempts ? CodeCheck.NoAttemptsLeft : CodeCheck.Wrong;
    }

    /// <summary>A random code, with leading zeros kept.</summary>
    public static string NewCode() => RandomNumberGenerator.GetInt32(1_000_000).ToString($"D{CodeLength}");

    public static string HashToken(string token) => Invitation.HashToken(token);

    private void SetCode(string code, DateTimeOffset now)
    {
        CodeHash = HashCode(code);
        CodeSentAt = now;
        CodesSent++;
        FailedAttempts = 0;
    }

    // With the token's hash, so one code doesn't hash the same in two registrations.
    private string HashCode(string code) => Invitation.HashToken($"{TokenHash}:{code}");
}
