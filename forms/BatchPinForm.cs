using System.Security;

namespace FirmadorPades.Forms;

internal sealed class BatchPinForm : Form
{
    private readonly SecureString _pin = new();
    private readonly TextBox _input = new() { Left = 20, Top = 70, Width = 350, ReadOnly = true, ShortcutsEnabled = false };

    internal BatchPinForm(int count)
    {
        Text = "PIN para el lote";
        ClientSize = new Size(400, 165);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        Controls.Add(new Label { Left = 20, Top = 15, Width = 360, Height = 45,
            Text = $"Ingrese el PIN para firmar {count} PDFs. Se conservará en memoria solo durante este lote." });
        var accept = new Button { Left = 185, Top = 115, Width = 100, Text = "Continuar", Enabled = false };
        var cancel = new Button { Left = 290, Top = 115, Width = 90, Text = "Cancelar", DialogResult = DialogResult.Cancel };
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
