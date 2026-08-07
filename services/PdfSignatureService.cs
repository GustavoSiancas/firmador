using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Helpers;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace FirmadorPades.Services;

public class PdfSignatureService
{
    public byte[] Sign(
        byte[] pdfBytes,
        string reason,
        X509Certificate2 certificate)
    {
        using var inputStream = new MemoryStream(pdfBytes);
        using var outputStream = new MemoryStream();

        using var reader = new PdfReader(inputStream);
        using var writer = new PdfWriter(outputStream);

        var signer = new PdfSigner(
            reader,
            writer,
            // La actualización incremental conserva intacto cada rango de bytes
            // cubierto por firmas PAdES anteriores.
            new StampingProperties().UseAppendMode());

        signer.GetSignatureAppearance()
            .SetReason(reason)
            .SetLocation("Perú");

        string fieldName = $"Signature_{Guid.NewGuid():N}";
        signer.SetFieldName(fieldName);

        var externalSignature = new X509Certificate2Signature(
            certificate,
            DigestAlgorithms.SHA256);

        var chain = CertificateConverter.ToChain(certificate);

        signer.SignDetached(
            externalSignature,
            chain,
            null,
            null,
            null,
            0,
            PdfSigner.CryptoStandard.CADES);

        return outputStream.ToArray();
    }
}
