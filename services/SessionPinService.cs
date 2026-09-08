using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32.SafeHandles;

namespace FirmadorPades.Services;

internal static class SessionPinService
{
    // El llamador es dueño de la clave devuelta y debe liberarla al terminar el lote.
    internal static RSA OpenKey(X509Certificate2 certificate, SecureString? pin)
    {
        RSA key = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("No se encontró la clave privada RSA.");
        if (pin is null) return key;
        try
        {
            if (pin.Length == 0) throw new ArgumentException("Ingrese el PIN.");
            if (key is RSACng cng)
            {
                return key;
            }
            if (key is RSACryptoServiceProvider csp)
            {
                var info = csp.CspKeyContainerInfo;
                var parameters = new CspParameters(info.ProviderType, info.ProviderName, info.KeyContainerName)
                {
                    KeyNumber = (int)info.KeyNumber,
                    KeyPassword = pin,
                    Flags = CspProviderFlags.UseExistingKey | CspProviderFlags.NoPrompt |
                        (info.MachineKeyStore ? CspProviderFlags.UseMachineKeyStore : CspProviderFlags.NoFlags)
                };
                key.Dispose();
                return new RSACryptoServiceProvider(parameters);
            }
            throw new NotSupportedException("El proveedor del certificado no admite el PIN del lote.");
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    internal static byte[] SignWithPin(RSA key, SecureString pin, byte[] message)
    {
        if (key is RSACng cng)
        {
            using var handle = cng.Key.Handle;
            IntPtr buffer = Marshal.SecureStringToGlobalAllocUnicode(pin);
            try
            {
                CheckStatus(NCryptSetProperty(handle, "SmartCardPin", buffer,
                    checked((pin.Length + 1) * 2), 0x40), "configurar el PIN");
            }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(buffer); }
            return SignCngSilently(cng, message);
        }
        if (key is RSACryptoServiceProvider csp)
        {
            var info = csp.CspKeyContainerInfo;
            // CSP puede consumir la autenticación: suministrar el PIN para cada operación.
            using var operation = new RSACryptoServiceProvider(new CspParameters(
                info.ProviderType, info.ProviderName, info.KeyContainerName)
            {
                KeyNumber = (int)info.KeyNumber,
                KeyPassword = pin,
                Flags = CspProviderFlags.UseExistingKey | CspProviderFlags.NoPrompt |
                    (info.MachineKeyStore ? CspProviderFlags.UseMachineKeyStore : CspProviderFlags.NoFlags)
            });
            return operation.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        throw new CryptographicException("El proveedor no admite la firma con PIN del lote.");
    }

    internal static byte[] SignCngSilently(RSACng key, byte[] message)
    {
        using var handle = key.Key.Handle;
        byte[] hash = SHA256.HashData(message);
        byte[] signature = new byte[(key.KeySize + 7) / 8];
        var padding = new Pkcs1PaddingInfo { Algorithm = "SHA256" };
        CheckStatus(NCryptSignHash(handle, ref padding, hash, hash.Length,
            signature, signature.Length, out int written, 0x2 | 0x40), "firmar sin diálogo");
        return signature.AsSpan(0, written).ToArray();
    }

    private static void CheckStatus(int status, string operation)
    {
        if (status == 0) return;
        throw new CryptographicException($"El proveedor rechazó {operation} (0x{status:X8}). " +
            "Se detuvo el lote sin reintentar. " + new CryptographicException(status).Message);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Pkcs1PaddingInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Algorithm;
    }

    [DllImport("ncrypt.dll")]
    private static extern int NCryptSignHash(SafeNCryptKeyHandle key, ref Pkcs1PaddingInfo padding,
        byte[] hash, int hashLength, [Out] byte[] signature, int signatureLength, out int written, int flags);

    [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
    private static extern int NCryptSetProperty(SafeNCryptKeyHandle key, string property,
        IntPtr value, int valueLength, int flags);
}
