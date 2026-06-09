using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flit.Infrastructure.MultiTenant;

public static class MultiTenantServiceCollectionExtensions
{
    public static IServiceCollection AddFlitMultiTenant(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<TenantDbConnectionInterceptor>();
        return services;
    }

    public static DbContextOptionsBuilder UseFlitTenantConnectionInterceptor(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<TenantDbConnectionInterceptor>();
        return options.AddInterceptors(interceptor);
    }
}
