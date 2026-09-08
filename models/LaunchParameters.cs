namespace FirmadorPades.Models;
public class LaunchParameters
{
    public const float DefaultX = 50;
    public const float DefaultY = 20;

    public float X { get; set; } = DefaultX;
    public float Y { get; set; } = DefaultY;

    public bool Clean { get; set; } = false;

    public Uri InputEndpoint { get; set; } = null!;

    public Uri OutputEndpoint { get; set; } = null!;

    public string FileId { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}
