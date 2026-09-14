namespace FirmadorPades.Services;

public sealed class LaunchParameterException(string message, Exception? innerException = null)
    : Exception(message, innerException);
