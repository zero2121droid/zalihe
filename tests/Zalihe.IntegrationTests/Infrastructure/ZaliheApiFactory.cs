using Microsoft.AspNetCore.Hosting;
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

    /// <summary>Keeps every email the app sends instead of sending it.</summary>
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Webhooks:BaseUrl", WebhookBaseUrl);
        // Scheduled jobs would sync every test's shop at random moments; tests run them directly.
        builder.UseSetting("Jobs:OrderSyncCron", "");
        builder.UseSetting("Jobs:WebhookCleanupCron", "");
        builder.UseSetting("Jobs:LowStockAlertCron", "");
        builder.UseSetting("Email:AppUrl", "https://app.zalihe.test");
        // Likewise stock pushes, which every stock change in every test would schedule.
        builder.UseSetting("Jobs:StockPushDelaySeconds", "");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(SalesChannelFactory.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Shop);
            services.AddSingleton<IEmailSender>(Emails);
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
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ZaliheApiFactory>
{
    public const string Name = "Api";
}
