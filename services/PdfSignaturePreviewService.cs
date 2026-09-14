using FirmadorPades.Models;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using PdfRectangle = iText.Kernel.Geom.Rectangle;

namespace FirmadorPades.Services;

/// <summary>Crea una copia visual del PDF; no altera los bytes que se firmarán.</summary>
public sealed class PdfSignaturePreviewService
{
    public byte[] Create(byte[] pdfBytes, SignatureLocation location)
    {
        using var input = new MemoryStream(pdfBytes, writable: false);
        using var output = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(input), new PdfWriter(output)))
        {
            var rectangle = new PdfRectangle(location.X, location.Y, location.Width, location.Height);
            var canvas = new PdfCanvas(pdf.GetPage(location.Page));
            canvas.SetStrokeColor(ColorConstants.RED)
                .SetLineWidth(2)
                .Rectangle(rectangle)
                .Stroke();

            using var layout = new Canvas(canvas, rectangle);
            layout.SetFontColor(ColorConstants.RED)
                .Add(new Paragraph("Firma del usuario X")
                    .SetFontSize(10)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                    .SetMarginTop(location.Height / 2 - 12));
        }

        return output.ToArray();
    }
}
