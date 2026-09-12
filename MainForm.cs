using System.Security.Cryptography;

namespace DNDLootRoller;

internal sealed class MainForm : Form
{
    private LootData _data;
    private string _sourceName = "Original embedded workbook";
    private readonly Label _sourceLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(8) };
    private Button _issuesButton = new();
    private readonly ComboBox _tableBox = new();
    private readonly NumericUpDown _d100Box = new();
    private readonly Label _d6Result = new();
    private readonly Label _d100Result = new();
    private readonly TextBox _lootResult = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(255, 252, 241)
    };
    private readonly LinkLabel _itemLink = new();
    private ItemLink? _activeLink;
    private readonly Button _subRollButton = new();
    private readonly ListBox _history = new();
    private LootEntry? _currentEntry;

    public MainForm()
    {
        _data = LootData.Load();

        Text = "D&D Loot Roller — v3.0";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 650);
        Size = new Size(900, 720);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(244, 239, 226);

        BuildInterface();
        _tableBox.SelectedIndex = 0;
        UpdateSource();
        Shown += (_, _) =>
        {
            try
            {
                var saved = LootStore.Read();
                if (saved != null) Activate(saved.Data, saved.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The saved import could not be loaded. Using the original workbook.\n\n" + ex.Message,
                    "Saved import unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "D&D LOOT ROLLER  •  v3.0",
            Dock = DockStyle.Top,
            Height = 62,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Georgia", 24F, FontStyle.Bold),
            ForeColor = Color.FromArgb(82, 53, 34)
        };

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 52));

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(4)
        };

        controls.Controls.Add(new Label { Text = "D6 Table:", AutoSize = true, Margin = new Padding(4, 11, 4, 4) });

        _tableBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _tableBox.Width = 90;
        for (int i = 1; i <= 6; i++) _tableBox.Items.Add(i);
        _tableBox.SelectedIndexChanged += (_, _) =>
        {
            _d6Result.Text = $"D6: {_tableBox.SelectedItem}";
            ClearResult();
        };
        controls.Controls.Add(_tableBox);

        controls.Controls.Add(MakeButton("Roll D6", (_, _) => RollD6()));

        controls.Controls.Add(new Label { Text = "D100:", AutoSize = true, Margin = new Padding(18, 11, 4, 4) });
        _d100Box.Minimum = 1;
        _d100Box.Maximum = 100;
        _d100Box.Value = 1;
        _d100Box.Width = 80;
        controls.Controls.Add(_d100Box);

        controls.Controls.Add(MakeButton("Use D100", (_, _) => ResolveLoot((int)_d100Box.Value)));
        controls.Controls.Add(MakeButton("Roll D100", (_, _) => RollD100()));
        controls.Controls.Add(MakeButton("Roll Both", (_, _) => RollBoth(), true));

        var dicePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(4, 8, 4, 8)
        };
        dicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        ConfigureDieLabel(_d6Result, "D6: 1");
        ConfigureDieLabel(_d100Result, "D100: —");
        dicePanel.Controls.Add(_d6Result, 0, 0);
        dicePanel.Controls.Add(_d100Result, 1, 0);

        var resultPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(255, 252, 241),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(20)
        };

        _lootResult.Text = "Roll the dice to discover the loot.";
        _lootResult.Dock = DockStyle.Fill;
        _lootResult.TextAlign = HorizontalAlignment.Center;
        _lootResult.Font = new Font("Georgia", 19F, FontStyle.Bold);
        _lootResult.ForeColor = Color.FromArgb(58, 42, 31);

        _subRollButton.Text = "Roll Subtable";
        _subRollButton.Dock = DockStyle.Bottom;
        _subRollButton.Height = 42;
        _subRollButton.Visible = false;
        _subRollButton.Click += (_, _) => RollSubtable();

        resultPanel.Controls.Add(_lootResult);
        _itemLink.Dock = DockStyle.Bottom;
        _itemLink.Height = 30;
        _itemLink.TextAlign = ContentAlignment.MiddleCenter;
        _itemLink.Visible = false;
        _itemLink.LinkClicked += (_, _) => OpenItemLink();
        resultPanel.Controls.Add(_itemLink);
        resultPanel.Controls.Add(_subRollButton);

        var lower = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        lower.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var historyHeader = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        historyHeader.Controls.Add(new Label
        {
            Text = "Roll History",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(3, 9, 15, 3)
        });
        historyHeader.Controls.Add(MakeButton("Copy Result", (_, _) => CopyCurrent()));
        historyHeader.Controls.Add(MakeButton("Clear History", (_, _) => _history.Items.Clear()));
        _issuesButton = MakeButton($"Data Issues ({_data.Issues.Count})", (_, _) => ShowIssues());
        historyHeader.Controls.Add(_issuesButton);

        _history.Dock = DockStyle.Fill;
        _history.HorizontalScrollbar = true;
        _history.Font = new Font("Consolas", 10F);

        lower.Controls.Add(historyHeader, 0, 0);
        lower.Controls.Add(_history, 0, 1);

        var imports = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        imports.Controls.Add(MakeButton("Import Spreadsheet…", async (_, _) => await ImportSpreadsheet()));
        imports.Controls.Add(MakeButton("Use Original Workbook", (_, _) => RestoreOriginal()));
        imports.Controls.Add(_sourceLabel);
        main.Controls.Add(imports, 0, 0);
        main.Controls.Add(controls, 0, 1);
        main.Controls.Add(dicePanel, 0, 2);
        main.Controls.Add(resultPanel, 0, 3);
        main.Controls.Add(lower, 0, 4);

        Controls.Add(main);
        Controls.Add(title);
    }

    private async Task ImportSpreadsheet()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Import six loot tables (columns A: roll, B: loot)",
            Filter = "Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true, Multiselect = false
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        Enabled = false;
        UseWaitCursor = true;
        LootData imported;
        try { imported = await Task.Run(() => SpreadsheetImporter.Read(picker.FileName)); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The spreadsheet was not imported. Your current tables are unchanged.\n\n" + ex.Message,
                "Import problem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        finally { Enabled = true; UseWaitCursor = false; }
        string name = Path.GetFileName(picker.FileName);
        using var preview = new Form { Text = "Review spreadsheet import", Size = new Size(720, 560), MinimumSize = new Size(500, 400), StartPosition = FormStartPosition.CenterParent };
        var details = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
        var lines = new List<string> { name, "", "Worksheet order maps to D6 results 1–6.", "A: roll; B: loot; C: optional D&D Beyond item URL.", "", "VALIDATION" };
        lines.AddRange(imported.Issues.Count == 0 ? new[] { "No coverage issues found." } : imported.Issues);
        lines.Add("\r\nGaps give no loot; overlaps use the first matching row.");
        lines.Add("\r\nPREVIEW");
        foreach (var t in imported.Tables)
        {
            lines.Add($"D6 {t.Die} → tab '{t.Name}': {t.Entries.Count} loot rows");
            foreach (var e in t.Entries)
            {
                lines.Add($"  {e.Min}–{e.Max}: {e.Description}");
                if (e.BeyondUrl != null) lines.Add($"      Link: {e.BeyondUrl}");
                lines.AddRange(e.SubEntries.Select(sub => $"      D{e.SubDie} {sub.Min}–{sub.Max}: {sub.Description}" + (sub.BeyondUrl == null ? "" : $"\r\n          Link: {sub.BeyondUrl}")));
            }
        }
        lines.Add("\r\nImport replaces the active six tables and saves a local copy for next time.");
        lines.Add("Gaps give no loot; overlaps use the first matching row. Cancel keeps current tables.");
        details.Text = string.Join("\r\n", lines);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var accept = new Button { Text = imported.Issues.Count > 0 ? "Import with warnings" : "Import", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(cancel); buttons.Controls.Add(accept);
        preview.Controls.Add(details); preview.Controls.Add(buttons);
        preview.CancelButton = cancel;
        if (preview.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            LootStore.Save(new SavedLoot { Name = name, Data = imported });
            Activate(imported, name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save the import. Current tables are unchanged.\n\n" + ex.Message,
                "Save problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreOriginal()
    {
        try
        {
            var original = LootData.Load();
            LootStore.Reset();
            Activate(original, "Original embedded workbook");
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not restore the original workbook.\n\n" + ex.Message); }
    }

    private void Activate(LootData data, string name)
    {
        _data = data;
        _sourceName = name;
        ClearResult();
        UpdateSource();
        _history.Items.Insert(0, $"--- Active workbook: {name} ---");
    }

    private void UpdateSource()
    {
        _sourceLabel.Text = "Active: " + _sourceName;
        _issuesButton.Text = $"Data Issues ({_data.Issues.Count})";
    }

    private static Button MakeButton(string text, EventHandler click, bool accent = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 34,
            Padding = new Padding(9, 2, 9, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Color.FromArgb(111, 67, 42) : Color.FromArgb(225, 214, 188),
            ForeColor = accent ? Color.White : Color.FromArgb(55, 42, 31),
            Margin = new Padding(5)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(128, 94, 62);
        button.Click += click;
        return button;
    }

    private static void ConfigureDieLabel(Label label, string text)
    {
        label.Text = text;
        label.Dock = DockStyle.Fill;
        label.AutoSize = true;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Font = new Font("Georgia", 17F, FontStyle.Bold);
        label.ForeColor = Color.FromArgb(111, 67, 42);
        label.Padding = new Padding(8);
    }

    private int SelectedTable => Convert.ToInt32(_tableBox.SelectedItem ?? 1);

    private void RollD6()
    {
        int roll = RandomNumberGenerator.GetInt32(1, 7);
        _tableBox.SelectedItem = roll;
        _d6Result.Text = $"D6: {roll}";
    }

    private void RollD100()
    {
        int roll = RandomNumberGenerator.GetInt32(1, 101);
        _d100Box.Value = roll;
        ResolveLoot(roll);
    }

    private void RollBoth()
    {
        RollD6();
        RollD100();
    }

    private void ResolveLoot(int roll)
    {
        _d100Result.Text = $"D100: {roll}";
        LootTable? table = _data.Tables.FirstOrDefault(t => t.Die == SelectedTable);
        _currentEntry = table?.Entries.FirstOrDefault(e => e.Matches(roll));

        if (_currentEntry is null)
        {
            SetItemLink(null);
            _lootResult.Text = $"No loot entry exists for Table {SelectedTable}, roll {roll}.";
            _subRollButton.Visible = false;
            AddHistory(roll, _lootResult.Text);
            return;
        }

        _lootResult.Text = _currentEntry.Description;
        SetItemLink(_currentEntry.SubEntries.Count > 0 ? null : ItemLinks.For(_currentEntry.Description, _currentEntry.BeyondUrl));
        _subRollButton.Visible = _currentEntry.SubEntries.Count > 0;
        if (_subRollButton.Visible)
            _subRollButton.Text = $"Roll D{_currentEntry.SubDie} Subtable";

        AddHistory(roll, _currentEntry.Description);
    }

    private void RollSubtable()
    {
        if (_currentEntry is null || _currentEntry.SubEntries.Count == 0 || _currentEntry.SubDie is null)
            return;

        int subRoll = RandomNumberGenerator.GetInt32(1, _currentEntry.SubDie.Value + 1);
        SubEntry? subEntry = _currentEntry.SubEntries.FirstOrDefault(e => e.Matches(subRoll));
        string detail = subEntry?.Description ?? "No subtable entry found.";
        SetItemLink(subEntry == null ? null : ItemLinks.For(subEntry.Description, subEntry.BeyondUrl, _currentEntry.Description));
        _lootResult.Text = $"{_currentEntry.Description}\n\nD{_currentEntry.SubDie}: {subRoll}\n{detail}";
        _history.Items.Insert(0, $"    ↳ D{_currentEntry.SubDie} {subRoll:00}: {detail}");
    }

    private void AddHistory(int d100, string result)
    {
        _history.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} | D6 {SelectedTable} | D100 {d100:000} | {result}");
    }

    private void CopyCurrent()
    {
        if (!string.IsNullOrWhiteSpace(_lootResult.Text))
            Clipboard.SetText(_lootResult.Text);
    }

    private void ClearResult()
    {
        SetItemLink(null);
        _d100Result.Text = "D100: —";
        _lootResult.Text = "Roll the D100 to discover the loot.";
        _subRollButton.Visible = false;
        _currentEntry = null;
    }

    private void SetItemLink(ItemLink? link)
    {
        _activeLink = link;
        _itemLink.Visible = link != null;
        _itemLink.Text = link?.IsSearch == true ? "Search for this item on D&D Beyond ↗" : "View item on D&D Beyond ↗";
        _itemLink.LinkVisited = false;
        _itemLink.AccessibleDescription = link?.Url;
    }

    private void OpenItemLink()
    {
        if (_activeLink == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_activeLink.Url) { UseShellExecute = true });
            _itemLink.LinkVisited = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open your browser.\n\n" + _activeLink.Url + "\n\n" + ex.Message,
                "D&D Beyond link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowIssues()
    {
        string message = _data.Issues.Count == 0
            ? "No data issues were detected."
            : string.Join(Environment.NewLine + Environment.NewLine, _data.Issues);

        MessageBox.Show(message, "Loot Table Data Issues", MessageBoxButtons.OK,
            _data.Issues.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
