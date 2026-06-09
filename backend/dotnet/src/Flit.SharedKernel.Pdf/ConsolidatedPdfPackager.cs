using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Flit.SharedKernel.Pdf;

public sealed record ConsolidatedEvidenceImage(string Label, byte[] ImageBytes);

public sealed record ConsolidatedPackageInput(
    IReadOnlyList<byte[]> OrderedProcedurePdfs,
    IReadOnlyList<ConsolidatedEvidenceImage> IdentityEvidenceImages);

/// <summary>Empaqueta PDFs ordenados + evidencia de identidad (HU #9443 DOC-02).</summary>
public interface IConsolidatedPdfPackager
{
    byte[] Package(ConsolidatedPackageInput input);
}

public sealed class ConsolidatedPdfPackager : IConsolidatedPdfPackager
{
    static ConsolidatedPdfPackager() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Package(ConsolidatedPackageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parts = new List<byte[]>(input.OrderedProcedurePdfs.Count + 1);
        parts.AddRange(input.OrderedProcedurePdfs);

        if (input.IdentityEvidenceImages.Count > 0)
            parts.Add(RenderEvidenceAnnex(input.IdentityEvidenceImages));

        if (parts.Count == 0)
            throw new InvalidOperationException("No hay contenido para empaquetar.");

        return parts.Count == 1 ? parts[0] : PdfMerger.Merge(parts);
    }

    private static byte[] RenderEvidenceAnnex(IReadOnlyList<ConsolidatedEvidenceImage> images)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Evidencia de identidad validada").Bold().FontSize(16);
            });

            foreach (var image in images)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Item().Text(image.Label).SemiBold().FontSize(12);
                        col.Item().PaddingTop(8).Image(image.ImageBytes).FitArea();
                    });
                });
            }
        });

        return doc.GeneratePdf();
    }

    private static class PdfMerger
    {
        public static byte[] Merge(IReadOnlyList<byte[]> pdfs)
        {
            using var output = new PdfDocument();

            foreach (var pdfBytes in pdfs)
            {
                using var inputStream = new MemoryStream(pdfBytes);
                using var input = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);
                for (var i = 0; i < input.PageCount; i++)
                    output.AddPage(input.Pages[i]);
            }

            using var outStream = new MemoryStream();
            output.Save(outStream, false);
            return outStream.ToArray();
        }
    }
}
