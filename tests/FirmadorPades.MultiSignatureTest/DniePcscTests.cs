using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using FirmadorPades.Services;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

internal static class DniePcscTests
{
    internal static void Run()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=PRUEBA DNIe FIR", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        using var publicCertificate = new X509Certificate2(certificate.RawData);
        using SecureString pin = new();
        foreach (char c in "123456") pin.AppendChar(c); // Solo tarjeta simulada.

        var detection = new FakeCard(certificate, key) { RecoverSelection = true };
        using (var read = Dnie2Protocol.ReadCertificate(detection))
            Require(read.RawData.SequenceEqual(certificate.RawData), "Lectura por bloques conserva exactamente el DER");
        Require(detection.Resets == 1 && detection.PinAttempts == 0 && detection.Signatures == 0,
            "Recuperación de lectura única sin PIN ni firma");
        Require(detection.BytesRead < certificate.RawData.Length + 228, "No se lee el relleno del archivo");

        var unsupported = new FakeCard(certificate, key) { Atr = [0x3B, 0] };
        ExpectFailure(() => Dnie2Protocol.ReadCertificate(unsupported), "perfil");
        Require(unsupported.Commands == 0, "No se envían comandos a tarjetas desconocidas");
        var stuck = new FakeCard(certificate, key) { AlwaysRejectSelection = true };
        ExpectFailure(() => Dnie2Protocol.ReadCertificate(stuck), "6985");
        Require(stuck.Resets == 1 && stuck.Commands == 2, "No hay bucle de reinicio");
        foreach (int mode in new[] { 1, 2, 3 })
        {
            var malformed = new FakeCard(certificate, key) { ReadFault = mode };
            ExpectFailure(() => Dnie2Protocol.ReadCertificate(malformed));
            Require(malformed.PinAttempts == 0, "Datos corruptos no llegan al PIN");
        }

        var opened = new List<FakeCard>();
        using (var session = new DniePcscSigningSession(publicCertificate, pin, () =>
        {
            var card = new FakeCard(certificate, key);
            opened.Add(card);
            return card;
        }))
        {
            byte[] message = [1, 2, 3, 4, 5];
            Require(key.VerifyData(message, session.Sign(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                "Firma del protocolo verificada con RSA");
            byte[] pdf = CreatePdf();
            byte[] stamp = new StampService().CreateStamp(certificate.Subject, "Prueba DNIe");
            for (int i = 0; i < 2; i++)
            {
                pdf = new PdfSignatureService().Sign(pdf, "Prueba DNIe", publicCertificate, stamp,
                    new SignatureLocation { Page = 1, X = 20 + 190 * i, Y = 20, Width = 170, Height = 60 }, session);
                CmsAudit.Verify(pdf, publicCertificate);
            }
            Require(new PdfValidationService().GetSignatures(pdf).All(s => s.IsValid), "Dos firmas PDF íntegras");
        }
        Require(opened.Count == 3 && opened.All(c => c.PinAttempts == 1 && c.Signatures == 1 && c.Disposed && c.ResetOnDispose),
            "Cada operación libera y desautentica la tarjeta antes de subir el PDF");
        Require(opened.All(c => c.PinCommand!.All(b => b == 0)), "Borrado del búfer del PIN");

        using var otherKey = RSA.Create(2048);
        using var otherCertificate = new CertificateRequest("CN=OTRA TARJETA", otherKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        var replaced = new FakeCard(otherCertificate, otherKey);
        using (var session = new DniePcscSigningSession(publicCertificate, pin, () => replaced))
            ExpectFailure(() => session.Sign([1]), "no coincide");
        Require(replaced.PinAttempts == 0 && replaced.Disposed, "Cambio de DNIe detectado antes de enviar el PIN");

        foreach (int pinStatus in new[] { 0x63C2, 0x6983, 0x6982 })
        {
            var rejected = new FakeCard(certificate, key) { PinStatus = pinStatus };
            using var session = new SigningSession(new DniePcscSigningSession(publicCertificate, pin, () => rejected));
            ExpectFailure(() => session.Sign([1]));
            Require(session.HasFailed, "El lote detecta el fallo aunque iText envuelva la excepción");
            ExpectFailure(() => session.Sign([1]), "No se reintentará");
            Require(rejected.PinAttempts == 1 && rejected.Signatures == 0 && rejected.Disposed,
                "PIN rechazado detiene sesión sin reintentos ni firma");
        }
        foreach (int stage in new[] { 1, 2, 3, 4 })
        {
            var failed = new FakeCard(certificate, key) { SignFault = stage };
            using var session = new DniePcscSigningSession(publicCertificate, pin, () => failed);
            ExpectFailure(() => session.Sign([1]));
            ExpectFailure(() => session.Sign([1]), "No se reintentará");
            Require(failed.PinAttempts == 1 && failed.Signatures <= 1 && failed.Disposed && failed.ResetOnDispose,
                "Fallo de comunicación/firma no repite la operación privada");
        }
        var removed = new FakeCard(certificate, key) { Removed = true };
        using (var session = new DniePcscSigningSession(publicCertificate, pin, () => removed))
            ExpectFailure(() => session.Sign([1]), "retirada");
        Require(removed.PinAttempts == 0 && removed.Disposed, "Retirada detiene antes del PIN");
        Console.WriteLine("DNIe PC/SC: lectura, recuperación, cambio/retirada de tarjeta, PIN, firma RSA y CMS/PDF correctos (tarjeta simulada).");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Prueba DNIe: " + message);
    }

    private static void ExpectFailure(Action action, string? contains = null)
    {
        try { action(); }
        catch (CryptographicException ex)
        {
            if (contains is not null) Require(ex.Message.Contains(contains), $"Mensaje esperado: {contains}; recibido: {ex.Message}");
            return;
        }
        throw new Exception("Se esperaba un rechazo criptográfico del DNIe.");
    }

    private static byte[] CreatePdf()
    {
        using var stream = new MemoryStream();
        using (var document = new Document(new PdfDocument(new PdfWriter(stream))))
            document.Add(new Paragraph("Prueba de la ruta de firma DNIe PC/SC"));
        return stream.ToArray();
    }

    private sealed class FakeCard(X509Certificate2 certificate, RSA key) : IDnieCardConnection
    {
        public byte[] Atr { get; init; } = Convert.FromHexString("3BDC18FF8191FE1FC38073C821136605036351000250");
        public bool ResetOnDispose { get; set; }
        public bool RecoverSelection { get; init; }
        public bool AlwaysRejectSelection { get; init; }
        public bool Removed { get; init; }
        public int PinStatus { get; init; } = 0x9000;
        public int ReadFault { get; init; }
        public int SignFault { get; init; }
        public int Resets { get; private set; }
        public int Commands { get; private set; }
        public int PinAttempts { get; private set; }
        public int Signatures { get; private set; }
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public byte[]? PinCommand { get; private set; }
        private byte[]? _pending;

        public void Reset() => Resets++;
        public void Dispose() => Disposed = true;

        public byte[] Transmit(byte[] command)
        {
            Require(!Disposed, "Acceso a tarjeta liberada");
            Commands++;
            if (Removed) throw new CryptographicException("Tarjeta retirada.");
            switch (command[1])
            {
                case 0xA4:
                    if (command[2] == 4)
                    {
                        Require(Convert.ToHexString(command) == "00A404000EE828BD080FD25047656E65726963", "AID exacto");
                        if (AlwaysRejectSelection || (RecoverSelection && Resets == 0)) return [0x69, 0x85];
                    }
                    else Require(Convert.ToHexString(command) == "00A4020402001D", "Certificado de firma 001D");
                    return [0x90, 0];
                case 0xB1:
                    if (ReadFault == 1) return [0x53, 0x81, 0xE4, 0x30, 0x90, 0]; // TLV truncado.
                    if (ReadFault == 2) return [0x53, 1, 0x30, 0x62, 0x82]; // DER truncado.
                    if (ReadFault == 3) return [0x69, 0x82];
                    int offset = (command[7] << 8) | command[8];
                    Require(offset == BytesRead, "Offsets sin bytes omitidos ni duplicados");
                    byte[] block = certificate.RawData.Skip(offset).Take(228).ToArray();
                    BytesRead += block.Length;
                    byte[] prefix = block.Length >= 128 ? [0x53, 0x81, (byte)block.Length] : [0x53, (byte)block.Length];
                    return prefix.Concat(block).Concat(new byte[] { 0x90, 0 }).ToArray();
                case 0x20:
                    Require(command[3] == 0x81 && command[4] == 6, "PIN de firma sin padding");
                    Require(command.AsSpan(5).SequenceEqual("123456"u8), "PIN simulado correcto");
                    PinAttempts++;
                    PinCommand = command;
                    if (SignFault == 4) throw new CryptographicException("Comunicación interrumpida durante VERIFY.");
                    return [(byte)(PinStatus >> 8), (byte)PinStatus];
                case 0x22:
                    Require(PinAttempts == 1 && PinStatus == 0x9000, "MSE solo tras PIN válido");
                    Require(Convert.ToHexString(command) == "002241B60680018A840181", "MSE de firma RSA");
                    return SignFault == 1 ? [0x69, 0x82] : [0x90, 0];
                case 0x2A:
                    Signatures++;
                    Require(command[2] == 0x9E && command[3] == 0x9A && command[4] == 51, "PSO con DigestInfo SHA256");
                    Require(command.AsSpan(5, 19).SequenceEqual(Convert.FromHexString("3031300D060960864801650304020105000420")), "DigestInfo SHA256 exacto");
                    if (SignFault == 2) throw new CryptographicException("Tarjeta retirada durante PSO.");
                    byte[] signature = key.SignHash(command[^32..], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (SignFault == 3) signature[0] ^= 1;
                    _pending = signature[128..];
                    return signature[..128].Concat(new byte[] { 0x61, 0x80 }).ToArray();
                case 0xC0:
                    Require(_pending is not null && Signatures == 1, "GET RESPONSE no repite firma");
                    return _pending!.Concat(new byte[] { 0x90, 0 }).ToArray();
                default: throw new Exception("APDU inesperado en la prueba.");
            }
        }
    }
}
