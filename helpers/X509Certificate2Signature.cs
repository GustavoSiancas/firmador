using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Signatures;

namespace FirmadorPades.Helpers;

public class X509Certificate2Signature : IExternalSignature
{
    private readonly X509Certificate2? _certificate;
    private readonly RSA? _sharedKey;
    private readonly Func<byte[], byte[]>? _signOperation;
    private readonly string _hashAlgorithm;

    public X509Certificate2Signature(
        X509Certificate2 certificate,
        string hashAlgorithm)
    {
        _certificate = certificate;
        _hashAlgorithm = hashAlgorithm;
    }

    internal X509Certificate2Signature(RSA sharedKey, string hashAlgorithm, Func<byte[], byte[]>? signOperation = null)
    {
        ArgumentNullException.ThrowIfNull(sharedKey);
        _sharedKey = sharedKey;
        _signOperation = signOperation;
        _hashAlgorithm = hashAlgorithm;
    }

    public string GetHashAlgorithm()
    {
        return _hashAlgorithm;
    }

    public string GetEncryptionAlgorithm()
    {
        return "RSA";
    }

    public byte[] Sign(byte[] message)
    {
        if (_signOperation is not null) return _signOperation(message);
        // La clave compartida pertenece al lote; no se libera entre documentos.
        if (_sharedKey is not null)
            return _sharedKey.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var rsa = _certificate!.GetRSAPrivateKey();

        if (rsa == null)
            throw new Exception("No se encontró la clave privada.");

        return rsa.SignData(
            message,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
