namespace FirmadorPades.Models;

/// <summary>Área de firma en coordenadas PDF (puntos), con origen abajo a la izquierda.</summary>
public sealed class SignatureLocation
{
    public int Page { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}
