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

    public X509Certificate2 GetCertificateByThumbprint(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

        store.Open(OpenFlags.ReadOnly);

        var cert = store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(c =>
                c.Thumbprint != null &&
                c.Thumbprint.Replace(" ", "")
                    .Equals(
                        thumbprint.Replace(" ", ""),
                        StringComparison.OrdinalIgnoreCase));

        if (cert == null)
            throw new Exception("Certificado no encontrado.");

        return cert;
    }

    public RSA GetPrivateKey(X509Certificate2 cert)
    {
        return cert.GetRSAPrivateKey()
            ?? throw new Exception("El certificado no tiene una clave RSA.");
    }

    public byte[] Sign(byte[] data)
    {
        var cert = GetSigningCertificate();

        using var rsa = GetPrivateKey(cert);

        return rsa.SignData(
            data,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
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

    public void PrintCertificate(X509Certificate2 cert)
    {
        Console.WriteLine($"Titular     : {GetHolderName(cert)}");
        Console.WriteLine($"Subject     : {cert.Subject}");
        Console.WriteLine($"Issuer      : {cert.Issuer}");
        Console.WriteLine($"Thumbprint  : {cert.Thumbprint}");
        Console.WriteLine($"Expira      : {cert.NotAfter}");
        Console.WriteLine($"Tiene Clave : {cert.HasPrivateKey}");
    }
}