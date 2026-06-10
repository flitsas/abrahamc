using Flit.Infrastructure.Adapters;
using Flit.Infrastructure.Email;
using Flit.Infrastructure.Repositories;
using Flit.Modules.Notifications.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flit.Infrastructure;

public static class GlobalEmailServiceCollectionExtensions
{
  public static IServiceCollection AddFlitGlobalEmail(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

    services.AddScoped<IEmailTemplateRepository, NpgsqlEmailTemplateRepository>();
    services.AddScoped<SmtpEmailService>();
    services.AddScoped<GlobalEmailService>();
    services.AddScoped<NoOpGlobalEmailService>();
    services.AddScoped<IGlobalEmailService>(sp =>
    {
      var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpOptions>>().Value;
      return opts.IsConfigured
        ? sp.GetRequiredService<GlobalEmailService>()
        : sp.GetRequiredService<NoOpGlobalEmailService>();
    });

    return services;
  }
}
