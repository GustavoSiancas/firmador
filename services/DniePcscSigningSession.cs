using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Signatures;

namespace FirmadorPades.Services;

internal sealed class DniePcscSigningSession : IExternalSignature, IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly SecureString _pin;
    private readonly Func<IDnieCardConnection> _open;
    private bool _failed;
    private bool _disposed;

    internal DniePcscSigningSession(X509Certificate2 certificate, SecureString pin, string reader)
        : this(certificate, pin, () => new DniePcscConnection(reader)) { }

    internal DniePcscSigningSession(X509Certificate2 certificate, SecureString pin, Func<IDnieCardConnection> open)
    {
        _certificate = new X509Certificate2(certificate.RawData);
        _pin = pin.Copy();
        _pin.MakeReadOnly();
        _open = open;
    }

    public string GetHashAlgorithm() => DigestAlgorithms.SHA256;
    public string GetEncryptionAlgorithm() => "RSA";

    public byte[] Sign(byte[] message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_failed) throw new CryptographicException("La sesión DNIe se detuvo. No se reintentará el PIN.");
        try
        {
            using var card = _open();
            using var current = Dnie2Protocol.ReadCertificate(card);
            // Una tarjeta cambiada/retirada nunca debe recibir el PIN del titular anterior.
            if (!current.RawData.AsSpan().SequenceEqual(_certificate.RawData))
                throw new CryptographicException("El DNIe conectado no coincide con el certificado seleccionado. Lote detenido.");
            if (DateTime.Now < current.NotBefore || DateTime.Now > current.NotAfter)
                throw new CryptographicException("El certificado del DNIe está fuera de su periodo de vigencia.");
            Dnie2Protocol.VerifyPin(card, _pin);
            byte[] signature = Dnie2Protocol.Sign(card, message);
            using var rsa = _certificate.GetRSAPublicKey();
            if (rsa is null || !rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new CryptographicException("La firma del DNIe no corresponde al certificado seleccionado.");
            return signature;
        }
        catch { _failed = true; throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pin.Dispose();
        _certificate.Dispose();
    }
}
