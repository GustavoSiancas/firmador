using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public record BatchSignatureResult(string FileName, byte[]? PdfBytes, string? Error);

public class BatchSignatureService
{
    public const int MaxDocuments = 20;

    public IReadOnlyList<BatchSignatureResult> Sign(
        IReadOnlyList<string> paths, X509Certificate2 certificate, bool clean,
        IProgress<int>? progress = null)
    {
        if (paths.Count is < 1 or > MaxDocuments)
            throw new ArgumentException("Seleccione entre 1 y 20 PDFs.", nameof(paths));

        const string reason = "Documento firmado digitalmente";
        var stamp = new StampService().CreateStamp(certificate.Subject, reason);
        var signer = new PdfSignatureService();
        var cleaner = new PdfCleaningService();
        var results = new List<BatchSignatureResult>();
        foreach (string path in paths)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (clean) bytes = cleaner.Clean(bytes);
                byte[] signed = signer.Sign(bytes, reason, certificate, stamp,
                    new SignatureLocation { X = 50, Y = 20, Width = 170, Height = 60 });
                results.Add(new(Path.GetFileName(path), signed, null));
            }
            catch (Exception ex)
            {
                results.Add(new(Path.GetFileName(path), null, ex.Message));
                // No repetir un PIN rechazado: podría agotar los intentos del DNIe.
                if (ex is CryptographicException)
                {
                    foreach (string pending in paths.Skip(results.Count))
                        results.Add(new(Path.GetFileName(pending), null,
                            "No procesado: se detuvo el lote por un error criptográfico. Revise el PIN y el DNIe."));
                    progress?.Report(results.Count);
                    return results;
                }
            }
            progress?.Report(results.Count);
        }
        return results;
    }
}
