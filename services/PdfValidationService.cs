using System.Security.Cryptography.X509Certificates;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace FirmadorPades.Services;

public class PdfValidationService
{
    public List<PdfSignatureInfo> GetSignatures(byte[] pdfBytes)
    {
        var result = new List<PdfSignatureInfo>();

        using var stream = new MemoryStream(pdfBytes);
        using var reader = new PdfReader(stream);
        using var pdf = new PdfDocument(reader);

        var util = new SignatureUtil(pdf);

        var signatures = util.GetSignatureNames();

        int totalRevisions = signatures.Count;

        for (int index = 0; index < signatures.Count; index++)
        {
            string signatureName = signatures[index];
            PdfPKCS7 pkcs7 = util.ReadSignatureData(signatureName);

            bool valid = pkcs7.VerifySignatureIntegrityAndAuthenticity();

            var cert = new X509Certificate2(
                pkcs7.GetSigningCertificate().GetEncoded());

            result.Add(new PdfSignatureInfo
            {
                SignatureName = signatureName,

                Signer = cert.GetNameInfo(
                    X509NameType.SimpleName,
                    false),

                Subject = cert.Subject,

                Issuer = cert.Issuer,

                SigningDate = pkcs7.GetSignDate().ToUniversalTime(),

                IsValid = valid,

                // iText 7.2.5 no expone GetRevision/GetTotalRevisions. La lista
                // de SignatureUtil está ordenada por revisión de firma.
                Revision = index + 1,

                TotalRevisions = totalRevisions,

                CoversWholeDocument = util.SignatureCoversWholeDocument(signatureName),

                Algorithm = pkcs7.GetDigestAlgorithm(),

                Certificate = cert
            });
        }

        return result;
    }

}
