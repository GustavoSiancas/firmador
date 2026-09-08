using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Signatures;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace FirmadorPades.Services;

internal sealed class Pkcs11SigningSession : IExternalSignature, IDisposable
{
    private readonly IPkcs11Library _library;
    private ISession? _session;
    private IObjectHandle? _key;
    private bool _ownsLogin;
    private bool _alwaysAuthenticate;
    private readonly SecureString _pin;
    private readonly X509Certificate2 _certificate;

    internal static string LibraryPath => Path.Combine(
        Environment.GetFolderPath(Environment.Is64BitProcess ? Environment.SpecialFolder.ProgramFiles : Environment.SpecialFolder.ProgramFilesX86),
        "IDEMIA", "IDPlugClassic", "DLLs", "idplug-pkcs11.dll");

    internal Pkcs11SigningSession(X509Certificate2 certificate, SecureString pin)
    {
        _certificate = certificate;
        _pin = pin; // Pertenece al lote, nunca se conserva después de cerrarlo.
        var factories = new Pkcs11InteropFactories();
        _library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, LibraryPath, AppType.MultiThreaded);
        try
        {
            foreach (var slot in _library.GetSlotList(SlotsType.WithTokenPresent))
            {
                var session = slot.OpenSession(SessionType.ReadOnly);
                bool selected = false;
                try
                {
                    var id = FindCertificateId(session, certificate);
                    if (id is null) continue;
                    _session = session;
                    selected = true;
                    WithPin(bytes =>
                    {
                        try { session.Login(CKU.CKU_USER, bytes); _ownsLogin = true; }
                        catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_USER_ALREADY_LOGGED_IN) { }
                        return true;
                    });
                    var factory = session.Factories.ObjectAttributeFactory;
                    var template = new List<IObjectAttribute>
                    {
                        factory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
                        factory.Create(CKA.CKA_KEY_TYPE, CKK.CKK_RSA),
                        factory.Create(CKA.CKA_ID, id)
                    };
                    List<IObjectHandle> keys;
                    try { keys = session.FindAllObjects(template); }
                    finally { foreach (var attribute in template) attribute.Dispose(); }
                    if (keys.Count != 1) throw new CryptographicException("No se encontró una clave RSA única para el certificado seleccionado.");
                    _key = keys[0];
                    var attributes = session.GetAttributeValue(_key, new List<CKA> { CKA.CKA_ALWAYS_AUTHENTICATE });
                    try { _alwaysAuthenticate = !attributes[0].CannotBeRead && attributes[0].GetValueAsBool(); }
                    finally { foreach (var attribute in attributes) attribute.Dispose(); }
                    if (!slot.GetMechanismList().Contains(CKM.CKM_RSA_PKCS))
                        throw new CryptographicException("El DNIe no ofrece el mecanismo RSA PKCS#1 requerido.");
                    return;
                }
                finally { if (!selected) session.Dispose(); }
            }
            throw new CryptographicException("IDEMIA no encontró el certificado seleccionado en el DNIe. Conecte la tarjeta y actualice los certificados.");
        }
        catch { Dispose(); throw; }
    }

    private static byte[]? FindCertificateId(ISession session, X509Certificate2 certificate)
    {
        using var type = session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE);
        foreach (var handle in session.FindAllObjects(new List<IObjectAttribute> { type }))
        {
            var attributes = session.GetAttributeValue(handle, new List<CKA> { CKA.CKA_VALUE, CKA.CKA_ID });
            try
            {
                if (!attributes[0].CannotBeRead && !attributes[1].CannotBeRead &&
                    attributes[0].GetValueAsByteArray().AsSpan().SequenceEqual(certificate.RawData))
                    return attributes[1].GetValueAsByteArray();
            }
            finally { foreach (var attribute in attributes) attribute.Dispose(); }
        }
        return null;
    }

    internal static int CountMatchingCertificates(IEnumerable<X509Certificate2> certificates)
    {
        var factories = new Pkcs11InteropFactories();
        using var library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, LibraryPath, AppType.MultiThreaded);
        int matches = 0;
        foreach (var slot in library.GetSlotList(SlotsType.WithTokenPresent))
        {
            using var session = slot.OpenSession(SessionType.ReadOnly);
            foreach (var certificate in certificates)
                if (FindCertificateId(session, certificate) is not null) matches++;
        }
        return matches;
    }

    public string GetHashAlgorithm() => DigestAlgorithms.SHA256;
    public string GetEncryptionAlgorithm() => "RSA";

    public byte[] Sign(byte[] message)
    {
        try
        {
            // DigestInfo SHA-256 para CKM_RSA_PKCS (el token realiza el padding).
            byte[] digestInfo = Convert.FromHexString("3031300D060960864801650304020105000420")
                .Concat(SHA256.HashData(message)).ToArray();
            using var mechanism = _session!.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);
            byte[] signature = _alwaysAuthenticate
                ? WithPin(bytes => _session.Sign(mechanism, _key!, bytes, digestInfo))
                : _session.Sign(mechanism, _key!, digestInfo);
            using var publicKey = _certificate.GetRSAPublicKey();
            if (publicKey is null || !publicKey.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new CryptographicException("La firma PKCS#11 no corresponde al certificado seleccionado.");
            return signature;
        }
        catch (Pkcs11Exception ex)
        {
            throw new CryptographicException($"PKCS#11: {ex.RV}. Lote detenido sin reintentar el PIN.", ex);
        }
    }

    private T WithPin<T>(Func<byte[], T> operation)
    {
        IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(_pin);
        byte[] bytes = new byte[_pin.Length];
        try
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                char value = (char)Marshal.ReadInt16(pointer, i * 2);
                if (value > 127) throw new ArgumentException("El PIN del DNIe debe contener caracteres ASCII.");
                bytes[i] = (byte)value;
            }
            return operation(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_ownsLogin && _session is not null)
            {
                try { _session.Logout(); }
                catch (Pkcs11Exception) { /* La tarjeta puede haberse retirado. */ }
                _ownsLogin = false;
            }
        }
        finally
        {
            try { _session?.Dispose(); _session = null; }
            finally { _library.Dispose(); }
        }
    }
}
