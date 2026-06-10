using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace Flit.SharedKernel.Pdf;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlitPdf(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IMarkerPdfRenderer, MarkerPdfRenderer>();
        services.AddSingleton<IConsolidatedPdfPackager, ConsolidatedPdfPackager>();
        return services;
    }
}
