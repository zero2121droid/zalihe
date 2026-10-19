using System.Collections.Concurrent;
using Zalihe.Infrastructure.Email;

namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>Stands in for SMTP in tests: keeps the emails, shared by all tests, so tests look for their own recipients.</summary>
public class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>When set, sending throws it, like an unreachable mail server.</summary>
    public Func<EmailMessage, Exception?> Fail { get; set; } = _ => null;

    public IReadOnlyList<EmailMessage> SentTo(string email) => _sent.Where(m => m.To == email).ToList();

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Fail(message) is { } error) throw error;
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
