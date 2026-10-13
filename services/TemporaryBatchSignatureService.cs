using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public sealed record TemporarySignatureResult(string FileName, string? Error);

public sealed class TemporaryBatchSignatureService
{
    public async Task<IReadOnlyList<TemporarySignatureResult>> SignAndUploadAsync(
        IReadOnlyList<TemporarySigningDocument> documents,
        X509Certificate2 certificate,
        ApiService apiService,
        SecureString pin,
        IProgress<int>? progress = null,
        IProgress<string>? status = null)
        => await SignAsync(documents, certificate, pin,
            (document, signedPdf) => apiService.UpdateDocumentAsync(document.Id, signedPdf), progress, status);

    internal async Task<IReadOnlyList<TemporarySignatureResult>> SignAsync(
        IReadOnlyList<TemporarySigningDocument> documents,
        X509Certificate2 certificate,
        SecureString pin,
        Func<TemporarySigningDocument, byte[], Task> save,
        IProgress<int>? progress = null,
        IProgress<string>? status = null)
    {
        const string reason = "Documento firmado digitalmente";
        var results = new List<TemporarySignatureResult>(documents.Count);
        var signer = new PdfSignatureService();
        byte[] stamp = new StampService().CreateStamp(certificate.Subject, reason);

        SigningSession session;
        try { session = SigningSession.Open(certificate, pin, message => status?.Report(message)); }
        catch (Exception ex)
        {
            string error = SigningErrorMessages.GetMessage(ex);
            progress?.Report(documents.Count);
            return documents.Select(document => new TemporarySignatureResult(document.FileName, error)).ToArray();
        }
        using var sessionLifetime = session;

        for (int index = 0; index < documents.Count; index++)
        {
            TemporarySigningDocument document = documents[index];
            try
            {
                byte[] signedPdf = signer.Sign(document.PdfBytes, reason, certificate, stamp, document.SignatureLocation, session);

                await save(document, signedPdf);
                results.Add(new TemporarySignatureResult(document.FileName, null));
            }
            catch (Exception ex)
            {
                results.Add(new TemporarySignatureResult(document.FileName, SigningErrorMessages.GetMessage(ex)));

                // No reintentar un PIN inválido: puede bloquear el DNIe.
                // iText puede envolver el error original en PdfException.
                if (session.HasFailed || ex is CryptographicException)
                {
                    foreach (TemporarySigningDocument pending in documents.Skip(index + 1))
                    {
                        results.Add(new TemporarySignatureResult(pending.FileName,
                            "No procesado: se detuvo el lote. " + SigningErrorMessages.GetMessage(ex)));
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
