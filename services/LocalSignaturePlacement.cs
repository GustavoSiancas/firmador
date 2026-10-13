using FirmadorPades.Models;
using iText.Kernel.Pdf;

namespace FirmadorPades.Services;

internal sealed record LocalSignaturePlacement(int Column = 2, int Row = 3, bool FirstPage = false)
{
    internal SignatureLocation Resolve(PdfDocument pdf)
    {
        if (Column is < 0 or > 2 || Row is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(Column), "Seleccione una celda de la matriz 3 × 4.");
        int count = pdf.GetNumberOfPages();
        if (count == 0) throw new InvalidDataException("El PDF no contiene páginas.");
        int page = FirstPage ? 1 : count;
        var size = pdf.GetPage(page).GetPageSize();
        float width = Math.Min(170, size.GetWidth());
        float height = Math.Min(60, size.GetHeight());
        float marginX = Math.Min(20, (size.GetWidth() - width) / 2);
        float marginY = Math.Min(20, (size.GetHeight() - height) / 2);
        return new SignatureLocation
        {
            Page = page, Width = width, Height = height,
            X = size.GetLeft() + marginX + Column * (size.GetWidth() - width - 2 * marginX) / 2,
            Y = size.GetBottom() + marginY + (3 - Row) * (size.GetHeight() - height - 2 * marginY) / 3
        };
    }

    public override string ToString() => $"{(FirstPage ? "Primera" : "Última")} página · fila {Row + 1}, columna {Column + 1}";
}
