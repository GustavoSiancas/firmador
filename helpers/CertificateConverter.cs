using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.X509;

namespace FirmadorPades.Helpers;

public static class CertificateConverter
{
    public static Org.BouncyCastle.X509.X509Certificate ToBouncyCastle(X509Certificate2 certificate)
    {
        return new X509CertificateParser()
            .ReadCertificate(certificate.RawData);
    }

    public static Org.BouncyCastle.X509.X509Certificate[] ToChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();

        chain.Build(certificate);

        return chain.ChainElements
            .Cast<X509ChainElement>()
            .Select(e =>
                new X509CertificateParser()
                    .ReadCertificate(e.Certificate.RawData))
            .ToArray();
    }
}