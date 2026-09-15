using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Security;
using FirmadorPades.Models;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public class CertificateForm : Form
{
    private static readonly Color BrandTeal = Color.FromArgb(20, 125, 106);
    private static readonly Color BrandTealDark = Color.FromArgb(13, 93, 79);
    private static readonly Color Surface = Color.FromArgb(244, 247, 251);
    private readonly CertificateService _certificateService;
    private readonly ApiService _apiService;
    private readonly IReadOnlyList<TemporarySigningDocument> _documents;
    private readonly ListBox _certificates;
    private readonly Button _sign = new();
    private readonly Button _refresh = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 200 };
    private readonly List<CertificateItem> _items = [];
    private bool _signing;

    public CertificateForm(
        CertificateService certificateService,
        ApiService apiService,
        IReadOnlyList<TemporarySigningDocument> documents)
    {
        _certificateService = certificateService;
        _apiService = apiService;
        _documents = documents;

        Text = "Firmador CAL - Versión 1.11";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));
        ClientSize = new Size(560, 390);
        BackColor = Surface;
        Font = new Font("Segoe UI", 9);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Shown += (_, _) => PositionOnLeftSide();

        // Cabecera y superficie usan los mismos tonos de la identidad visual.
        var header = new Panel
        {
            BackColor = BrandTeal,
            Location = Point.Empty,
            Size = new Size(ClientSize.Width, 82)
        };
        header.Controls.Add(new Label
        {
            Text = "Firmador CAL", AutoSize = true,
            Location = new Point(30, 16), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White
        });
        header.Controls.Add(new Label
        {
            Text = "Elija el certificado digital que utilizará para firmar.", AutoSize = true,
            Location = new Point(32, 48), Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(222, 244, 240)
        });
        Controls.Add(header);

        _certificates = new ListBox
        {
            Left = 30, Top = 100, Width = ClientSize.Width - 60, Height = 166,
            Font = new Font("Segoe UI", 11), BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false,
            ItemHeight = 42, DrawMode = DrawMode.OwnerDrawFixed
        };
        _certificates.DrawItem += DrawCertificate;
        _certificates.SelectedIndexChanged += (_, _) => UpdateSignButton();
        Controls.Add(_certificates);

        var documentsControl = new DocumentCountControl
        {
            Location = new Point(header.ClientSize.Width - 95, 7), Size = new Size(75, 68),
            BackColor = BrandTeal,
            DocumentCount = _documents.Count
        };
        documentsControl.Click += (_, _) => ShowDocuments();
        header.Controls.Add(documentsControl);

        _status.Location = new Point(30, _certificates.Bottom + 67);
        _status.AutoSize = true;
        _status.Text = $"{_documents.Count} documentos listos para firmar.";
        _status.ForeColor = Color.FromArgb(84, 97, 110);
        _status.Font = new Font("Segoe UI", 9);
        Controls.Add(_status);

        _sign.Text = "Firmar";
        _sign.Size = new Size(180, 42);
        _sign.Location = new Point(_certificates.Right - _sign.Width, _certificates.Bottom + 12);
        _sign.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _sign.FlatStyle = FlatStyle.Flat;
        _sign.FlatAppearance.BorderSize = 0;
        _sign.BackColor = BrandTeal;
        _sign.FlatAppearance.MouseOverBackColor = BrandTealDark;
        _sign.FlatAppearance.MouseDownBackColor = BrandTealDark;
        _sign.ForeColor = Color.White;
        _sign.Click += SignDocuments;
        Controls.Add(_sign);

        _refresh.Text = "Actualizar certificados";
        _refresh.Size = new Size(180, 42);
        _refresh.Location = new Point(30, _certificates.Bottom + 12);
        _refresh.FlatStyle = FlatStyle.Flat;
        _refresh.FlatAppearance.BorderColor = BrandTeal;
        _refresh.ForeColor = BrandTeal;
        _refresh.BackColor = Color.White;
        _refresh.Click += (_, _) => LoadCertificates();
        Controls.Add(_refresh);

        _refreshTimer.Tick += (_, _) => LoadCertificates();
        Load += (_, _) =>
        {
            LoadCertificates();
            _refreshTimer.Start();
            UpdateSignButton();
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
            if (_items.Count == 0 && !_signing)
                _status.Text = "Windows no expone un certificado RSA con clave privada. Verifique el middleware, lector y DNIe.";
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
            if (_items.Count == 0 && !_signing)
                _status.Text = "Windows no expone un certificado RSA con clave privada. Verifique el middleware, lector y DNIe.";
            UpdateSignButton();
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
        using var selectedBrush = new SolidBrush(Color.FromArgb(225, 243, 239));
        e.Graphics.FillRectangle(selected ? selectedBrush : Brushes.White, e.Bounds);
        string label = $"{_certificateService.GetHolderName(item.Certificate)} — {_certificateService.GetCertificatePurpose(item.Certificate)}";
        TextRenderer.DrawText(e.Graphics, label,
            _certificates.Font, new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
            Color.FromArgb(32, 43, 54), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    private void UpdateSignButton() =>
        _sign.Enabled = !_signing && _documents.Count > 0 && _certificates.SelectedItem is CertificateItem;

    private async void SignDocuments(object? sender, EventArgs e)
    {
        if (_signing || _certificates.SelectedItem is not CertificateItem item)
            return;

        SecureString pin;
        using (var pinForm = new BatchPinForm(_documents.Count))
        {
            if (pinForm.ShowDialog(this) != DialogResult.OK)
                return;
            pin = pinForm.CopyPin();
        }

        _signing = true;
        _refreshTimer.Stop();
        _certificates.Enabled = false;
        _refresh.Enabled = false;
        _sign.Enabled = false;
        _status.Text = $"Firmando 0 / {_documents.Count}...";
        var progress = new Progress<int>(count => _status.Text = $"Firmando {count} / {_documents.Count}...");
        var signingStatus = new Progress<string>(message => _status.Text = message);

        try
        {
            IReadOnlyList<TemporarySignatureResult> results;
            using (pin)
            {
                results = await new TemporaryBatchSignatureService().SignAndUploadAsync(
                    _documents, item.Certificate, _apiService, pin, progress, signingStatus);
            }

            Hide();
            using var report = new BatchSignatureResultsForm(results);
            report.ShowDialog();
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al firmar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                _signing = false;
                _certificates.Enabled = true;
                _refresh.Enabled = true;
                _refreshTimer.Start();
                UpdateSignButton();
            }
        }
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
