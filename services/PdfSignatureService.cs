using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Helpers;
using FirmadorPades.Models;
using iText.Forms;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Signatures;
using PdfRectangle = iText.Kernel.Geom.Rectangle;

namespace FirmadorPades.Services;

public class PdfSignatureService
{
    public byte[] Sign(
        byte[] pdfBytes,
        string reason,
        X509Certificate2 certificate,
        byte[] stampBytes,
        SignatureLocation placement)
    {
        using var inputStream = new MemoryStream(pdfBytes);
        using var outputStream = new MemoryStream();
        using var reader = new PdfReader(inputStream);

        var signer = new PdfSigner(
            reader,
            outputStream,
            new StampingProperties().UseAppendMode());

        PdfDocument document = signer.GetDocument();
        if (placement.Page < 1 || placement.Page > document.GetNumberOfPages())
            throw new ArgumentOutOfRangeException(nameof(placement), "La página elegida no existe en el PDF.");
        if (placement.Width <= 0 || placement.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(placement), "El tamaño de la firma debe ser mayor que cero.");

        PdfRectangle pageSize = document.GetPage(placement.Page).GetPageSize();
        float width = Math.Min(placement.Width, pageSize.GetWidth());
        float height = Math.Min(placement.Height, pageSize.GetHeight());
        float x = Math.Clamp(placement.X, pageSize.GetLeft(), pageSize.GetRight() - width);
        float y = Math.Clamp(placement.Y, pageSize.GetBottom(), pageSize.GetTop() - height);

        signer.GetSignatureAppearance()
            .SetReason(reason)
            .SetLocation("Perú")
            .SetPageNumber(placement.Page)
            .SetPageRect(new PdfRectangle(x, y, width, height))
            .SetSignatureGraphic(ImageDataFactory.Create(stampBytes))
            .SetRenderingMode(PdfSignatureAppearance.RenderingMode.GRAPHIC);

        signer.SetFieldName(GetNextSignatureFieldName(document));

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

    private static string GetNextSignatureFieldName(PdfDocument document)
    {
        var existingNames = new HashSet<string>(
            PdfAcroForm.GetAcroForm(document, false)?.GetFormFields().Keys
                ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        for (int number = 1; ; number++)
        {
            string candidate = $"Signature{number}";
            if (!existingNames.Contains(candidate))
                return candidate;
        }
    }
}
