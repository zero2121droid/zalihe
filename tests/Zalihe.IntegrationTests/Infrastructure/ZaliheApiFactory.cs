using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Zalihe.Infrastructure.Channels.WooCommerce;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>Runs the API against a real PostgreSQL in a throwaway container, shared by all tests.</summary>
public class ZaliheApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    /// <summary>The fake WooCommerce shop every channel talks to in tests.</summary>
    public FakeShopHandler Shop { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.ConfigureTestServices(services =>
            services.AddHttpClient(SalesChannelFactory.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Shop));
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
