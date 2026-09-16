using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmadorPades.Services;

// Perfil DNIe Perú 2.0 comprobado con ReFirma 1.6 y la tarjeta conectada.
// No enviar este protocolo a DNIe de otras generaciones ni a tarjetas desconocidas.
internal static class Dnie2Protocol
{
    private static readonly byte[] SupportedAtr = Convert.FromHexString("3BDC18FF8191FE1FC38073C821136605036351000250");
    internal static bool Supports(byte[] atr) => atr.AsSpan().SequenceEqual(SupportedAtr);

    internal static X509Certificate2 ReadCertificate(IDnieCardConnection card)
    {
        if (!Supports(card.Atr)) throw new CryptographicException("La tarjeta no corresponde al perfil DNIe 2.0 soportado.");
        byte[] select = Convert.FromHexString("00A404000EE828BD080FD25047656E65726963");
        byte[] response = card.Transmit(select);
        if (Status(response) == 0x6985)
        {
            // Recuperación de estado observada en este DNIe, una sola vez y sin PIN.
            card.Reset();
            response = card.Transmit(select);
        }
        RequireSuccess(response, "seleccionar la aplicación del DNIe 2.0");
        RequireSuccess(card.Transmit(Convert.FromHexString("00A4020402001D")), "seleccionar el certificado de firma");

        using var output = new MemoryStream();
        int? certificateLength = null;
        while (output.Length < 16384)
        {
            int offset = (int)output.Length;
            response = card.Transmit([0, 0xB1, 0, 0, 4, 0x54, 2, (byte)(offset >> 8), (byte)offset, 0xFF]);
            int status = Status(response);
            if (status != 0x9000 && status != 0x6282)
                throw new CryptographicException($"No se pudo leer el certificado del DNIe (SW={status:X4}).");
            byte[] block = ReadDataObject(response.AsSpan(0, response.Length - 2));
            if (block.Length == 0) throw new CryptographicException("El DNIe devolvió un bloque de certificado vacío.");
            output.Write(block);
            byte[] bytes = output.ToArray();
            certificateLength ??= GetDerLength(bytes);
            if (certificateLength is int length && bytes.Length >= length)
            {
                // El archivo de la tarjeta tiene relleno después del DER; no es parte del certificado.
                var certificate = new X509Certificate2(bytes[..length]);
                using RSA? rsa = certificate.GetRSAPublicKey();
                if (rsa is not null) return certificate;
                certificate.Dispose();
                throw new CryptographicException("El certificado del DNIe no contiene una clave pública RSA.");
            }
            if (status == 0x6282) break;
        }
        throw new CryptographicException("El certificado del DNIe está truncado o excede el tamaño permitido.");
    }

    private static byte[] ReadDataObject(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || data[0] != 0x53)
            throw new CryptographicException("Formato de respuesta del certificado DNIe no reconocido.");
        int header = 2, length = data[1];
        if ((length & 0x80) != 0)
        {
            int count = length & 0x7F;
            if (count is < 1 or > 2 || data.Length < 2 + count)
                throw new CryptographicException("Longitud TLV inválida en la respuesta del DNIe.");
            length = 0;
            for (int i = 0; i < count; i++) length = (length << 8) | data[2 + i];
            header += count;
        }
        if (length != data.Length - header)
            throw new CryptographicException("Bloque de certificado DNIe incompleto.");
        return data.Slice(header, length).ToArray();
    }

    private static int? GetDerLength(byte[] data)
    {
        if (data.Length < 2) return null;
        if (data[0] != 0x30) throw new CryptographicException("El certificado no es una secuencia DER.");
        int count = (data[1] & 0x80) == 0 ? 0 : data[1] & 0x7F;
        if ((data[1] == 0x80) || count > 2) throw new CryptographicException("Longitud DER inválida.");
        if (data.Length < 2 + count) return null;
        int length = count == 0 ? data[1] : 0;
        for (int i = 0; i < count; i++) length = (length << 8) | data[2 + i];
        length += 2 + count;
        if (length > 16384) throw new CryptographicException("Certificado DNIe demasiado grande.");
        return length;
    }

    internal static void VerifyPin(IDnieCardConnection card, SecureString pin)
    {
        if (pin.Length is < 1 or > 64) throw new CryptographicException("Longitud de PIN inválida.");
        byte[] command = new byte[5 + pin.Length];
        command[1] = 0x20;
        command[3] = 0x81; // PIN de firma, no el PIN de autenticación.
        command[4] = (byte)pin.Length;
        IntPtr value = Marshal.SecureStringToGlobalAllocUnicode(pin);
        try
        {
            for (int i = 0; i < pin.Length; i++)
            {
                char character = (char)Marshal.ReadInt16(value, i * 2);
                if (character > 127) throw new CryptographicException("El PIN debe contener caracteres ASCII.");
                command[5 + i] = (byte)character;
            }
            card.ResetOnDispose = true;
            int status = Status(card.Transmit(command));
            if (status == 0x9000) return;
            if ((status & 0xFFF0) == 0x63C0)
                throw new CryptographicException($"PIN incorrecto. Intentos restantes: {status & 0xF}. Lote detenido sin reintentar.");
            if (status == 0x6983)
                throw new CryptographicException("El PIN de firma está bloqueado. Lote detenido.");
            throw new CryptographicException($"No se pudo verificar el PIN (SW={status:X4}). Lote detenido sin reintentar.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(command);
            Marshal.ZeroFreeGlobalAllocUnicode(value);
        }
    }

    internal static byte[] Sign(IDnieCardConnection card, byte[] message)
    {
        RequireSuccess(card.Transmit(Convert.FromHexString("002241B60680018A840181")), "seleccionar la clave de firma");
        byte[] digestInfo = Convert.FromHexString("3031300D060960864801650304020105000420")
            .Concat(SHA256.HashData(message)).ToArray();
        byte[] command = new byte[5 + digestInfo.Length];
        command[1] = 0x2A;
        command[2] = 0x9E;
        command[3] = 0x9A;
        command[4] = (byte)digestInfo.Length;
        digestInfo.CopyTo(command, 5);
        byte[] response = card.Transmit(command);
        using var signature = new MemoryStream();
        // GET RESPONSE recoge la salida; nunca se reenvía PSO ni VERIFY.
        for (int part = 0; part < 32; part++)
        {
            int status = Status(response);
            signature.Write(response, 0, response.Length - 2);
            if (signature.Length > 1024) throw new CryptographicException("Firma RSA demasiado grande.");
            if (status == 0x9000) return signature.ToArray();
            if ((status & 0xFF00) != 0x6100) RequireSuccess(response, "firmar con el DNIe");
            response = card.Transmit([0, 0xC0, 0, 0, (byte)status]);
        }
        throw new CryptographicException("Respuesta de firma DNIe incompleta.");
    }

    private static int Status(byte[] response) => response.Length >= 2
        ? (response[^2] << 8) | response[^1]
        : throw new CryptographicException("Respuesta DNIe incompleta.");

    private static void RequireSuccess(byte[] response, string operation)
    {
        int status = Status(response);
        if (status != 0x9000)
            throw new CryptographicException($"No se pudo {operation} (SW={status:X4}).");
    }
}
