using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FirmadorPades.Services;

internal interface IDnieCardConnection : IDisposable
{
    byte[] Atr { get; }
    byte[] Transmit(byte[] command);
    void Reset();
    bool ResetOnDispose { get; set; }
}

// Una conexión por lectura/firma; nunca se mantiene una transacción durante una subida HTTP.
internal sealed class DniePcscConnection : IDnieCardConnection
{
    private IntPtr _context;
    private IntPtr _card;
    private uint _protocol;
    private bool _transaction;
    public byte[] Atr { get; private set; } = [];
    public bool ResetOnDispose { get; set; }

    // Consulta de presencia/contador de inserciones: no abre la tarjeta ni envía APDU.
    internal static string GetReaderSnapshot()
    {
        string[] readers = GetReaders();
        if (readers.Length == 0) return string.Empty;
        Check(SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out var context), "consultar presencia del DNIe");
        try
        {
            var states = readers.Select(reader => new ReaderState { Reader = reader, Atr = new byte[36] }).ToArray();
            Check(SCardGetStatusChange(context, 0, states, (uint)states.Length), "consultar presencia del DNIe");
            return string.Join("|", states.Select(state =>
                $"{state.Reader}:{state.EventState & 0xFFFF04FC:X8}:{Convert.ToHexString(state.Atr.AsSpan(0, (int)Math.Min(state.AtrLength, 36)))}"));
        }
        finally { SCardReleaseContext(context); }
    }

    internal static string[] GetReaders()
    {
        int result = SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out var context);
        if (result == unchecked((int)0x8010001D)) return []; // Servicio detenido.
        Check(result, "acceder al servicio de tarjetas inteligentes");
        try
        {
            uint length = 0;
            result = SCardListReaders(context, null, null, ref length);
            if (result == unchecked((int)0x8010002E)) return []; // Sin lectores.
            Check(result, "enumerar lectores");
            if (length == 0) return [];
            var names = new char[length];
            Check(SCardListReaders(context, null, names, ref length), "enumerar lectores");
            return new string(names).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        finally { SCardReleaseContext(context); }
    }

    internal DniePcscConnection(string reader)
    {
        try
        {
            Check(SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out _context), "abrir el servicio de tarjetas");
            Check(SCardConnect(_context, reader, 2, 3, out _card, out _protocol), "conectar con el DNIe");
            var atr = new byte[36];
            uint atrLength = (uint)atr.Length, nameLength = 0;
            Check(SCardStatus(_card, null, ref nameLength, out _, out _protocol, atr, ref atrLength), "consultar el DNIe");
            Atr = atr[..(int)atrLength];
        }
        catch { Dispose(); throw; }
    }

    public byte[] Transmit(byte[] command)
    {
        ObjectDisposedException.ThrowIf(_card == IntPtr.Zero, this);
        if (!_transaction)
        {
            Check(SCardBeginTransaction(_card), "reservar temporalmente el DNIe");
            _transaction = true;
        }
        var pci = new IoRequest { Protocol = _protocol, Length = 8 };
        byte[] response = new byte[4096];
        uint length = (uint)response.Length;
        Check(SCardTransmit(_card, ref pci, command, (uint)command.Length,
            IntPtr.Zero, response, ref length), "comunicarse con el DNIe");
        if (length < 2 || length > response.Length)
            throw new CryptographicException("Respuesta incompleta del DNIe.");
        return response[..(int)length];
    }

    public void Reset()
    {
        // Solo antes de enviar un PIN. Reconnect libera las transacciones de Windows.
        if (_transaction)
        {
            Check(SCardEndTransaction(_card, 0), "liberar la lectura del DNIe");
            _transaction = false;
        }
        Check(SCardReconnect(_card, 2, 3, 1, out _protocol), "reinicializar la conexión del DNIe");
    }

    public void Dispose()
    {
        if (_card != IntPtr.Zero)
        {
            uint disposition = ResetOnDispose ? 1u : 0u;
            // Reset dentro de la transacción borra la autenticación antes de ceder la tarjeta.
            bool ended = _transaction && SCardEndTransaction(_card, disposition) == 0;
            SCardDisconnect(_card, ended ? 0u : disposition);
            _card = IntPtr.Zero;
            _transaction = false;
        }
        if (_context != IntPtr.Zero)
        {
            SCardReleaseContext(_context);
            _context = IntPtr.Zero;
        }
    }

    private static void Check(int result, string operation)
    {
        if (result == 0) return;
        string detail = result switch
        {
            unchecked((int)0x8010000B) => "Otra aplicación tiene el DNIe ocupado. Cierre esa operación e inténtelo de nuevo.",
            unchecked((int)0x8010000C) or unchecked((int)0x80100069) => "Inserte el DNIe seleccionado en el lector.",
            unchecked((int)0x80100068) => "La tarjeta se reinició durante la operación. El lote se detuvo.",
            _ => "Compruebe el lector y la tarjeta."
        };
        throw new CryptographicException($"No se pudo {operation} (PC/SC 0x{result:X8}). {detail}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoRequest { public uint Protocol; public uint Length; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReaderState
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Reader;
        public IntPtr UserData;
        public uint CurrentState;
        public uint EventState;
        public uint AtrLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)] public byte[] Atr;
    }

    [DllImport("winscard.dll")] private static extern int SCardEstablishContext(uint scope, IntPtr a, IntPtr b, out IntPtr context);
    [DllImport("winscard.dll")] private static extern int SCardReleaseContext(IntPtr context);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode)] private static extern int SCardListReaders(IntPtr context, string? groups, [Out] char[]? readers, ref uint length);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode)] private static extern int SCardGetStatusChange(IntPtr context, uint timeout, [In, Out] ReaderState[] states, uint count);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode)] private static extern int SCardConnect(IntPtr context, string reader, uint share, uint protocols, out IntPtr card, out uint protocol);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode)] private static extern int SCardStatus(IntPtr card, [Out] char[]? readers, ref uint readerLength, out uint state, out uint protocol, [Out] byte[] atr, ref uint atrLength);
    [DllImport("winscard.dll")] private static extern int SCardReconnect(IntPtr card, uint share, uint protocols, uint initialization, out uint protocol);
    [DllImport("winscard.dll")] private static extern int SCardBeginTransaction(IntPtr card);
    [DllImport("winscard.dll")] private static extern int SCardEndTransaction(IntPtr card, uint disposition);
    [DllImport("winscard.dll")] private static extern int SCardDisconnect(IntPtr card, uint disposition);
    [DllImport("winscard.dll")] private static extern int SCardTransmit(IntPtr card, ref IoRequest pci, byte[] command, uint length, IntPtr receivePci, [Out] byte[] response, ref uint responseLength);
}
