using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class OrchestratorService
{
    private readonly StampService _stampService;
    private readonly PdfSignatureService _pdfSignatureService;
    private readonly ApiService _apiService;

    public OrchestratorService(
        StampService stampService,
        PdfSignatureService pdfSignatureService,
        ApiService apiService)
    {
        _stampService = stampService;
        _pdfSignatureService = pdfSignatureService;
        _apiService = apiService;
    }

    public async Task<byte[]> SignDocumentAsync(
        string documentId,
        byte[] documentMemory,
        string reason,
        X509Certificate2 certificate,
        SignatureLocation placement)
    {
        // 1. Generar sello
        byte[] stamp = _stampService.CreateStamp(
            certificate.Subject,
            reason);

        // El sello es la apariencia del campo. SignDetached crea la firma
        // criptográfica y ambos se agregan en una única revisión incremental.
        byte[] signedPdf = _pdfSignatureService.Sign(
            documentMemory,
            reason,
            certificate,
            stamp,
            placement);

        // 3. Subir PDF firmado
        await _apiService.UpdateDocumentAsync(signedPdf);

        return signedPdf;
    }

}
