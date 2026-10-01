using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zalihe.Application.Common;
using Zalihe.Application.Dashboard;
using Zalihe.Application.Imports;
using Zalihe.Application.Items;
using Zalihe.Application.Stock;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;
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

        return services;
    }
}
