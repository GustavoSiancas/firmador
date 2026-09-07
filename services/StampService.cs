using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace FirmadorPades.Services;

[SupportedOSPlatform("windows")]
public class StampService
{
    /// <summary>Genera el sello y lo redimensiona al ancho y alto exactos, en píxeles.</summary>
    public byte[] CreateStamp(string subject, string reason, int width, int height, bool isPreview = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return ResizeStamp(CreateStamp(subject, reason, isPreview), width, height);
    }

    /// <summary>Redimensiona un sello existente a medidas exactas, en píxeles.</summary>
    public byte[] ResizeStamp(byte[] stampBytes, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stampBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using MemoryStream input = new(stampBytes);
        using Image original = Image.FromStream(input);
        using Bitmap resized = new(width, height);
        using (Graphics graphics = Graphics.FromImage(resized))
        using (ImageAttributes attributes = new())
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(original, new Rectangle(0, 0, width, height),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        }

        using MemoryStream output = new();
        resized.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    [SupportedOSPlatform("windows")]
    public byte[] CreateStamp(
        string subject,
        string reason,
        bool isPreview = false)
    {
        const int width = 870;
        const int height = 250;

        using Bitmap bitmap = new(width, height);
        using Graphics g = Graphics.FromImage(bitmap);

        g.Clear(Color.White);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // ==========================
        // Borde
        // ==========================

        using (Pen pen = new(Color.Black, 2))
        {
            g.DrawRectangle(
                pen,
                1,
                1,
                width - 2,
                height - 2);
        }

        // ==========================
        // Logo
        // ==========================

        string logoPath = Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "logo_stamp.png");

        using Image logo = Image.FromFile(logoPath);

        int maxWidth = 420;
        int maxHeight = 220;

        float ratioX = (float)maxWidth / logo.Width;
        float ratioY = (float)maxHeight / logo.Height;

        float ratio = Math.Min(ratioX, ratioY);

        int newWidth = (int)(logo.Width * ratio);
        int newHeight = (int)(logo.Height * ratio);

        g.DrawImage(
            logo,
            new Rectangle(
                10,
                10,
                newWidth,
                newHeight));

        // ==========================
        // Obtener CN
        // ==========================

        string nombre = ObtenerCN(subject);

        // ==========================
        // Hora Perú
        // ==========================

        TimeZoneInfo zonaPeru;

        try
        {
            zonaPeru = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
        catch
        {
            zonaPeru = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        }

        DateTime fecha =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                zonaPeru);

        string fechaTexto =
            fecha.ToString("dd.MM.yyyy HH:mm:ss") + " -05:00";

        // ==========================
        // Fuentes
        // ==========================

        using Font titulo =
            new("Arial", 22, FontStyle.Bold);

        using Font texto =
            new("Arial", 18, FontStyle.Regular);

        Brush brush = Brushes.Black;

        int x = 400;
        int y = 28;

        // ==========================
        // Texto
        // ==========================

        g.DrawString(
            isPreview ? "SELLO DE PRUEBA" : "Firmado digitalmente por",
            titulo,
            brush,
            x,
            y);

        y += 42;

        RectangleF rectNombre =
            new RectangleF(
                x,
                y,
                520,
                70);

        using StringFormat format = new()
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.Word,
            FormatFlags = 0
        };

        g.DrawString(
            nombre,
            texto,
            brush,
            rectNombre,
            format);

        y += 78;

        g.DrawString(
            $"Motivo: {reason}",
            texto,
            brush,
            x,
            y);

        y += 36;

        g.DrawString(
            $"Fecha: {fechaTexto}",
            texto,
            brush,
            x,
            y);

        using MemoryStream ms = new();

        bitmap.Save(ms, ImageFormat.Png);

        return ms.ToArray();
    }

    private static string ObtenerCN(string subject)
    {
        foreach (string parte in subject.Split(','))
        {
            string valor = parte.Trim();

            if (valor.StartsWith("CN=",
                StringComparison.OrdinalIgnoreCase))
            {
                return valor.Substring(3);
            }
        }

        return subject;
    }
}
