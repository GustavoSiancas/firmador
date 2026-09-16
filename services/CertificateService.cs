using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

public class CertificateService
{
    public string? LastDetectionDiagnostic { get; private set; }

    public List<X509Certificate2> GetAllCertificates()
    {
        // La detección directa no depende de que los otros middleware funcionen.
        var certificates = DniePcscCertificateService.GetCertificates(out string? diagnostic);
        LastDetectionDiagnostic = diagnostic;
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            foreach (var certificate in store.Certificates)
            {
                // Listar metadatos y clave pública sin abrir claves privadas ni pedir PIN.
                if (!IsLocalDevelopmentCertificate(certificate) && HasRsaPrivateKeyReference(certificate))
                    certificates.Add(certificate);
                else certificate.Dispose();
            }
        }
        catch (CryptographicException ex) { LastDetectionDiagnostic ??= ex.Message; }

        certificates.AddRange(Pkcs11SigningSession.GetTokenCertificates());
        try { certificates.AddRange(CspSmartCardCertificateService.GetCertificates()); }
        catch (CryptographicException ex) { LastDetectionDiagnostic ??= ex.Message; }

        // PC/SC se agregó primero: conservar su procedencia si Windows tiene una copia.
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

        if (certificates.Count == 0) throw new CryptographicException("No se encontró un certificado de firma.");
        foreach (var certificate in certificates.Skip(1)) certificate.Dispose();
        // Puede ser un certificado público PC/SC: usar SigningSession para firmar.
        return certificates[0];
    }

    internal static string GetReaderSnapshot()
    {
        try { return DniePcscConnection.GetReaderSnapshot(); }
        catch (CryptographicException ex) { return ex.Message; }
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

    private static bool HasRsaPrivateKeyReference(X509Certificate2 cert)
    {
        if (!cert.HasPrivateKey)
            return false;

        try
        {
            using RSA? rsa = cert.GetRSAPublicKey();
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
