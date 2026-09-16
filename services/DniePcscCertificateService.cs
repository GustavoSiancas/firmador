using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

internal static class DniePcscCertificateService
{
    private sealed record Source(string Reader);
    private static readonly ConditionalWeakTable<X509Certificate2, Source> Sources = new();

    internal static bool TryGetReader(X509Certificate2 certificate, out string reader)
    {
        bool found = Sources.TryGetValue(certificate, out var source);
        reader = source?.Reader ?? string.Empty;
        return found;
    }

    internal static List<X509Certificate2> GetCertificates(out string? diagnostic)
    {
        diagnostic = null;
        var certificates = new List<X509Certificate2>();
        try
        {
            foreach (string reader in DniePcscConnection.GetReaders())
            {
                try
                {
                    using var card = new DniePcscConnection(reader);
                    if (!Dnie2Protocol.Supports(card.Atr)) continue;
                    var certificate = Dnie2Protocol.ReadCertificate(card);
                    Sources.Add(certificate, new Source(reader));
                    certificates.Add(certificate);
                }
                catch (CryptographicException ex) { diagnostic = ex.Message; }
            }
        }
        catch (CryptographicException ex) { diagnostic = ex.Message; }
        return certificates;
    }
}
