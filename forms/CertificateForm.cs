using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using FirmadorPades.Models;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public class CertificateForm : Form
{
    private readonly CertificateService _certificateService;
    private readonly IReadOnlyList<TemporarySigningDocument> _documents;
    private readonly ListBox _certificates;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 200 };
    private readonly List<CertificateItem> _items = [];

    public CertificateForm(
        CertificateService certificateService,
        IReadOnlyList<TemporarySigningDocument> documents)
    {
        _certificateService = certificateService;
        _documents = documents;

        Text = "Firmador CAL - Versión 1.04";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));
        Width = 560;
        Height = 470;
        BackColor = Color.FromArgb(244, 247, 251);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Shown += (_, _) => PositionOnLeftSide();

        Controls.Add(new Label
        {
            Text = "Firmador CAL - Versión 1.04", AutoSize = true,
            Location = new Point(30, 25), Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
        Controls.Add(new Label
        {
            Text = "Elija el certificado digital que utilizará para firmar.", AutoSize = true,
            Location = new Point(32, 56), Font = new Font("Segoe UI", 9)
        });

        _certificates = new ListBox
        {
            Left = 30, Top = 92, Width = ClientSize.Width - 60, Height = 245,
            Font = new Font("Segoe UI", 11), BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false,
            ItemHeight = 42, DrawMode = DrawMode.OwnerDrawFixed
        };
        _certificates.DrawItem += DrawCertificate;
        Controls.Add(_certificates);

        var documentsControl = new DocumentCountControl
        {
            Location = new Point(_certificates.Right - 75, 12), Size = new Size(75, 68),
            DocumentCount = _documents.Count
        };
        documentsControl.Click += (_, _) => ShowDocuments();
        Controls.Add(documentsControl);

        _refreshTimer.Tick += (_, _) => LoadCertificates();
        Load += (_, _) =>
        {
            LoadCertificates();
            _refreshTimer.Start();
        };
    }

    private void ShowDocuments()
    {
        using var list = new DocumentListForm(_documents, this);
        list.ShowDialog(this);
    }

    private void PositionOnLeftSide()
    {
        const int leftMargin = 80;
        var area = Screen.FromControl(this).WorkingArea;
        int y = area.Top + (area.Height - Height) / 2;
        Location = new Point(area.Left + leftMargin, Math.Max(area.Top, y));
    }

    private void LoadCertificates()
    {
        List<X509Certificate2> certificates;
        try
        {
            certificates = _certificateService.GetAllCertificates();
        }
        catch (CryptographicException)
        {
            certificates = [];
        }

        if (_items.Select(item => item.Certificate.Thumbprint)
            .SequenceEqual(certificates.Select(cert => cert.Thumbprint)))
        {
            foreach (var certificate in certificates)
                certificate.Dispose();
            return;
        }

        string? selected = (_certificates.SelectedItem as CertificateItem)?.Certificate.Thumbprint;
        _certificates.BeginUpdate();
        try
        {
            _certificates.Items.Clear();
            foreach (var item in _items)
                item.Certificate.Dispose();
            _items.Clear();

            foreach (var certificate in certificates)
            {
                var item = new CertificateItem(certificate);
                _items.Add(item);
                _certificates.Items.Add(item);
            }

            int selectedIndex = _items.FindIndex(item => item.Certificate.Thumbprint == selected);
            _certificates.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_items.Count > 0 ? 0 : -1);
        }
        finally
        {
            _certificates.EndUpdate();
        }
    }

    private void DrawCertificate(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _certificates.Items[e.Index] is not CertificateItem item)
            return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        e.Graphics.FillRectangle(selected ? Brushes.Gainsboro : Brushes.White, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _certificateService.GetHolderName(item.Certificate),
            _certificates.Font, new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
            Color.Black, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            foreach (var item in _items)
                item.Certificate.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class CertificateItem(X509Certificate2 certificate)
    {
        public X509Certificate2 Certificate { get; } = certificate;
    }
}
