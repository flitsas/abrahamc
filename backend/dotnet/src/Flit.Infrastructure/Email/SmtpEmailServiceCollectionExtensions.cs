using Flit.Infrastructure.Adapters;
using Flit.Infrastructure.Options;
using Flit.Modules.Identity.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flit.Infrastructure.Email;

public static class SmtpEmailServiceCollectionExtensions
{
    public static IServiceCollection AddFlitSmtpEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SmtpEmailOptions>()
            .Bind(configuration.GetSection(SmtpEmailOptions.SectionName))
            .PostConfigure<IConfiguration>(ApplyEnvironmentOverrides);

        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<SmtpOnboardingEmailNotifier>();
        services.AddSingleton<NoOpOnboardingEmailNotifier>();
        services.AddSingleton<IOnboardingEmailNotifier>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpEmailOptions>>().Value;
            return opts.IsConfigured
                ? sp.GetRequiredService<SmtpOnboardingEmailNotifier>()
                : sp.GetRequiredService<NoOpOnboardingEmailNotifier>();
        });

        return services;
    }

    private static void ApplyEnvironmentOverrides(SmtpEmailOptions opts, IConfiguration config)
    {
        opts.Host ??= config["SMTP_HOST"];
        opts.DefaultSenderEmail ??= config["SMTP_EMAIL_DEFAULT_SENDER_EMAIL"];
        opts.DefaultSenderPassword ??= config["SMTP_EMAIL_DEFAULT_SENDER_PASSWORD"];

        if (int.TryParse(config["SMTP_PORT"], out var port))
            opts.Port = port;

        if (bool.TryParse(config["SMTP_DISABLE_AUTHENTICATION"], out var noAuth))
            opts.DisableAuthentication = noAuth;

        if (bool.TryParse(config["SMTP_USE_STARTTLS"], out var tls))
            opts.UseStartTls = tls;
    }
}
