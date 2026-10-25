using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Zalihe.Infrastructure.Channels.WooCommerce;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>Runs the API against a real PostgreSQL in a throwaway container, shared by all tests.</summary>
public class ZaliheApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    /// <summary>The public address shops are told to send webhooks to.</summary>
    public const string WebhookBaseUrl = "https://app.zalihe.test";

    /// <summary>The fake WooCommerce shop every channel talks to in tests.</summary>
    public FakeShopHandler Shop { get; } = new();

    /// <summary>
    /// The clock that decides when a sign-in cookie's claims are rebuilt from the user (every minute); moved
    /// ahead, the next request rebuilds them, e.g. to see a changed role.
    /// </summary>
    public ShiftedTimeProvider CookieCheckClock { get; } = new();

    /// <summary>Keeps every email the app sends instead of sending it.</summary>
    public FakeEmailSender Emails { get; } = new();

    /// <summary>A stand-in for the React build, served like the real one in the container.</summary>
    public const string IndexHtml = "<!doctype html><title>Zalihe</title><div id=\"root\"></div>";
    public const string AssetPath = "/assets/index-C0ffee42.js";
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("zalihe-wwwroot-");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Webhooks:BaseUrl", WebhookBaseUrl);
        // As in production: shops send the keys of an approved connection to the webhooks' address.
        builder.UseSetting("Webhooks:AuthCallbackBaseUrl", "");
        // Scheduled jobs would sync every test's shop at random moments; tests run them directly.
        builder.UseSetting("Jobs:OrderSyncCron", "");
        builder.UseSetting("Jobs:WebhookCleanupCron", "");
        builder.UseSetting("Jobs:LowStockAlertCron", "");
        builder.UseSetting("Email:AppUrl", "https://app.zalihe.test");
        // As in production, behind Caddy.
        builder.UseSetting("ForwardedHeaders:Enabled", "true");
        File.WriteAllText(Path.Combine(_webRoot.FullName, "index.html"), IndexHtml);
        Directory.CreateDirectory(Path.Combine(_webRoot.FullName, "assets"));
        File.WriteAllText(_webRoot.FullName + AssetPath, "console.log('zalihe')");
        builder.UseWebRoot(_webRoot.FullName);
        // Likewise stock pushes, which every stock change in every test would schedule.
        builder.UseSetting("Jobs:StockPushDelaySeconds", "");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(SalesChannelFactory.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Shop);
            services.AddSingleton<IEmailSender>(Emails);
            services.Configure<SecurityStampValidatorOptions>(options => options.TimeProvider = CookieCheckClock);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        _webRoot.Delete(recursive: true);
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ZaliheApiFactory>
{
    public const string Name = "Api";
}
