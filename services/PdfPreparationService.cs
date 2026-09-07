using FirmadorPades.Models;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Layout;

namespace FirmadorPades.Services;

public class PdfPreparationService
{
    public byte[] InsertStamp(byte[] pdfBytes, byte[] stampBytes, SignatureLocation placement) =>
        InsertStamp(pdfBytes, stampBytes, new[] { placement });

    /// <summary>Coloca el sello en las páginas indicadas (numeradas desde 1).
    /// Coordenadas y medidas en puntos PDF, con origen abajo a la izquierda.</summary>
    public byte[] InsertStamp(
        byte[] pdfBytes,
        byte[] stampBytes,
        IEnumerable<SignatureLocation> placements)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(stampBytes);
        ArgumentNullException.ThrowIfNull(placements);
        var locations = placements.ToArray();
        if (locations.Length == 0)
            throw new ArgumentException("Indique al menos una página de destino.", nameof(placements));

        using MemoryStream input = new(pdfBytes);
        using MemoryStream output = new();
        using PdfReader reader = new(input);
        using PdfWriter writer = new(output);
        using PdfDocument pdf = new(reader, writer, new StampingProperties().UseAppendMode());

        foreach (var placement in locations)
        {
            ArgumentNullException.ThrowIfNull(placement);
            if (placement.Page < 1 || placement.Page > pdf.GetNumberOfPages())
                throw new ArgumentOutOfRangeException(nameof(placements), "La página elegida no existe en el PDF.");
            var bounds = pdf.GetPage(placement.Page).GetPageSize();
            if (!float.IsFinite(placement.Width) || !float.IsFinite(placement.Height) ||
                placement.Width <= 0 || placement.Height <= 0 ||
                placement.Width > bounds.GetWidth() || placement.Height > bounds.GetHeight())
                throw new ArgumentOutOfRangeException(nameof(placements), "El tamaño del sello debe ser positivo y caber en la página.");
            if (!float.IsFinite(placement.X) || !float.IsFinite(placement.Y) ||
                placement.X < bounds.GetLeft() || placement.Y < bounds.GetBottom() ||
                placement.X > bounds.GetRight() - placement.Width ||
                placement.Y > bounds.GetTop() - placement.Height)
                throw new ArgumentOutOfRangeException(nameof(placements), "Las coordenadas deben mantener el sello dentro de la página.");
        }

        using Document document = new(pdf);
        ImageData imageData = ImageDataFactory.Create(stampBytes);
        foreach (var placement in locations)
        {
            var stamp = new iText.Layout.Element.Image(imageData);
            stamp.SetAutoScale(false);
            stamp.ScaleAbsolute(placement.Width, placement.Height);
            stamp.SetOpacity(0.75f);
            stamp.SetFixedPosition(placement.Page, placement.X, placement.Y);
            document.Add(stamp);
        }

        document.Close();
        return output.ToArray();
    }
}
