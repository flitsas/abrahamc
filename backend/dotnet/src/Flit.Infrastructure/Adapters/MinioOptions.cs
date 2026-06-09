namespace Flit.Infrastructure.Adapters;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Configuración MinIO S3-compatible (ADR-0016). Sección <c>MinIO</c> en appsettings.
/// </summary>
public sealed class MinioOptions
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Requiere KMS en MinIO. Desactivar en DEV local sin KMS configurado.
    /// </summary>
    public bool ServerSideEncryption { get; set; }

    public static MinioOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new MinioOptions();
        configuration.GetSection(SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Endpoint))
            options.Endpoint = configuration[$"{SectionName}:Endpoint"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.AccessKey))
            options.AccessKey = configuration[$"{SectionName}:AccessKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            options.SecretKey = configuration[$"{SectionName}:SecretKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.Region))
            options.Region = configuration[$"{SectionName}:Region"] ?? "us-east-1";

        if (bool.TryParse(configuration[$"{SectionName}:ServerSideEncryption"], out var sse))
            options.ServerSideEncryption = sse;

        return options;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
