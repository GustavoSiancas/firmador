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

        foreach (string signatureName in signatures)
        {
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

                Issuer = cert.Issuer,

                SigningDate = pkcs7.GetSignDate().ToUniversalTime(),

                IsValid = valid,

                Certificate = cert
            });
        }

        return result;
    }

    public bool Validate(byte[] pdfBytes)
    {
        var signatures = GetSignatures(pdfBytes);

        if (signatures.Count == 0)
            return false;

        return signatures.All(x => x.IsValid);
    }
}