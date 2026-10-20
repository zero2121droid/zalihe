using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zalihe.Application.Alerts;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Application.Dashboard;
using Zalihe.Application.Imports;
using Zalihe.Application.Items;
using Zalihe.Application.Orders;
using Zalihe.Application.Reports;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Jobs;
using Zalihe.Application.Stock;
using Zalihe.Infrastructure.Channels.WooCommerce;
using Zalihe.Infrastructure.Email;
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
        services.AddScoped<InvitationService>();
        services.AddScoped<ItemService>();
        services.AddScoped<ItemImageService>();
        services.AddScoped<ItemHistoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ItemImportService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ChannelService>();
        services.AddScoped<ProductImportService>();
        services.AddScoped<OrderService>();
        services.AddScoped<WebhookService>();
        services.AddScoped<OrderSyncService>();
        services.AddScoped<OrderListService>();
        services.AddScoped<StockPushService>();
        services.AddScoped<LowStockAlertService>();
        services.AddScoped<ChannelFailureAlertService>();

        // Emails: SMTP settings from "Email" (Mailpit locally), texts from resx in the user's language.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section);
        services.AddLocalization();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<AlertEmails>();
        services.AddScoped<IAlertEmails>(sp => sp.GetRequiredService<AlertEmails>());
        services.AddSingleton<IShopOrderReader, WooCommerceOrderReader>();
        services.AddSingleton(sp =>
        {
            // Shops send webhooks to this address, so it must be reachable from the internet in production
            // (locally, from the WordPress container: http://host.docker.internal:5131).
            var baseUrl = sp.GetRequiredService<IConfiguration>()["Webhooks:BaseUrl"];
            return Uri.TryCreate(baseUrl, UriKind.Absolute, out _)
                ? new WebhookSettings(baseUrl)
                : throw new InvalidOperationException("Webhooks:BaseUrl must be set to the application's public address.");
        });

        // Background jobs in the application's own database. Long polling (LISTEN/NOTIFY) starts a new
        // job right away instead of on the next poll. The connection string is read lazily, like the DbContext's.
        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true, EnableLongPolling = true }));
        services.AddHangfireServer();
        services.AddScoped<IBackgroundJobs, HangfireBackgroundJobs>();

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
