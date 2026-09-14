using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public sealed class TemporaryBatchSignatureService
{
    public async Task<IReadOnlyList<BatchSignatureResult>> SignAndUploadAsync(
        IReadOnlyList<TemporarySigningDocument> documents,
        X509Certificate2 certificate,
        ApiService apiService,
        SecureString pin,
        IProgress<int>? progress = null,
        IProgress<string>? status = null)
    {
        const string reason = "Documento firmado digitalmente";
        var results = new List<BatchSignatureResult>(documents.Count);
        var signer = new PdfSignatureService();
        byte[] stamp = new StampService().CreateStamp(certificate.Subject, reason);

        using var session = Pkcs11Fallback.OpenOrUseWindows(
            () => new Pkcs11SigningSession(certificate, pin),
            () =>
            {
                pin.Dispose();
                status?.Report("DNIe no compatible con PKCS#11. Windows solicitará confirmación por cada documento.");
            });

        for (int index = 0; index < documents.Count; index++)
        {
            TemporarySigningDocument document = documents[index];
            try
            {
                byte[] signedPdf = session is null
                    ? signer.Sign(document.PdfBytes, reason, certificate, stamp, document.SignatureLocation)
                    : signer.Sign(document.PdfBytes, reason, certificate, stamp, document.SignatureLocation, session);

                await apiService.UpdateDocumentAsync(document.Id, signedPdf);
                results.Add(new BatchSignatureResult(document.FileName, signedPdf, null));
            }
            catch (Exception ex)
            {
                results.Add(new BatchSignatureResult(document.FileName, null, ex.Message));

                // No reintentar un PIN inválido: puede bloquear el DNIe.
                if (ex is CryptographicException)
                {
                    foreach (TemporarySigningDocument pending in documents.Skip(index + 1))
                    {
                        results.Add(new BatchSignatureResult(pending.FileName, null,
                            "No procesado: se detuvo el lote por un error criptográfico. Revise el PIN y el DNIe."));
                    }
                    progress?.Report(results.Count);
                    return results;
                }
            }

            progress?.Report(results.Count);
        }

        return results;
    }
}
