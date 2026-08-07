using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Pdf.Extgstate;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class PdfPreparationService
{
    public byte[] InsertStamp(
        byte[] pdfBytes,
        byte[] stampBytes,
        SignatureLocation placement)
    {
        using MemoryStream input = new(pdfBytes);
        using MemoryStream output = new();

        using PdfReader reader = new(input);
        using PdfWriter writer = new(output);
        // También debe ser incremental: agregar el sello no puede reescribir
        // el contenido cubierto por una firma ya existente.
        using PdfDocument pdf = new(reader, writer, new StampingProperties().UseAppendMode());

        if (placement.Page < 1 || placement.Page > pdf.GetNumberOfPages())
            throw new ArgumentOutOfRangeException(nameof(placement), "La página elegida no existe en el PDF.");

        PdfPage page = pdf.GetPage(placement.Page);
        iText.Kernel.Geom.Rectangle pageSize = page.GetPageSize();

        // Documento para agregar elementos de layout
        Document document = new(pdf);

        // Imagen del sello
        ImageData imageData = ImageDataFactory.Create(stampBytes);
        iText.Layout.Element.Image stamp = new(imageData);

        // ==========================
        // Tamaño del sello
        // ==========================

        if (placement.Width <= 0 || placement.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(placement), "El tamaño de la firma debe ser mayor que cero.");

        stamp.SetAutoScale(false);
        stamp.ScaleToFit(placement.Width, placement.Height);

        // ==========================
        // Transparencia
        // ==========================

        PdfExtGState gs = new PdfExtGState()
            .SetFillOpacity(0.75f)
            .SetStrokeOpacity(0.75f);

        PdfCanvas pdfCanvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdf);
        pdfCanvas.SaveState();
        pdfCanvas.SetExtGState(gs);
        pdfCanvas.RestoreState();

        // Aplicar la transparencia a la imagen
        stamp.GetAccessibilityProperties(); // evita warning
        stamp.SetOpacity(0.75f);

        // ==========================
        // Posición
        // ==========================

        float x = placement.X;
        float y = placement.Y;
        x = Math.Clamp(x, pageSize.GetLeft(), pageSize.GetRight() - stamp.GetImageScaledWidth());
        y = Math.Clamp(y, pageSize.GetBottom(), pageSize.GetTop() - stamp.GetImageScaledHeight());

        stamp.SetFixedPosition(placement.Page, x, y);

        document.Add(stamp);

        document.Close();

        return output.ToArray();
    }
}
