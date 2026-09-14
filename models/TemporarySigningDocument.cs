namespace FirmadorPades.Models;

/// <summary>Documento preparado en memoria y listo para el flujo de firma.</summary>
public sealed class TemporarySigningDocument
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = "documento.pdf";
    public byte[] PdfBytes { get; init; } = [];
    public byte[] PreviewPdfBytes { get; init; } = [];
    public bool Clean { get; init; }
    public SignatureLocation SignatureLocation { get; init; } = new();
}
