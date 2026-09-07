using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using FirmadorPades.Services;
using FirmadorPades.Models;

namespace FirmadorPades.Forms;

public class CertificateForm : Form
{
    private readonly CertificateService _certificateService;
    private readonly OrchestratorService _orchestratorService;

    private readonly byte[] _documentBytes;
    private readonly SignatureLocation _placement;

    private readonly ListBox lstCertificates;

    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 200 };
    private readonly Button btnSign;

    private readonly List<CertificateItem> _items = new();

    private bool _signatureCompleted;

    public CertificateForm(
        CertificateService certificateService,
        OrchestratorService orchestratorService,
        byte[] documentBytes,
        SignatureLocation placement)
    {
        _certificateService = certificateService;
        _orchestratorService = orchestratorService;

        _documentBytes = documentBytes;
        _placement = placement;

        Text = "Firmador CAL - Versión 1.02";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));

        Width = 800;
        Height = 470;
        BackColor = Color.FromArgb(244, 247, 251);

        StartPosition = FormStartPosition.CenterScreen;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        lstCertificates = new ListBox
        {
            Left = 30,
            Top = 92,
            Width = 725,
            Height = 245,
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Black,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            ItemHeight = 42,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        lstCertificates.DrawItem += LstCertificates_DrawItem;

        btnSign = new Button
        {
            Text = "Firmar",
            Width = 180,
            Height = 45,
            Left = lstCertificates.Right - 180,
            Top = lstCertificates.Bottom + 24,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 125, 106),
            ForeColor = Color.White
        };

        btnSign.FlatAppearance.BorderSize = 0;
        btnSign.Click += BtnSign_Click;
        AcceptButton = btnSign;

        Controls.Add(new Label { Text = "Firmador CAL - Versión 1.02", AutoSize = true, Location = new Point(30, 25), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Black });
        Controls.Add(new Label { Text = "Elija el certificado que utilizará para firmar este documento.", AutoSize = true, Location = new Point(32, 56), Font = new Font("Segoe UI", 9), ForeColor = Color.Black });
        Controls.Add(lstCertificates);
        Controls.Add(btnSign);

        _refreshTimer.Tick += (_, _) => LoadCertificates();
        Load += CertificateForm_Load;

    }

    private void CertificateForm_Load(object? sender, EventArgs e)
    {
        LoadCertificates();
        _refreshTimer.Start();
    }

    private void LoadCertificates()
    {
        List<X509Certificate2> certificates;
        try
        {
            certificates = _certificateService.GetAllCertificates();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // El almacen puede estar temporalmente inaccesible al retirar el dispositivo.
            certificates = new();
        }

        if (_items.Select(item => item.Certificate.Thumbprint)
            .SequenceEqual(certificates.Select(cert => cert.Thumbprint)))
        {
            foreach (var cert in certificates)
                cert.Dispose();
            btnSign.Enabled = lstCertificates.SelectedItem is CertificateItem;
            return;
        }

        string? selected = (lstCertificates.SelectedItem as CertificateItem)?.Certificate.Thumbprint;
        lstCertificates.BeginUpdate();
        try
        {
            lstCertificates.Items.Clear();
            foreach (var item in _items)
                item.Certificate.Dispose();
            _items.Clear();

            foreach (var cert in certificates)
            {
                var item = new CertificateItem(cert);
                _items.Add(item);
                lstCertificates.Items.Add(item);
            }

            int selectedIndex = _items.FindIndex(item => item.Certificate.Thumbprint == selected);
            lstCertificates.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_items.Count > 0 ? 0 : -1);
            btnSign.Enabled = lstCertificates.SelectedItem is CertificateItem;
        }
        finally
        {
            lstCertificates.EndUpdate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            foreach (var item in _items)
                item.Certificate.Dispose();
            _items.Clear();
        }
        base.Dispose(disposing);
    }

    private class CertificateItem
    {
        public X509Certificate2 Certificate { get; }

        public CertificateItem(
            X509Certificate2 certificate)
        {
            Certificate = certificate;
        }
    }

    private async void BtnSign_Click(
        object? sender,
        EventArgs e)
    {
        if (lstCertificates.SelectedItem is not CertificateItem item)
        {
            MessageBox.Show(
                "Seleccione un certificado.",
                "Firmador CAL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        btnSign.Enabled = false;
        btnSign.Text = "Firmando...";
        _refreshTimer.Stop();
        lstCertificates.Enabled = false;

        Cursor = Cursors.WaitCursor;

        try
        {
            const string reason = "Documento firmado digitalmente";

            byte[] signedPdf = await _orchestratorService.SignDocumentAsync(
                    _documentBytes,
                    reason,
                    item.Certificate,
                    _placement);

            _signatureCompleted = true;
            Cursor = Cursors.Default;

            using var resultForm = new SignatureSuccessForm(signedPdf);
            resultForm.ShowDialog(this);
            Application.Exit();
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(
                this,
                ex.Message,
                "Error al firmar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            if (!IsDisposed && !Disposing && !_signatureCompleted)
            {
                btnSign.Text = "Firmar";
                lstCertificates.Enabled = true;
                LoadCertificates();
                _refreshTimer.Start();
            }
        }
    }

    private void LstCertificates_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || lstCertificates.Items[e.Index] is not CertificateItem item)
            return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        e.Graphics.FillRectangle(selected ? Brushes.Gainsboro : Brushes.White, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            _certificateService.GetHolderName(item.Certificate),
            lstCertificates.Font,
            new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
            Color.Black,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }
}

