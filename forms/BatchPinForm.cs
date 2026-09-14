using System.Security;

namespace FirmadorPades.Forms;

internal sealed class BatchPinForm : Form
{
    private static readonly Color BrandTeal = Color.FromArgb(20, 125, 106);
    private static readonly Color BrandTealDark = Color.FromArgb(13, 93, 79);
    private static readonly Color Surface = Color.FromArgb(244, 247, 251);
    private readonly SecureString _pin = new();
    private readonly TextBox _input = new()
    {
        Left = 22, Top = 111, Width = 356, Height = 34, ReadOnly = true,
        ShortcutsEnabled = false, Font = new Font("Segoe UI", 12),
        BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
    };

    internal BatchPinForm(int count)
    {
        Text = "PIN para el lote";
        ClientSize = new Size(400, 220);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        BackColor = Surface;
        Font = new Font("Segoe UI", 9);
        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = BrandTeal };
        header.Controls.Add(new Label
        {
            Text = "Confirmar firma", AutoSize = true, Location = new Point(22, 13),
            Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.White
        });
        header.Controls.Add(new Label
        {
            Text = $"Ingrese el PIN para firmar {count} documentos", AutoSize = true,
            Location = new Point(24, 44), ForeColor = Color.FromArgb(222, 244, 240)
        });
        Controls.Add(header);
        Controls.Add(new Label
        {
            Left = 22, Top = 91, Width = 356, Height = 18,
            Text = "PIN del certificado", ForeColor = Color.FromArgb(84, 97, 110),
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        var accept = new Button
        {
            Left = 178, Top = 164, Width = 110, Height = 36, Text = "Continuar", Enabled = false,
            FlatStyle = FlatStyle.Flat, BackColor = BrandTeal, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        accept.FlatAppearance.BorderSize = 0;
        accept.FlatAppearance.MouseOverBackColor = BrandTealDark;
        var cancel = new Button
        {
            Left = 298, Top = 164, Width = 80, Height = 36, Text = "Cancelar",
            DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, ForeColor = Color.FromArgb(50, 62, 73)
        };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 209);
        _input.KeyPress += (_, e) =>
        {
            e.Handled = true;
            if (e.KeyChar == '\b' && _pin.Length > 0) _pin.RemoveAt(_pin.Length - 1);
            else if (!char.IsControl(e.KeyChar) && _pin.Length < 64) _pin.AppendChar(e.KeyChar);
            _input.Text = new string('●', _pin.Length);
            _input.SelectionStart = _input.TextLength;
            accept.Enabled = _pin.Length > 0;
        };
        accept.Click += (_, _) => { if (_pin.Length > 0) DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { _input, accept, cancel });
        AcceptButton = accept;
        CancelButton = cancel;
        Shown += (_, _) => _input.Focus();
    }

    internal SecureString CopyPin()
    {
        var copy = _pin.Copy();
        copy.MakeReadOnly();
        return copy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _pin.Dispose();
        base.Dispose(disposing);
    }
}
