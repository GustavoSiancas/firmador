using Microsoft.Web.WebView2.WinForms;

namespace FirmadorPades.Forms;

partial class SignaturePreviewForm
{
    private WebView2 pdfViewer = null!;
    private Button btnSelectLocation = null!;
    private Button btnDisableSelection = null!;
    private Button btnAccept = null!;
    private Button btnCancel = null!;
    private Button btnZoomIn = null!;
    private Button btnZoomOut = null!;
    private NumericUpDown nudPage = null!;
    private Label lblStatus = null!;

    private void InitializeComponent()
    {
        pdfViewer = new WebView2 { Dock = DockStyle.Fill };
        btnSelectLocation = CreateButton("Seleccionar ubicación", Color.FromArgb(22, 119, 160), Color.White);
        btnDisableSelection = CreateButton("Navegar", Color.White, Color.FromArgb(16, 44, 84));
        btnAccept = CreateButton("Confirmar ubicación", Color.FromArgb(20, 125, 106), Color.White);
        btnCancel = CreateButton("Cancelar", Color.FromArgb(244, 247, 251), Color.FromArgb(91, 108, 128));
        btnZoomIn = CreateButton("Zoom +", Color.White, Color.FromArgb(16, 44, 84));
        btnZoomOut = CreateButton("Zoom −", Color.White, Color.FromArgb(16, 44, 84));
        nudPage = new NumericUpDown { Minimum = 1, Width = 68, Font = new Font("Segoe UI", 9) };
        lblStatus = new Label { AutoSize = true, Padding = new Padding(12, 10, 12, 0), Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(58, 78, 102) };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(16, 10, 16, 9), WrapContents = false, BackColor = Color.FromArgb(244, 247, 251) };
        bar.Controls.Add(new Label { Text = "Página", AutoSize = true, Padding = new Padding(0, 10, 5, 0), ForeColor = Color.FromArgb(16, 44, 84), Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        bar.Controls.Add(nudPage);
        bar.Controls.Add(btnSelectLocation);
        bar.Controls.Add(btnDisableSelection);
        bar.Controls.Add(btnZoomOut);
        bar.Controls.Add(btnZoomIn);
        bar.Controls.Add(lblStatus);
        bar.Controls.Add(btnAccept);
        bar.Controls.Add(btnCancel);
        Controls.Add(pdfViewer);
        Controls.Add(bar);
        BackColor = Color.FromArgb(244, 247, 251);
        ClientSize = new Size(1180, 790);
        StartPosition = FormStartPosition.CenterParent;
        Text = "Previsualización de firma";
    }

    private static Button CreateButton(string text, Color background, Color foreground) => new()
    {
        Text = text,
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        BackColor = background,
        ForeColor = foreground,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        Margin = new Padding(5, 1, 0, 1)
    };
}
