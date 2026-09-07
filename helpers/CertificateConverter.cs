using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.X509;

namespace FirmadorPades.Helpers;

public static class CertificateConverter
{
    public static Org.BouncyCastle.X509.X509Certificate[] ToChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();

        chain.Build(certificate);

        var certificates = chain.ChainElements
            .Cast<X509ChainElement>()
            .Select(e =>
                new X509CertificateParser()
                    .ReadCertificate(e.Certificate.RawData))
            .ToArray();

        // PdfPKCS7 usa chain[0] tanto para SignerInfo como para SigningCertificateV2.
        // Un fallo de confianza/revocación de Windows no implica otro firmante,
        // pero nunca debemos continuar con una cadena vacía o un leaf diferente.
        if (certificates.Length == 0 ||
            !certificates[0].GetEncoded().AsSpan().SequenceEqual(certificate.RawData))
        {
            throw new InvalidOperationException(
                "La cadena no comienza con el certificado seleccionado para firmar.");
        }

        return certificates;
    }
}
