using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

public class CertificateService
{
    public List<X509Certificate2> GetAllCertificates()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

        store.Open(OpenFlags.ReadOnly);

        return store.Certificates
            .Cast<X509Certificate2>()
            .Where(cert =>
                cert.HasPrivateKey &&
                cert.Subject.Contains("FIR", StringComparison.OrdinalIgnoreCase))
            .OrderBy(cert => GetHolderName(cert))
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

}
