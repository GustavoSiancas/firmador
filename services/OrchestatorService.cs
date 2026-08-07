using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class OrchestratorService
{
    private readonly StampService _stampService;
    private readonly PdfPreparationService _pdfPreparationService;
    private readonly PdfSignatureService _pdfSignatureService;
    private readonly ApiService _apiService;

    public OrchestratorService(
        StampService stampService,
        PdfPreparationService pdfPreparationService,
        PdfSignatureService pdfSignatureService,
        ApiService apiService)
    {
        _stampService = stampService;
        _pdfPreparationService = pdfPreparationService;
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

        // 2. Insertar sello al PDF
        byte[] pdfWithStamp = _pdfPreparationService.InsertStamp(
            documentMemory,
            stamp,
            placement);

        // 3. Firmar PDF
        byte[] signedPdf = _pdfSignatureService.Sign(
            pdfWithStamp,
            reason,
            certificate);

        // 4. Subir PDF firmado
        await _apiService.UpdateDocumentAsync(signedPdf, documentId);

        return signedPdf;
    }

}
