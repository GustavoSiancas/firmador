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
        return Sign(pdfBytes, reason, certificate, stampBytes, placement,
            new X509Certificate2Signature(certificate, DigestAlgorithms.SHA256));
    }

    internal byte[] Sign(
        byte[] pdfBytes,
        string reason,
        X509Certificate2 certificate,
        byte[] stampBytes,
        SignatureLocation placement,
        IExternalSignature externalSignature)
    {
        using var inputStream = new MemoryStream(pdfBytes);
        using var outputStream = new MemoryStream();
        using var reader = new PdfReader(inputStream);

        var signer = new PdfSigner(
            reader,
            outputStream,
            new StampingProperties().UseAppendMode());

        PdfDocument document = signer.GetDocument();
        int page = document.GetNumberOfPages();
        if (placement.Width <= 0 || placement.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(placement), "El tamaño de la firma debe ser mayor que cero.");

        PdfRectangle pageSize = document.GetPage(page).GetPageSize();
        float width = Math.Min(placement.Width, pageSize.GetWidth());
        float height = Math.Min(placement.Height, pageSize.GetHeight());
        float x = Math.Clamp(placement.X, pageSize.GetLeft(), pageSize.GetRight() - width);
        float y = Math.Clamp(placement.Y, pageSize.GetBottom(), pageSize.GetTop() - height);

        signer.GetSignatureAppearance()
            .SetReason(reason)
            .SetLocation("Perú")
            .SetPageNumber(page)
            .SetPageRect(new PdfRectangle(x, y, width, height))
            .SetSignatureGraphic(ImageDataFactory.Create(stampBytes))
            .SetRenderingMode(PdfSignatureAppearance.RenderingMode.GRAPHIC);

        signer.SetFieldName(GetNextSignatureFieldName(document));

        var chain = CertificateConverter.ToChain(certificate);

        signer.GetSignatureAppearance().SetCertificate(chain[0]);
        // Declarar la extensión antes de la primera firma. No introducir ni
        // actualizar /Extensions en revisiones que ya contienen firmas.
        if (new SignatureUtil(document).GetSignatureNames().Count == 0)
            document.GetCatalog().AddDeveloperExtension(PdfDeveloperExtension.ESIC_1_7_EXTENSIONLEVEL2);
        // Reserva para la cadena completa, atributos ESS y firma RSA.
        int estimatedSize = checked(chain.Sum(c => c.GetEncoded().Length) + 8192);
        signer.SignExternalContainer(new CadesSignatureContainer(externalSignature, chain), estimatedSize);

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
