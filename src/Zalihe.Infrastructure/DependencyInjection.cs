using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Application.Dashboard;
using Zalihe.Application.Imports;
using Zalihe.Application.Items;
using Zalihe.Application.Stock;
using Zalihe.Infrastructure.Channels.WooCommerce;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Security;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Connection string is resolved lazily so tests can override configuration.
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<AccountService>();
        services.AddScoped<ItemService>();
        services.AddScoped<ItemHistoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ItemImportService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ChannelService>();

        // Shop API keys are encrypted with Data Protection. Its key ring must persist across
        // restarts and deployments (see DataProtection:KeysPath), or stored keys can't be read.
        services.AddDataProtection().SetApplicationName("Zalihe");
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            // In production this points at a persistent volume; locally the default user profile folder is used.
            if (configuration["DataProtection:KeysPath"] is { Length: > 0 } path)
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), NullLoggerFactory.Instance);
            }
        });
        services.AddSingleton<ICredentialProtector, DataProtectionCredentialProtector>();
        services.AddHttpClient(SalesChannelFactory.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Zalihe/1.0");
        });
        services.AddSingleton<ISalesChannelFactory, SalesChannelFactory>();

        return services;
    }
}
