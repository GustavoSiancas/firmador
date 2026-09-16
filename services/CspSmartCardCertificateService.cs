using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

// Respaldo para tarjetas publicadas por CSP. El DNIe 2.0 tiene su propia vía PC/SC.
internal static class CspSmartCardCertificateService
{
    private const uint ProvRsaFull = 1;
    private const uint CryptVerifyContext = 0xF0000000;
    private const uint PpEnumContainers = 2;
    private const uint CryptFirst = 1;
    private const uint AtKeyExchange = 1;
    private const uint AtSignature = 2;
    private const uint KpCertificate = 26;
    private const uint CryptSilent = 0x40;
    private const string Provider = "Microsoft Base Smart Card Crypto Provider";

    internal static List<X509Certificate2> GetCertificates()
    {
        var certificates = new List<X509Certificate2>();
        // Los minidrivers DNIe no siempre implementan PP_ENUMCONTAINERS.
        // Estos son los contenedores que usan las tarjetas ECEP; se complementan
        // con la enumeraciÃ³n cuando el proveedor la soporta.
        var containers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FIR_ECEP-Sig", "FIR_ECEP-Sign", "FIRMA", "SIGNATURE"
        };

        if (CryptAcquireContext(out var enumerationProvider, null, Provider, ProvRsaFull, CryptVerifyContext | CryptSilent))
        {
            try { foreach (string container in GetContainers(enumerationProvider)) containers.Add(container); }
            finally { CryptReleaseContext(enumerationProvider, 0); }
        }

        foreach (string container in containers)
        {
            if (!CryptAcquireContext(out var provider, container, Provider, ProvRsaFull, CryptSilent))
                continue;

            try
            {
                foreach (uint keyNumber in new[] { AtSignature, AtKeyExchange })
                {
                    if (!CryptGetUserKey(provider, keyNumber, out var key))
                        continue;
                    try
                    {
                        byte[]? rawCertificate = GetKeyCertificate(key);
                        if (rawCertificate is null)
                            continue;

                        var certificate = new X509Certificate2(rawCertificate);
                        try
                        {
                            // Asociar la referencia sin abrir la clave privada.
                            // Windows podrá solicitar autorización únicamente al firmar.
                            var parameters = new KeyProviderInfo
                            {
                                Container = container,
                                ProviderName = Provider,
                                ProviderType = ProvRsaFull,
                                KeySpec = keyNumber
                            };
                            if (!CertSetCertificateContextProperty(certificate.Handle, 2, 0, ref parameters))
                                throw new CryptographicException(Marshal.GetLastWin32Error());
                            certificates.Add(certificate);
                        }
                        catch (CryptographicException)
                        {
                            certificate.Dispose();
                        }
                    }
                    finally { CryptDestroyKey(key); }
                }
            }
            finally { CryptReleaseContext(provider, 0); }
        }

        return certificates;
    }

    private static IEnumerable<string> GetContainers(IntPtr provider)
    {
        uint flags = CryptFirst;
        while (true)
        {
            uint size = 0;
            if (!CryptGetProvParam(provider, PpEnumContainers, null, ref size, flags) || size == 0)
                yield break;

            byte[] value = new byte[size];
            if (!CryptGetProvParam(provider, PpEnumContainers, value, ref size, flags))
                yield break;

            string? container = System.Text.Encoding.Default.GetString(value).TrimEnd('\0');
            if (!string.IsNullOrWhiteSpace(container))
                yield return container;
            flags = 0;
        }
    }

    private static byte[]? GetKeyCertificate(IntPtr key)
    {
        uint size = 0;
        if (!CryptGetKeyParam(key, KpCertificate, null, ref size, 0) || size == 0)
            return null;
        var value = new byte[size];
        return CryptGetKeyParam(key, KpCertificate, value, ref size, 0) ? value : null;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool CryptAcquireContext(out IntPtr provider, string? container, string providerName,
        uint providerType, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CryptReleaseContext(IntPtr provider, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CryptGetProvParam(IntPtr provider, uint parameter, byte[]? data, ref uint dataLength, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CryptGetUserKey(IntPtr provider, uint keySpec, out IntPtr key);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CryptGetKeyParam(IntPtr key, uint parameter, byte[]? data, ref uint dataLength, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CryptDestroyKey(IntPtr key);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct KeyProviderInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Container;
        [MarshalAs(UnmanagedType.LPWStr)] public string ProviderName;
        public uint ProviderType;
        public uint Flags;
        public uint ParameterCount;
        public IntPtr Parameters;
        public uint KeySpec;
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CertSetCertificateContextProperty(IntPtr certificate, uint property,
        uint flags, ref KeyProviderInfo information);
}
