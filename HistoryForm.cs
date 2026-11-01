using System.ComponentModel;
namespace DNDLootRoller;

internal sealed class HistoryForm : Form
{
    private readonly ComboBox player = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox workbook = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker from = new() { Width = 130, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly DateTimePicker to = new() { Width = 130, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly Label count = new() { AutoSize = true };
    private List<HistoryRecord> visible = [];
    public HistoryForm()
    {
        Text = "Shared Loot History — all campaigns"; Size = new Size(1150, 650); MinimumSize = new Size(800, 450); StartPosition = FormStartPosition.CenterParent;
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        filters.Controls.AddRange([new Label { Text = "Player", AutoSize = true }, player, new Label { Text = "Spreadsheet", AutoSize = true }, workbook, new Label { Text = "From", AutoSize = true }, from, new Label { Text = "Through", AutoSize = true }, to]);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        void Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true }; b.Click += (_, _) => Run(action); actions.Controls.Add(b); }
        Button("Refresh", RefreshRows); Button("Correct selected…", Edit); Button("Delete selected…", Delete); Button("Export filtered Excel…", Export);
        actions.Controls.Add(count);
        Controls.Add(grid); Controls.Add(filters); Controls.Add(actions);
        player.SelectedIndexChanged += (_, _) => Run(RefreshRows); workbook.SelectedIndexChanged += (_, _) => Run(RefreshRows);
        from.ValueChanged += (_, _) => Run(RefreshRows); to.ValueChanged += (_, _) => Run(RefreshRows);
        Shown += (_, _) => Run(RefreshRows);
    }
    private void Run(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "History problem", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private void RefreshRows()
    {
        var rows = HistoryStore.Read();
        void Choices(ComboBox box, IEnumerable<string> values)
        {
            string selected = box.SelectedItem as string ?? "";
            var next = new[] { "" }.Concat(values.Distinct().Order()).ToArray();
            if (box.Items.Cast<string>().SequenceEqual(next)) return;
            box.Items.Clear(); box.Items.AddRange(next); box.SelectedItem = next.Contains(selected) ? selected : "";
        }
        Choices(player, rows.Select(r => r.Player)); Choices(workbook, rows.Select(r => r.Workbook));
        if (from.Checked && to.Checked && from.Value.Date > to.Value.Date) throw new InvalidOperationException("The start date must be on or before the end date.");
        visible = HistoryStore.Filter(rows, player.Text, workbook.Text, from.Checked ? from.Value : null, to.Checked ? to.Value : null);
        grid.DataSource = new BindingList<HistoryRecord>(visible);
        grid.Columns[nameof(HistoryRecord.Id)].Visible = false;
        grid.Columns[nameof(HistoryRecord.Timestamp)].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss zzz";
        count.Text = $"{visible.Count} of {rows.Count} records. Dates use this computer's local time.";
    }
    private HistoryRecord Selected() => grid.CurrentRow?.DataBoundItem as HistoryRecord ?? throw new InvalidOperationException("Select a history record first.");
    private void Edit()
    {
        var original = Selected();
        var copy = System.Text.Json.JsonSerializer.Deserialize<HistoryRecord>(System.Text.Json.JsonSerializer.Serialize(original))!;
        using var dialog = new Form { Text = "Correct loot record", Size = new Size(620, 570), StartPosition = FormStartPosition.CenterParent };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        TextBox Field(string label, string value) { var box = new TextBox { Text = value, Dock = DockStyle.Top }; fields.Controls.Add(new Label { Text = label, AutoSize = true }); fields.Controls.Add(box); return box; }
        var time = Field("Timestamp with offset", copy.Timestamp.ToString("O"));
        var name = Field("Player", copy.Player); var book = Field("Workbook", copy.Workbook); var sheet = Field("Sheet", copy.Sheet);
        var d6 = Field("D6 (1–6)", copy.D6.ToString()); var d100 = Field("D100 (1–100)", copy.D100.ToString());
        var sd = Field("Sub die (optional)", copy.SubDie?.ToString() ?? ""); var sr = Field("Sub roll (optional)", copy.SubRoll?.ToString() ?? "");
        var loot = Field("Loot description", copy.Loot); loot.Multiline = true; loot.Height = 110; loot.ScrollBars = ScrollBars.Vertical;
        var save = new Button { Text = "Save correction", Dock = DockStyle.Bottom, Height = 40 };
        save.Click += (_, _) => {
            try {
                if (!DateTimeOffset.TryParse(time.Text, out var timestamp)) throw new InvalidOperationException("Enter a valid timestamp with offset.");
                if (!int.TryParse(d6.Text, out int six) || six < 1 || six > 6 || !int.TryParse(d100.Text, out int hundred) || hundred < 1 || hundred > 100) throw new InvalidOperationException("D6 must be 1–6 and D100 must be 1–100.");
                int? die = null, roll = null;
                if (sd.Text.Length > 0 || sr.Text.Length > 0) {
                    if (!int.TryParse(sd.Text, out int dd) || dd < 1 || !int.TryParse(sr.Text, out int rr) || rr < 1 || rr > dd) throw new InvalidOperationException("Enter both sub die and sub roll, with roll within the die range."); die = dd; roll = rr;
                }
                if (new[] { name.Text, book.Text, sheet.Text, loot.Text }.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Player, workbook, sheet and loot are required.");
                copy.Timestamp = timestamp; copy.Player = name.Text.Trim(); copy.Workbook = book.Text.Trim(); copy.Sheet = sheet.Text.Trim(); copy.D6 = six; copy.D100 = hundred; copy.SubDie = die; copy.SubRoll = roll; copy.Loot = loot.Text;
                HistoryStore.Change(rows => { int i = rows.FindIndex(r => r.Id == copy.Id); if (i < 0) throw new IOException("This record was deleted. Refresh history."); rows[i] = copy; });
                dialog.DialogResult = DialogResult.OK;
            } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Correction not saved"); }
        };
        dialog.Controls.Add(fields); dialog.Controls.Add(save);
        if (dialog.ShowDialog(this) == DialogResult.OK) RefreshRows();
    }
    private void Delete()
    {
        var row = Selected();
        if (MessageBox.Show(this, $"Delete the selected record for {row.Player}?", "Delete history record", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        HistoryStore.Change(rows => rows.RemoveAll(r => r.Id == row.Id)); RefreshRows();
    }
    private void Export()
    {
        RefreshRows();
        using var picker = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", FileName = $"Loot-History-{DateTime.Now:yyyy-MM-dd}.xlsx" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        HistoryStore.Export(picker.FileName, visible);
        MessageBox.Show(this, $"Exported {visible.Count} records.", "Excel export complete");
    }
}
