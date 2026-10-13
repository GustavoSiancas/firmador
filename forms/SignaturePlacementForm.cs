using FirmadorPades.Services;

namespace FirmadorPades.Forms;

internal sealed class SignaturePlacementForm : Form
{
    private readonly RadioButton _first = new() { Text = "Primera página", AutoSize = true };
    private readonly RadioButton _last = new() { Text = "Última página", AutoSize = true };
    private readonly Button[] _cells = new Button[12];
    private int _column;
    private int _row;
    internal LocalSignaturePlacement Placement => new(_column, _row, _first.Checked);

    internal SignaturePlacementForm(LocalSignaturePlacement placement)
    {
        _column = placement.Column;
        _row = placement.Row;
        Text = "Ubicación de la firma";
        ClientSize = new Size(420, 540);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(244, 247, 251);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        Controls.Add(new Label { Text = "¿Dónde irá la firma?", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Location = new Point(28, 24) });
        Controls.Add(new Label { Text = "Elija una zona. Se aplicará a todos los PDF.", AutoSize = true, Location = new Point(28, 56) });
        var pages = new FlowLayoutPanel { Location = new Point(28, 94), Size = new Size(364, 36) };
        _first.Checked = placement.FirstPage;
        _last.Checked = !placement.FirstPage;
        _first.Margin = new Padding(0, 0, 24, 0);
        pages.Controls.AddRange([_first, _last]);
        Controls.Add(pages);
        var grid = new TableLayoutPanel
        {
            Location = new Point(72, 144), Size = new Size(276, 310), BackColor = Color.White,
            Padding = new Padding(10), ColumnCount = 3, RowCount = 4,
            AccessibleName = "Matriz de firma: tres columnas y cuatro filas"
        };
        for (int col = 0; col < 3; col++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        for (int row = 0; row < 4; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 3; col++)
        {
            int cellRow = row, cellColumn = col;
            var button = new Button
            {
                Dock = DockStyle.Fill, Margin = new Padding(4), FlatStyle = FlatStyle.Flat,
                Text = $"{row + 1} · {col + 1}", AccessibleName = $"Fila {row + 1}, columna {col + 1}"
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 226);
            button.Click += (_, _) => { _row = cellRow; _column = cellColumn; UpdateCells(); };
            _cells[row * 3 + col] = button;
            grid.Controls.Add(button, col, row);
        }
        Controls.Add(grid);
        var cancel = new Button { Text = "Cancelar", Location = new Point(92, 480), Size = new Size(110, 40), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
        var apply = new Button { Text = "Aplicar", Location = new Point(218, 480), Size = new Size(130, 40), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(20, 125, 106), ForeColor = Color.White };
        Controls.AddRange([cancel, apply]);
        AcceptButton = apply;
        CancelButton = cancel;
        UpdateCells();
    }

    private void UpdateCells()
    {
        for (int index = 0; index < _cells.Length; index++)
        {
            bool selected = index == _row * 3 + _column;
            _cells[index].BackColor = selected ? Color.FromArgb(20, 125, 106) : Color.FromArgb(244, 247, 251);
            _cells[index].ForeColor = selected ? Color.White : Color.FromArgb(65, 82, 94);
            _cells[index].Text = selected ? "Firma" : $"{index / 3 + 1} · {index % 3 + 1}";
        }
    }
}
