using System.Security.Cryptography;
using Net.Pkcs11Interop.Common;

namespace FirmadorPades.Services;

internal static class Pkcs11Fallback
{
    internal static T? OpenOrUseWindows<T>(Func<T> open, Action? onFallback = null) where T : class
    {
        try { return open(); }
        catch (Exception ex) when (IsCompatibilityError(ex))
        {
            onFallback?.Invoke();
            return null;
        }
    }

    // 
    private static bool IsCompatibilityError(Exception ex) =>
        ex is DniePkcs11IncompatibleTokenException or
        DllNotFoundException or BadImageFormatException ||
        ex is Pkcs11Exception pkcs11 && pkcs11.RV is
            CKR.CKR_TOKEN_NOT_RECOGNIZED or CKR.CKR_FUNCTION_NOT_SUPPORTED or CKR.CKR_MECHANISM_INVALID;
}

internal sealed class DniePkcs11IncompatibleTokenException(string message) : CryptographicException(message);
