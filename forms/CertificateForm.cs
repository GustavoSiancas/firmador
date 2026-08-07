using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using FirmadorPades.Services;
using FirmadorPades.Models;

namespace FirmadorPades.Forms;

public partial class CertificateForm : Form
{
    private readonly CertificateService _certificateService;
    private readonly OrchestratorService _orchestratorService;

    private readonly string _documentId;
    private readonly byte[] _documentBytes;
    private readonly SignatureLocation _placement;
    private readonly DownloadService _downloadService;

    private readonly ListBox lstCertificates;

    private readonly Button btnRefresh;
    private readonly Button btnSign;

    private readonly List<CertificateItem> _items = new();

    public CertificateForm(
        CertificateService certificateService,
        OrchestratorService orchestratorService,
        string documentId,
        byte[] documentBytes,
        SignatureLocation placement,
        DownloadService downloadService)
    {
        _certificateService = certificateService;
        _orchestratorService = orchestratorService;

        _documentId = documentId;
        _documentBytes = documentBytes;
        _placement = placement;
        _downloadService = downloadService;

        Text = "Seleccionar certificado";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));

        Width = 800;
        Height = 470;
        BackColor = Color.FromArgb(244, 247, 251);

        StartPosition = FormStartPosition.CenterParent;

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

        btnRefresh = new Button
        {
            Text = "Actualizar",
            Width = 150,
            Height = 40,
            Left = 325,
            Top = 370,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black
        };

        btnSign = new Button
        {
            Text = "Firmar",
            Width = 180,
            Height = 45,
            Left = 495,
            Top = 368,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 125, 106),
            ForeColor = Color.White
        };

        btnRefresh.Click += BtnRefresh_Click;
        btnSign.Click += BtnSign_Click;

        Controls.Add(new Label { Text = "Seleccione su certificado", AutoSize = true, Location = new Point(30, 25), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Black });
        Controls.Add(new Label { Text = "Elija el certificado que utilizará para firmar este documento.", AutoSize = true, Location = new Point(32, 56), Font = new Font("Segoe UI", 9), ForeColor = Color.Black });
        Controls.Add(lstCertificates);
        Controls.Add(btnRefresh);
        Controls.Add(btnSign);

        Load += CertificateForm_Load;
    
    }

        private void CertificateForm_Load(
        object? sender,
        EventArgs e)
    {
        LoadCertificates();
    }

    private void BtnRefresh_Click(
        object? sender,
        EventArgs e)
    {
        LoadCertificates();
    }

    private void LoadCertificates()
    {
        lstCertificates.Items.Clear();

        _items.Clear();

        var certificates = _certificateService.GetAllCertificates();

        foreach (var cert in certificates)
        {
            var item = new CertificateItem(cert);

            _items.Add(item);

            lstCertificates.Items.Add(item);
        }

        if (lstCertificates.Items.Count == 0)
        {
            MessageBox.Show(
                "No se encontraron certificados de firma (FIR).",
                "FIRMADOR CAL 2D",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            btnSign.Enabled = false;
        }
        else
        {
            lstCertificates.SelectedIndex = 0;
            btnSign.Enabled = true;
        }
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
                "FIRMADOR CAL 2D",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        btnSign.Enabled = false;
        btnRefresh.Enabled = false;

        Cursor = Cursors.WaitCursor;

    try
        {
            const string reason = "Documento firmado digitalmente";

            byte[] signedPdf =
                await _orchestratorService.SignDocumentAsync(
                    _documentId,
                    _documentBytes,
                    reason,
                    item.Certificate,
                    _placement);

            using var signedForm =
                new SignedPdfForm(signedPdf, _downloadService);

            signedForm.ShowDialog(this);
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error al firmar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            btnSign.Enabled = true;
            btnRefresh.Enabled = true;
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

