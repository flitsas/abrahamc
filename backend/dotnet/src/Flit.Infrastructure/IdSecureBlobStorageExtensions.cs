using Amazon.S3;
using Flit.Infrastructure.Adapters;
using Flit.Modules.IdentityVerification.Adapters;
using Flit.Modules.IdentityVerification.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flit.Infrastructure;

public static class IdSecureBlobStorageExtensions
{
    /// <summary>
    /// Registra <see cref="IIdSecureBlobStorage"/> con MinIO real si hay credenciales;
    /// de lo contrario usa almacenamiento in-memory (tests / fallback).
    /// </summary>
    public static IServiceCollection AddIdSecureBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var minioOptions = MinioOptions.FromConfiguration(configuration);
        var minioSectionPresent = configuration.GetSection(MinioOptions.SectionName).Exists();

        if (minioOptions.IsConfigured)
        {
            services.AddSingleton(minioOptions);
            services.AddSingleton(MinioS3ClientFactory.Create(minioOptions));
            services.AddSingleton<IIdSecureBlobStorage, MinioIdSecureBlobStorage>();
        }
        else
        {
            if (environment?.IsDevelopment() == true && minioSectionPresent)
            {
                throw new InvalidOperationException(
                    "MinIO está configurado en appsettings pero faltan Endpoint/AccessKey/SecretKey. "
                    + "IDSecure no puede usar almacenamiento in-memory en Development cuando la sección MinIO existe.");
            }

            services.AddSingleton<InMemoryIdSecureBlobStorage>();
            services.AddSingleton<IIdSecureBlobStorage>(
                sp => sp.GetRequiredService<InMemoryIdSecureBlobStorage>());
        }

        return services;
    }
}
