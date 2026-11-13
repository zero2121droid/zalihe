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
using Zalihe.Application.Feedback;
using Zalihe.Application.Imports;
using Zalihe.Application.Items;
using Zalihe.Application.Orders;
using Zalihe.Application.Receipts;
using Zalihe.Application.Reports;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Jobs;
using Zalihe.Application.Stock;
using Zalihe.Infrastructure.Channels.WooCommerce;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Platform;
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
        services.AddScoped<PurchasePriceAccess>();
        services.AddScoped<ItemService>();
        services.AddScoped<ItemImageService>();
        services.AddScoped<ItemHistoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<StockRecalculationService>();
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
        services.AddScoped<ManualOrderService>();
        services.AddScoped<ReceiptService>();
        services.AddScoped<StockPushService>();
        services.AddScoped<LowStockAlertService>();
        services.AddScoped<ChannelFailureAlertService>();
        services.AddScoped<FeedbackService>();

        // Emails: SMTP settings from "Email" (Mailpit locally), texts from resx in the user's language.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section);
        services.AddOptions<PlatformOptions>().BindConfiguration(PlatformOptions.Section);
        services.AddScoped<PlatformStatsService>();
        services.AddScoped<PlatformFeedbackService>();
        services.AddLocalization();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<AlertEmails>();
        // Limits how often one account gets a password reset email (AccountService).
        services.AddMemoryCache();
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
        services.AddSingleton(sp =>
        {
            // WooCommerce sends the keys only to https. Usually the webhooks' address; locally it is set apart
            // (Webhooks:AuthCallbackBaseUrl), because the test shop reaches the app over https only on another port.
            var configuration = sp.GetRequiredService<IConfiguration>();
            var callbackBaseUrl = configuration["Webhooks:AuthCallbackBaseUrl"] is { Length: > 0 } url ? url : configuration["Webhooks:BaseUrl"];
            var appUrl = configuration["Email:AppUrl"];
            return Uri.TryCreate(callbackBaseUrl, UriKind.Absolute, out var callback) && callback.Scheme == Uri.UriSchemeHttps
                && Uri.TryCreate(appUrl, UriKind.Absolute, out _)
                ? new ShopAuthorizationSettings(callbackBaseUrl!, appUrl!)
                : throw new InvalidOperationException("Webhooks:BaseUrl (or Webhooks:AuthCallbackBaseUrl) must be an https address and Email:AppUrl the application's address.");
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
