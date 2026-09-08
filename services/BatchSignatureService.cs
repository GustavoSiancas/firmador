using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Security;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public record BatchSignatureResult(string FileName, byte[]? PdfBytes, string? Error);

public class BatchSignatureService
{
    public const int MaxDocuments = 20;

    public IReadOnlyList<BatchSignatureResult> Sign(
        IReadOnlyList<string> paths, X509Certificate2 certificate, bool clean,
        IProgress<int>? progress = null, SecureString? pin = null,
        IProgress<string>? status = null)
    {
        if (paths.Count is < 1 or > MaxDocuments)
            throw new ArgumentException("Seleccione entre 1 y 20 PDFs.", nameof(paths));

        const string reason = "Documento firmado digitalmente";
        var stamp = new StampService().CreateStamp(certificate.Subject, reason);
        var signer = new PdfSignatureService();
        var cleaner = new PdfCleaningService();
        var results = new List<BatchSignatureResult>();
        using var session = pin is null ? null : Pkcs11Fallback.OpenOrUseWindows(
            () => new Pkcs11SigningSession(certificate, pin),
            () =>
            {
                // Este PIN ya no se utiliza: Windows obtiene su propia autenticación.
                pin.Dispose();
                status?.Report("DNIe no compatible con PKCS#11. Firme cada PDF en Seguridad de Windows.");
            });
        foreach (string path in paths)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (clean) bytes = cleaner.Clean(bytes);
                var placement = new SignatureLocation { X = 50, Y = 20, Width = 170, Height = 60 };
                byte[] signed = session is null
                    ? signer.Sign(bytes, reason, certificate, stamp, placement)
                    : signer.Sign(bytes, reason, certificate, stamp, placement, session);
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
