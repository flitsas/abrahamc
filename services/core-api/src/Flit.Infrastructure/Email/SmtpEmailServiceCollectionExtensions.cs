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
            .PostConfigure<IConfiguration>((opts, config) =>
            {
                ApplyEnvironmentOverrides(opts, config);
                ApplySmtpSectionAliases(opts, config.GetSection(SmtpOptions.SectionName));
            });

        services.AddSingleton<SmtpEmailSender>();
        services.AddScoped<SmtpOnboardingEmailNotifier>();
        services.AddScoped<NoOpOnboardingEmailNotifier>();
        services.AddScoped<IOnboardingEmailNotifier>(sp =>
        {
            var smtpOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpOptions>>().Value;
            var legacyOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpEmailOptions>>().Value;
            return smtpOpts.IsConfigured || legacyOpts.IsConfigured
                ? sp.GetRequiredService<SmtpOnboardingEmailNotifier>()
                : sp.GetRequiredService<NoOpOnboardingEmailNotifier>();
        });

        return services;
    }

    private static void ApplyEnvironmentOverrides(SmtpEmailOptions opts, IConfiguration config)
    {
        opts.Host ??= config["SMTP_HOST"];
        opts.DefaultSenderEmail ??= config["SMTP_USER"]
            ?? config["SMTP_FROM"]
            ?? config["SMTP_EMAIL_DEFAULT_SENDER_EMAIL"];
        opts.DefaultSenderPassword ??= config["SMTP_PASSWORD"]
            ?? config["SMTP_EMAIL_DEFAULT_SENDER_PASSWORD"];

        if (string.IsNullOrWhiteSpace(opts.DefaultSenderName) ||
            opts.DefaultSenderName == "FLIT Trámites")
        {
            var fromName = config["SMTP_FROM_NAME"];
            if (!string.IsNullOrWhiteSpace(fromName))
                opts.DefaultSenderName = fromName;
        }

        if (int.TryParse(config["SMTP_PORT"], out var port))
            opts.Port = port;

        if (bool.TryParse(config["SMTP_DISABLE_AUTHENTICATION"], out var noAuth))
            opts.DisableAuthentication = noAuth;

        if (bool.TryParse(config["SMTP_USE_STARTTLS"], out var tls))
            opts.UseStartTls = tls;
    }

  private static void ApplySmtpSectionAliases(SmtpEmailOptions opts, IConfigurationSection smtp)
  {
    if (string.IsNullOrWhiteSpace(opts.Host))
      opts.Host = smtp["Host"];

    if (opts.Port == 587 && int.TryParse(smtp["Port"], out var port))
      opts.Port = port;

    if (string.IsNullOrWhiteSpace(opts.DefaultSenderEmail))
      opts.DefaultSenderEmail = smtp["User"] ?? smtp["From"];

    if (string.IsNullOrWhiteSpace(opts.DefaultSenderPassword))
      opts.DefaultSenderPassword = smtp["Password"];

    if (!string.IsNullOrWhiteSpace(smtp["FromName"]))
      opts.DefaultSenderName = smtp["FromName"]!;

    if (smtp.GetValue<bool?>("EnableSsl") is true)
      opts.UseStartTls = true;
  }
}
