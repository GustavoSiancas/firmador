using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Helpers;
using iText.Signatures;

namespace FirmadorPades.Services;

internal sealed class SigningSession(IExternalSignature signature) : IExternalSignature, IDisposable
{
    internal bool HasFailed { get; private set; }
    internal static SigningSession Open(X509Certificate2 certificate, SecureString pin, Action<string>? status = null)
    {
        // La procedencia PC/SC se conserva al seleccionar y deduplicar certificados.
        // Ningún fallo de PIN/lectura/firma de esta vía activa otro proveedor.
        if (DniePcscCertificateService.TryGetReader(certificate, out string reader))
        {
            status?.Invoke("Firmando con DNIe 2.0.");
            return new SigningSession(new DniePcscSigningSession(certificate, pin, reader));
        }
        var token = Pkcs11Fallback.OpenOrUseWindows(() => new Pkcs11SigningSession(certificate, pin));
        if (token is not null) return new SigningSession(token);
        if (!certificate.HasPrivateKey)
            throw new CryptographicException("No hay un proveedor disponible para firmar con el certificado seleccionado.");
        status?.Invoke("Windows solicitará la autorización del certificado para firmar.");
        return new SigningSession(new X509Certificate2Signature(certificate, DigestAlgorithms.SHA256));
    }

    public string GetHashAlgorithm() => signature.GetHashAlgorithm();
    public string GetEncryptionAlgorithm() => signature.GetEncryptionAlgorithm();
    public byte[] Sign(byte[] message)
    {
        if (HasFailed) throw new CryptographicException("La sesión de firma ya se detuvo. No se reintentará el PIN.");
        try { return signature.Sign(message); }
        catch { HasFailed = true; throw; }
    }
    public void Dispose() => (signature as IDisposable)?.Dispose();
}
