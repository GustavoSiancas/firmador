using System.Security.Cryptography.X509Certificates;
using System.Security;
using System.Security.Cryptography;
using FirmadorPades.Helpers;
using FirmadorPades.Models;
using iText.Signatures;

namespace FirmadorPades.Services;

public record BatchSignatureResult(string FileName, byte[]? PdfBytes, string? Error);

public class BatchSignatureService
{
    public const int MaxDocuments = 20;

    public IReadOnlyList<BatchSignatureResult> Sign(
        IReadOnlyList<string> paths, X509Certificate2 certificate, bool clean,
        IProgress<int>? progress = null, SecureString? pin = null, bool usePkcs11 = false)
    {
        if (paths.Count is < 1 or > MaxDocuments)
            throw new ArgumentException("Seleccione entre 1 y 20 PDFs.", nameof(paths));

        const string reason = "Documento firmado digitalmente";
        var stamp = new StampService().CreateStamp(certificate.Subject, reason);
        var signer = new PdfSignatureService();
        var cleaner = new PdfCleaningService();
        var results = new List<BatchSignatureResult>();
        // Un único acceso a la clave durante todo el lote, en el mismo hilo.
        // using lo libera al finalizar, incluso si una operación falla.
        using var pkcs11 = usePkcs11
            ? new Pkcs11SigningSession(certificate, pin ?? throw new ArgumentException("Ingrese el PIN del lote.")) : null;
        using var key = usePkcs11 ? null : SessionPinService.OpenKey(certificate, pin);
        IExternalSignature externalSignature = pkcs11 is not null ? pkcs11 :
            new X509Certificate2Signature(key!, DigestAlgorithms.SHA256,
                pin is null ? null : message => SessionPinService.SignWithPin(key!, pin, message));
        foreach (string path in paths)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (clean) bytes = cleaner.Clean(bytes);
                byte[] signed = signer.Sign(bytes, reason, certificate, stamp,
                    new SignatureLocation { X = 50, Y = 20, Width = 170, Height = 60 }, externalSignature);
                results.Add(new(Path.GetFileName(path), signed, null));
            }
            catch (Exception ex)
            {
                results.Add(new(Path.GetFileName(path), null, ex.Message));
                // No repetir un PIN rechazado: podría agotar los intentos del DNIe.
                if (pin is not null && ex is CryptographicException)
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
