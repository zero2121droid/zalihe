namespace Zalihe.Infrastructure.Email;

public enum SmtpSecurity
{
    /// <summary>Plain connection, for a local catcher such as Mailpit.</summary>
    None,

    /// <summary>Connects in plain text, then switches to TLS (port 587).</summary>
    StartTls,

    /// <summary>TLS from the start (port 465).</summary>
    SslOnConnect,
}

/// <summary>The "Email" configuration section. Locally Mailpit (appsettings.Development.json); in production a real SMTP server.</summary>
public class EmailOptions
{
    public const string Section = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>The sender's address, e.g. upozorenja@zalihe.rs.</summary>
    public string? From { get; set; }

    public string FromName { get; set; } = "Zalihe";

    /// <summary>The web app's public address, for links in emails, e.g. https://app.zalihe.rs.</summary>
    public string? AppUrl { get; set; }
}
