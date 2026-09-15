using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

public class CertificateService
{
    public List<X509Certificate2> GetAllCertificates()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

        store.Open(OpenFlags.ReadOnly);

        var certificates = store.Certificates
            .Cast<X509Certificate2>()
            // Los certificados renovados de DNIe 1.0/2.0 pueden no incluir
            // "FIR" en el Subject. Windows informa la disponibilidad real de
            // la clave mediante el CSP/KSP del middleware instalado.
            .Where(HasUsableRsaPrivateKey)
            .Where(cert => !IsLocalDevelopmentCertificate(cert))
            .ToList();

        certificates.AddRange(Pkcs11SigningSession.GetTokenCertificates());
        certificates.AddRange(CspSmartCardCertificateService.GetCertificates());

        var uniqueCertificates = new List<X509Certificate2>();
        foreach (var group in certificates.GroupBy(cert => Convert.ToHexString(cert.RawData), StringComparer.Ordinal))
        {
            uniqueCertificates.Add(group.First());
            foreach (var duplicate in group.Skip(1))
                duplicate.Dispose();
        }

        return uniqueCertificates
            .OrderByDescending(IsExplicitSigningCertificate)
            .ThenBy(GetHolderName)
            .ToList();
    }

    public X509Certificate2 GetSigningCertificate()
    {
        var certificates = GetAllCertificates();

        foreach (var cert in certificates)
        {
            try
            {
                using var rsa = cert.GetRSAPrivateKey();

                if (rsa != null)
                    return cert;
            }
            catch
            {
                // Ignorar certificados inválidos
            }
        }

        throw new Exception("No se encontró un certificado de firma.");
    }

    public string GetHolderName(X509Certificate2 cert)
    {
        var simpleName = cert.GetNameInfo(
            X509NameType.SimpleName,
            false);

        int index = simpleName.IndexOf(
            " FIR",
            StringComparison.OrdinalIgnoreCase);

        if (index > 0)
            return simpleName[..index].Trim();

        return simpleName;
    }

    public string GetCertificatePurpose(X509Certificate2 cert) =>
        IsExplicitSigningCertificate(cert) ? "Certificado de firma" : "Certificado RSA";

    private static bool HasUsableRsaPrivateKey(X509Certificate2 cert)
    {
        if (!cert.HasPrivateKey)
            return false;

        try
        {
            using RSA? rsa = cert.GetRSAPrivateKey();
            return rsa is not null;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool IsExplicitSigningCertificate(X509Certificate2 cert) =>
        cert.Extensions
            .OfType<X509KeyUsageExtension>()
            .Any(extension => extension.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation)) ||
        cert.Subject.Contains("FIR", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalDevelopmentCertificate(X509Certificate2 cert) =>
        cert.Subject.Contains("CN=localhost", StringComparison.OrdinalIgnoreCase);

}
