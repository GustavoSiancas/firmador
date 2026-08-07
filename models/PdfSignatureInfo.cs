using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

public class PdfSignatureInfo
{
    public string SignatureName { get; set; } = "";

    public string Signer { get; set; } = "";

    public string Issuer { get; set; } = "";

    public DateTime? SigningDate { get; set; }

    public bool IsValid { get; set; }

    public X509Certificate2? Certificate { get; set; }
}