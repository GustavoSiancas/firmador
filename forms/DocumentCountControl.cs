using System.Drawing.Drawing2D;

namespace FirmadorPades.Forms;

/// <summary>Displays the number of documents received for signing.</summary>
internal sealed class DocumentCountControl : Button
{
    private int _documentCount;
    private bool _hovered;
    private bool _pressed;

    public int DocumentCount
    {
        get => _documentCount;
        set
        {
            _documentCount = Math.Max(0, value);
            AccessibleName = $"Documentos recibidos: {_documentCount}";
            Invalidate();
        }
    }

    public DocumentCountControl()
    {
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleDescription = "Abrir la vista previa del documento";
        TabStop = true;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { _pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { _pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnLostFocus(EventArgs e) { _pressed = false; Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(_pressed ? Color.FromArgb(190, 225, 230) : _hovered ? Color.FromArgb(221, 239, 243) : BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        var state = graphics.Save();
        graphics.ScaleTransform(scale, scale);
        graphics.TranslateTransform(4, _pressed ? 5 : 2);

        // Stacked sheets, following the document icon used by the application.
        using var blue = new SolidBrush(Color.FromArgb(0, 166, 215));
        using var teal = new SolidBrush(Color.FromArgb(0, 184, 199));
        using var paper = new SolidBrush(Color.FromArgb(250, 250, 250));
        using var outline = new Pen(Color.FromArgb(30, 35, 40), 2.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.FillRectangle(blue, 17, 18, 33, 43);
        graphics.FillRectangle(teal, 11, 12, 33, 43);
        graphics.FillRectangle(paper, 5, 6, 33, 43);
        graphics.DrawRectangle(outline, 5, 6, 33, 43);
        for (int y = 19; y <= 39; y += 7)
            graphics.DrawLine(outline, 12, y, 31, y);

        using var badge = new SolidBrush(Color.FromArgb(220, 38, 38));
        graphics.FillEllipse(badge, 35, 0, 28, 28);
        using var countFont = new Font("Segoe UI", _documentCount > 99 ? 8 : 11, FontStyle.Bold);
        using var centered = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(_documentCount.ToString(), countFont, Brushes.White,
            new RectangleF(35, 0, 28, 28), centered);

        graphics.Restore(state);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, ClientRectangle);
    }
}
