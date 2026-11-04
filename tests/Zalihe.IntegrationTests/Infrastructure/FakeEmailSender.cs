using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Zalihe.Infrastructure.Email;

namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>Stands in for SMTP in tests: keeps the emails, shared by all tests, so tests look for their own recipients.</summary>
public partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>When set, sending throws it, like an unreachable mail server.</summary>
    public Func<EmailMessage, Exception?> Fail { get; set; } = _ => null;

    /// <summary>Everything sent to the address except registration codes, which every owner gets at the start.</summary>
    public IReadOnlyList<EmailMessage> SentTo(string email) => _sent.Where(m => m.To == email && !IsCode(m)).ToList();

    public IReadOnlyList<EmailMessage> CodesTo(string email) => _sent.Where(m => m.To == email && IsCode(m)).ToList();

    /// <summary>The newest registration code sent to <paramref name="email"/>.</summary>
    public string LatestCode(string email) => CodeSubject().Match(CodesTo(email)[^1].Subject).Value;

    private static bool IsCode(EmailMessage message) => CodeSubject().IsMatch(message.Subject);

    [GeneratedRegex(@"^\d{6}\b")]
    private static partial Regex CodeSubject();

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Fail(message) is { } error) throw error;
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
