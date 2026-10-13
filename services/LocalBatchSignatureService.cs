using System.IO.Compression;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using iText.Kernel.Pdf;

namespace FirmadorPades.Services;

internal sealed class LocalBatchSignatureService
{
    internal static IReadOnlyList<TemporarySigningDocument> LoadDocuments(IEnumerable<string> paths, LocalSignaturePlacement? placement = null)
    {
        var documents = new List<TemporarySigningDocument>();
        foreach (string path in paths)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var input = new MemoryStream(bytes, writable: false);
                using var pdf = new PdfDocument(new PdfReader(input));
                var location = (placement ?? new LocalSignaturePlacement()).Resolve(pdf);
                documents.Add(new TemporarySigningDocument
                {
                    Id = Guid.NewGuid().ToString("N"), FileName = Path.GetFileName(path),
                    PdfBytes = bytes, SignatureLocation = location,
                    PreviewPdfBytes = new PdfSignaturePreviewService().Create(bytes, location)
                });
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"No se pudo cargar {Path.GetFileName(path)}: {ex.Message}", ex);
            }
        }
        return documents;
    }

    internal static IReadOnlyList<TemporarySigningDocument> Reposition(
        IReadOnlyList<TemporarySigningDocument> documents, LocalSignaturePlacement placement)
        => documents.Select(document =>
        {
            using var input = new MemoryStream(document.PdfBytes, writable: false);
            using var pdf = new PdfDocument(new PdfReader(input));
            var location = placement.Resolve(pdf);
            return new TemporarySigningDocument
            {
                Id = document.Id, FileName = document.FileName, PdfBytes = document.PdfBytes,
                SignatureLocation = location,
                PreviewPdfBytes = new PdfSignaturePreviewService().Create(document.PdfBytes, location)
            };
        }).ToArray();

    internal async Task<IReadOnlyList<TemporarySignatureResult>> SignToZipAsync(
        IReadOnlyList<TemporarySigningDocument> documents, X509Certificate2 certificate,
        SecureString pin, string outputPath, IProgress<int>? progress = null, IProgress<string>? status = null)
    {
        // Preparar junto al destino para publicar solo un ZIP completo, sin reemplazarlo si falla.
        string staging = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, $".{Guid.NewGuid():N}.tmp");
        try
        {
            IReadOnlyList<TemporarySignatureResult> results;
            using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                results = await new TemporaryBatchSignatureService().SignAsync(documents, certificate, pin,
                    (document, bytes) =>
                    {
                        string stem = Path.GetFileNameWithoutExtension(document.FileName);
                        string name = $"{stem}_firmado.pdf";
                        for (int suffix = 2; !names.Add(name); suffix++) name = $"{stem}_firmado_{suffix}.pdf";
                        using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                        entry.Write(bytes);
                        return Task.CompletedTask;
                    }, progress, status);
                if (results.Any(result => result.Error is not null))
                {
                    using var report = new StreamWriter(zip.CreateEntry("errores.txt").Open());
                    foreach (var result in results.Where(result => result.Error is not null))
                        report.WriteLine($"{result.FileName}: {result.Error}");
                }
            }
            File.Move(staging, outputPath, overwrite: true);
            return results;
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
