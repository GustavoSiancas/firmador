using System.Security.Cryptography;
using Net.Pkcs11Interop.Common;

namespace FirmadorPades.Services;

internal static class SigningErrorMessages
{
    internal static string GetMessage(Exception exception)
    {
        // iText envuelve los errores del proveedor: buscar primero su causa real.
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is Pkcs11Exception pkcs11)
                return pkcs11.RV switch
                {
                    CKR.CKR_PIN_INCORRECT => "La clave (PIN) es incorrecta. Revísela e intente nuevamente. No se reintentó automáticamente.",
                    CKR.CKR_PIN_INVALID => "La clave (PIN) contiene caracteres no válidos. Revísela e intente nuevamente.",
                    CKR.CKR_PIN_LEN_RANGE => "La longitud de la clave (PIN) no es válida. Revísela e intente nuevamente.",
                    CKR.CKR_PIN_LOCKED => "La clave (PIN) está bloqueada. Debe desbloquearla antes de volver a firmar.",
                    CKR.CKR_PIN_EXPIRED => "La clave (PIN) ha vencido. Debe cambiarla antes de volver a firmar.",
                    CKR.CKR_CANCEL or CKR.CKR_FUNCTION_CANCELED => "Se canceló la introducción de la clave (PIN). No se completó la firma.",
                    CKR.CKR_TOKEN_NOT_PRESENT or CKR.CKR_DEVICE_REMOVED => "No se encuentra el DNIe. Insértelo en el lector y vuelva a intentarlo.",
                    CKR.CKR_USER_NOT_LOGGED_IN => "No se pudo autorizar la firma con la clave (PIN). Vuelva a introducirla.",
                    _ => "No se pudo firmar con el DNIe. Compruebe la tarjeta y el lector y vuelva a intentarlo."
                };
            string? windowsMessage = unchecked((uint)cause.HResult) switch
            {
                0x8010006B => "La clave (PIN) es incorrecta. Revísela e intente nuevamente. No se reintentó automáticamente.",
                0x8010006C => "La clave (PIN) está bloqueada. Debe desbloquearla antes de volver a firmar.",
                0x8010006E or 0x80090036 or 0x800704C7 => "Se canceló la introducción de la clave (PIN). No se completó la firma.",
                0x8010000C or 0x80100069 => "No se encuentra el DNIe. Insértelo en el lector y vuelva a intentarlo.",
                _ => null
            };
            if (windowsMessage is not null) return windowsMessage;
        }
        // Conservar mensajes específicos de PC/SC, sin mostrar la envoltura inglesa de iText.
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
            if (cause is CryptographicException && cause.Message.Contains("PIN", StringComparison.OrdinalIgnoreCase))
                return cause.Message;
        Exception root = exception;
        while (root.InnerException is not null) root = root.InnerException;
        if (root.Message.Contains("CKR_", StringComparison.OrdinalIgnoreCase) ||
            root.Message.Contains("sign", StringComparison.OrdinalIgnoreCase) && root.GetType().Namespace?.StartsWith("iText", StringComparison.Ordinal) == true)
            return "No se pudo completar la firma. Compruebe el certificado, el DNIe y el lector y vuelva a intentarlo.";
        return root.Message;
    }
}
