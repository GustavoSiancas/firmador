using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Signatures;

namespace FirmadorPades.Helpers;

public class X509Certificate2Signature : IExternalSignature
{
    private readonly X509Certificate2 _certificate;
    private readonly string _hashAlgorithm;

    public X509Certificate2Signature(
        X509Certificate2 certificate,
        string hashAlgorithm)
    {
        _certificate = certificate;
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
        using var rsa = _certificate.GetRSAPrivateKey();

        if (rsa == null)
            throw new Exception("No se encontró la clave privada.");

        return rsa.SignData(
            message,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}