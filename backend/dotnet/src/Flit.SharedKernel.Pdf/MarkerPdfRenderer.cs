using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Flit.SharedKernel.Pdf;

/// <summary>Renderiza un documento estructurado a partir de marcadores resueltos (HU #9442 DOC-01).</summary>
public interface IMarkerPdfRenderer
{
    byte[] Render(MarkerPdfDocumentModel model);
}

public sealed record MarkerPdfDocumentModel(
    string Title,
    string? Subtitle,
    IReadOnlyDictionary<string, string?> ResolvedMarkers);

public sealed class MarkerPdfRenderer : IMarkerPdfRenderer
{
    static MarkerPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(MarkerPdfDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(11));
                page.Header().Text("FLIT 2.0").SemiBold().FontSize(10).FontColor(Colors.Grey.Darken2);
                page.Content().Column(col =>
                {
                    col.Item().Text(model.Title).Bold().FontSize(16);
                    if (!string.IsNullOrWhiteSpace(model.Subtitle))
                        col.Item().PaddingTop(6).Text(model.Subtitle!).FontSize(12).Italic();
                    col.Item().PaddingTop(16).LineHorizontal(1);
                    foreach (var (key, value) in model.ResolvedMarkers.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.ConstantItem(140).Text(key).SemiBold();
                            row.RelativeItem().Text(value ?? "—");
                        });
                    }
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generado ");
                    t.Span(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC").Italic();
                });
            });
        });
        return doc.GeneratePdf();
    }
}
