using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Paratext.PluginInterfaces;
using ReferenceTransfer.Core.Engine;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Plugin.UI
{
    public class TransferMainWindow : Form
    {
        private readonly IPluginHost _host;
        private readonly IParatextChildState? _childState;
        private readonly IPluginObject _pluginObj;
        private readonly ParatextScriptureService _service;

        // UI Controls
        private ComboBox _cboSourceType = null!;
        private ComboBox _cboSourceVal = null!;
        private Button _btnBrowseFile = null!;
        private Button _btnPaste = null!;

        private ComboBox _cboTargetProject = null!;
        private ComboBox _cboNameStyle = null!;
        private Label _lblVernacularStatus = null!;
        private ComboBox _cboScope = null!;
        private ComboBox _cboSingleBook = null!;

        private CheckBox _chkParentheses = null!;
        private CheckBox _chkHeading = null!;
        private CheckBox _chkCleanPunct = null!;
        private CheckBox _chkRemoveExisting = null!;

        private Button _btnScan = null!;
        private Button _btnApply = null!;
        private Button _btnSelectAll = null!;
        private Button _btnUnselectAll = null!;
        private Button _btnUninstall = null!;

        private DataGridView _grid = null!;
        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;

        private List<ReferenceTransferProposal> _currentProposals = new List<ReferenceTransferProposal>();
        private VernacularBookNameManager _vernacularMgr = new VernacularBookNameManager();
        private VernacularBookNameManager _sourceVernacularMgr = new VernacularBookNameManager();
        private ProjectScriptureSettings _scriptureSettings = new ProjectScriptureSettings();
        private ProjectScriptureSettings _sourceScriptureSettings = new ProjectScriptureSettings();
        private List<ParallelReferenceEntry> _loadedSourceReferences = new List<ParallelReferenceEntry>();

        // Modal Spinner Overlay for RegEx Pal Extraction
        private Panel _spinnerOverlay = null!;
        private Label _lblSpinnerTitle = null!;
        private Label _lblSpinnerSubtitle = null!;
        private ProgressBar _spinnerBar = null!;
        private Label _lblSpinnerStep1 = null!;
        private Label _lblSpinnerStep2 = null!;
        private Label _lblSpinnerStep3 = null!;
        private Label _lblSpinnerStep4 = null!;

        public TransferMainWindow(IPluginHost host, IParatextChildState? childState, IPluginObject pluginObj)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _childState = childState;
            _pluginObj = pluginObj ?? throw new ArgumentNullException(nameof(pluginObj));
            _service = new ParatextScriptureService(_host);

            InitializeComponent();
            LoadInitialData();
        }

        private void InitializeComponent()
        {
            Text = "Parallel Passage References Transfer - Paratext 9.5";
            Size = new Size(1200, 760);
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.FromArgb(245, 247, 250);

            // 1. Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(24, 32, 47),
                Padding = new Padding(15, 10, 15, 10)
            };

            var lblTitle = new Label
            {
                Text = "📖 Parallel Passage References Transfer",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 12)
            };

            var lblSubtitle = new Label
            {
                Text = "Synchronize parallel passage references across projects and resources with vernacular translation",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 175, 200),
                AutoSize = true,
                Location = new Point(18, 38)
            };

            _btnUninstall = new Button
            {
                Text = "Uninstall Plugin...",
                Size = new Size(140, 30),
                ForeColor = Color.FromArgb(240, 100, 100),
                BackColor = Color.FromArgb(36, 46, 66),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnUninstall.FlatAppearance.BorderColor = Color.FromArgb(80, 50, 50);
            _btnUninstall.Click += (s, e) => PluginUninstaller.InitiateUninstall(() => Close());

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(_btnUninstall);

            void PositionHeaderControls()
            {
                if (_btnUninstall != null && headerPanel != null)
                {
                    int x = Math.Max(800, headerPanel.ClientSize.Width - _btnUninstall.Width - 15);
                    _btnUninstall.Location = new Point(x, 17);
                }
            }
            headerPanel.Resize += (s, e) => PositionHeaderControls();

            // 2. Control Cards Panel
            var controlsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.White,
                Padding = new Padding(15, 10, 15, 10)
            };

            // Row 1: Source Selection
            var lblSource = new Label { Text = "1. Reference Source:", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(15, 15), AutoSize = true };
            _cboSourceType = new ComboBox { Location = new Point(160, 12), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSourceType.Items.AddRange(new object[] { "Paratext Resource", "Paratext Project", "Custom File (.txt / .sfm)" });
            _cboSourceType.SelectedIndex = 0;
            _cboSourceType.SelectedIndexChanged += CboSourceType_SelectedIndexChanged;

            _cboSourceVal = new ComboBox { Location = new Point(350, 12), Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSourceVal.SelectedIndexChanged += (s, e) => LoadSelectedSource();

            _btnBrowseFile = new Button { Text = "Browse...", Location = new Point(740, 10), Width = 80, Height = 28, Visible = false };
            _btnBrowseFile.Click += BtnBrowseFile_Click;

            _btnPaste = new Button { Text = "📋 Paste Clipboard", Location = new Point(825, 10), Width = 130, Height = 28, Visible = false };
            _btnPaste.Click += BtnPaste_Click;

            // Row 2: Target Project, Name Style, Scope
            var lblTarget = new Label { Text = "2. Target Project:", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(15, 52), AutoSize = true };
            _cboTargetProject = new ComboBox { Location = new Point(150, 49), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboTargetProject.SelectedIndexChanged += CboTargetProject_SelectedIndexChanged;

            var lblStyle = new Label { Text = "Book Name Style:", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(325, 52), AutoSize = true };
            _cboNameStyle = new ComboBox { Location = new Point(450, 49), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboNameStyle.Items.AddRange(new object[] { "Abbreviation", "Short Name", "Long Name" });
            _cboNameStyle.SelectedIndex = 0;
            _cboNameStyle.SelectedIndexChanged += CboNameStyle_SelectedIndexChanged;

            var lblScope = new Label { Text = "Scope:", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(605, 52), AutoSize = true };
            _cboScope = new ComboBox { Location = new Point(655, 49), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboScope.Items.AddRange(new object[] { "All OT (39)", "All NT (27)", "All 66 Books", "Single Book" });
            _cboScope.SelectedIndex = 0;
            _cboScope.SelectedIndexChanged += CboScope_SelectedIndexChanged;

            _cboSingleBook = new ComboBox { Location = new Point(795, 49), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };

            _lblVernacularStatus = new Label { Text = "Loading scripture reference settings...", ForeColor = Color.FromArgb(40, 120, 40), Location = new Point(895, 52), AutoSize = true };

            // Row 3: Options
            _chkParentheses = new CheckBox { Text = "Use Parentheses (e.g. (1Chr 1:1))", Checked = true, Location = new Point(15, 88), AutoSize = true };
            _chkParentheses.CheckedChanged += (s, e) => UpdateProposedReferencesInGrid();

            _chkHeading = new CheckBox { Text = "Insert '\\s Insert Heading' if missing", Checked = true, Location = new Point(275, 88), AutoSize = true };
            _chkHeading.CheckedChanged += ChkHeading_CheckedChanged;

            _chkCleanPunct = new CheckBox { Text = "Clean trailing punctuation", Checked = true, Location = new Point(540, 88), AutoSize = true };
            _chkCleanPunct.CheckedChanged += (s, e) => UpdateProposedReferencesInGrid();

            _chkRemoveExisting = new CheckBox { Text = "Replace existing \\r references", Checked = true, Location = new Point(745, 88), AutoSize = true };
            _chkRemoveExisting.CheckedChanged += ChkRemoveExisting_CheckedChanged;

            // Row 4: Actions
            _btnScan = new Button
            {
                Text = "🔍 Preview References (Scan)",
                Location = new Point(15, 115),
                Width = 220,
                Height = 30,
                BackColor = Color.FromArgb(220, 235, 252),
                ForeColor = Color.FromArgb(20, 70, 160),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnScan.FlatAppearance.BorderColor = Color.FromArgb(170, 205, 245);
            _btnScan.Click += BtnScan_Click;

            _btnApply = new Button
            {
                Text = "✓ Apply References to Target",
                Location = new Point(245, 115),
                Width = 240,
                Height = 30,
                BackColor = Color.FromArgb(220, 245, 230),
                ForeColor = Color.FromArgb(20, 120, 50),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnApply.FlatAppearance.BorderColor = Color.FromArgb(160, 220, 180);
            _btnApply.Click += BtnApply_Click;

            _btnSelectAll = new Button { Text = "Select All", Location = new Point(500, 117), Width = 90, Height = 26 };
            _btnSelectAll.Click += (s, e) => SetAllSelection(true);

            _btnUnselectAll = new Button { Text = "Unselect All", Location = new Point(595, 117), Width = 90, Height = 26 };
            _btnUnselectAll.Click += (s, e) => SetAllSelection(false);

            controlsPanel.Controls.Add(lblSource);
            controlsPanel.Controls.Add(_cboSourceType);
            controlsPanel.Controls.Add(_cboSourceVal);
            controlsPanel.Controls.Add(_btnBrowseFile);
            controlsPanel.Controls.Add(_btnPaste);
            controlsPanel.Controls.Add(lblTarget);
            controlsPanel.Controls.Add(_cboTargetProject);
            controlsPanel.Controls.Add(lblStyle);
            controlsPanel.Controls.Add(_cboNameStyle);
            controlsPanel.Controls.Add(_lblVernacularStatus);
            controlsPanel.Controls.Add(lblScope);
            controlsPanel.Controls.Add(_cboScope);
            controlsPanel.Controls.Add(_cboSingleBook);
            controlsPanel.Controls.Add(_chkParentheses);
            controlsPanel.Controls.Add(_chkHeading);
            controlsPanel.Controls.Add(_chkCleanPunct);
            controlsPanel.Controls.Add(_chkRemoveExisting);
            controlsPanel.Controls.Add(_btnScan);
            controlsPanel.Controls.Add(_btnApply);
            controlsPanel.Controls.Add(_btnSelectAll);
            controlsPanel.Controls.Add(_btnUnselectAll);

            // 3. Grid for Review
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(230, 235, 240),
                ScrollBars = ScrollBars.Both,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _grid.RowTemplate.Height = 32;

            ConfigureGridColumns();

            // 4. Status Bar Panel
            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(240, 243, 248), Padding = new Padding(15, 6, 15, 6) };
            _lblStatus = new Label { Text = "Ready. Select source and target, then click 'Preview References'.", AutoSize = true, Location = new Point(15, 9) };
            _progressBar = new ProgressBar { Width = 200, Height = 18, Location = new Point(960, 8), Visible = false };

            void PositionStatusBar()
            {
                if (_progressBar != null && statusPanel != null)
                {
                    _progressBar.Location = new Point(statusPanel.ClientSize.Width - _progressBar.Width - 15, 9);
                }
            }
            statusPanel.Resize += (s, e) => PositionStatusBar();

            statusPanel.Controls.Add(_lblStatus);
            statusPanel.Controls.Add(_progressBar);

            // 5. Centered RegEx Pal Spinner Modal Overlay
            _spinnerOverlay = new Panel
            {
                Size = new Size(500, 270),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            _lblSpinnerTitle = new Label
            {
                Text = "⚡ RegEx Pal Reference Extraction",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 30, 50),
                Location = new Point(22, 16),
                AutoSize = true
            };
            _lblSpinnerSubtitle = new Label
            {
                Text = "Extracting parallel passage references from resource...",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 115, 130),
                Location = new Point(24, 42),
                AutoSize = true
            };
            _spinnerBar = new ProgressBar
            {
                Location = new Point(24, 68),
                Size = new Size(450, 18),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };
            _lblSpinnerStep1 = new Label { Text = "● 1. Connecting to Paratext resource engine", Location = new Point(26, 102), AutoSize = true, ForeColor = Color.FromArgb(70, 80, 95) };
            _lblSpinnerStep2 = new Label { Text = "● 2. Running RegEx Pal extraction (\\r references)", Location = new Point(26, 130), AutoSize = true, ForeColor = Color.FromArgb(70, 80, 95) };
            _lblSpinnerStep3 = new Label { Text = "● 3. Indexing Canonical 3-letter Bible book codes", Location = new Point(26, 158), AutoSize = true, ForeColor = Color.FromArgb(70, 80, 95) };
            _lblSpinnerStep4 = new Label { Text = "● 4. Translating Source Vernacular → Target Vernacular", Location = new Point(26, 186), AutoSize = true, ForeColor = Color.FromArgb(70, 80, 95) };

            _spinnerOverlay.Controls.Add(_lblSpinnerTitle);
            _spinnerOverlay.Controls.Add(_lblSpinnerSubtitle);
            _spinnerOverlay.Controls.Add(_spinnerBar);
            _spinnerOverlay.Controls.Add(_lblSpinnerStep1);
            _spinnerOverlay.Controls.Add(_lblSpinnerStep2);
            _spinnerOverlay.Controls.Add(_lblSpinnerStep3);
            _spinnerOverlay.Controls.Add(_lblSpinnerStep4);

            void CenterSpinner()
            {
                if (_spinnerOverlay != null)
                {
                    _spinnerOverlay.Location = new Point(
                        Math.Max(10, (ClientSize.Width - _spinnerOverlay.Width) / 2),
                        Math.Max(10, (ClientSize.Height - _spinnerOverlay.Height) / 2));
                }
            }
            Resize += (s, e) => CenterSpinner();

            // Add panels in order (spinner overlay on top)
            Controls.Add(_spinnerOverlay);
            Controls.Add(_grid);
            Controls.Add(controlsPanel);
            Controls.Add(statusPanel);
            Controls.Add(headerPanel);
        }

        private void ConfigureGridColumns()
        {
            _grid.Columns.Clear();

            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name = "ColApply",
                HeaderText = "Apply",
                Width = 60
            };
            _grid.Columns.Add(chkCol);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColVerse", HeaderText = "Target Verse", Width = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColCanonical", HeaderText = "Canonical Ref", Width = 130, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColSource", HeaderText = "Source (Original)", Width = 250, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColProposed", HeaderText = "Proposed (Target)", Width = 280, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Status", Width = 210, ReadOnly = true });

            _grid.ShowCellToolTips = true;
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellValueChanged += Grid_CellValueChanged;
        }

        private BookNameStyle GetSelectedNameStyle()
        {
            switch (_cboNameStyle.SelectedIndex)
            {
                case 1:
                    return BookNameStyle.ShortName;
                case 2:
                    return BookNameStyle.LongName;
                case 0:
                default:
                    return BookNameStyle.Abbreviation;
            }
        }

        private void CboNameStyle_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateProposedReferencesInGrid();
        }

        private void UpdateProposedReferencesInGrid()
        {
            if (_currentProposals == null || _currentProposals.Count == 0) return;

            var style = GetSelectedNameStyle();
            bool useParens = _chkParentheses.Checked;
            bool cleanPunct = _chkCleanPunct.Checked;

            for (int i = 0; i < _currentProposals.Count; i++)
            {
                var p = _currentProposals[i];
                p.CanonicalReference = _vernacularMgr.ToCanonicalCitation(p.SourceReference, _sourceVernacularMgr);
                p.ProposedReference = _vernacularMgr.TranslateCitation(
                    p.SourceReference, 
                    useParens, 
                    cleanPunct, 
                    style, 
                    _scriptureSettings,
                    _sourceVernacularMgr);

                if (i < _grid.Rows.Count)
                {
                    _grid.Rows[i].Cells["ColCanonical"].Value = p.CanonicalReference;
                    _grid.Rows[i].Cells["ColProposed"].Value = p.ProposedReference;
                    _grid.Rows[i].Cells["ColSource"].Value = FormatDisplayReference(p.SourceReference, useParens, cleanPunct);
                    _grid.Rows[i].Cells["ColStatus"].Value = p.Status;
                    _grid.Rows[i].Cells["ColStatus"].ToolTipText = GetStatusToolTip(p);
                }
            }

            _grid.Refresh();
        }

        private static string FormatDisplayReference(string rawRef, bool useParens, bool cleanPunct)
        {
            if (string.IsNullOrWhiteSpace(rawRef)) return string.Empty;
            string s = rawRef.Trim();
            if ((s.StartsWith("(") && s.EndsWith(")")) || (s.StartsWith("[") && s.EndsWith("]")))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            if (cleanPunct)
            {
                s = s.TrimEnd('.', ':', ';', ',');
            }
            return useParens ? $"({s})" : s;
        }

        private void LoadInitialData()
        {
            // Populate Target Projects
            _cboTargetProject.Items.Clear();
            var projects = _service.GetAllProjects();
            foreach (var p in projects)
            {
                _cboTargetProject.Items.Add(p.ShortName);
            }

            // Pre-select active project if opened from one
            if (_childState?.Project != null)
            {
                int idx = _cboTargetProject.Items.IndexOf(_childState.Project.ShortName);
                if (idx >= 0) _cboTargetProject.SelectedIndex = idx;
            }
            else if (_cboTargetProject.Items.Count > 0)
            {
                _cboTargetProject.SelectedIndex = 0;
            }

            // Populate Single Books dropdown
            _cboSingleBook.Items.Clear();
            foreach (var b in CanonicalBookLists.All66Books)
            {
                _cboSingleBook.Items.Add(b);
            }
            _cboSingleBook.SelectedIndex = 0;

            // Trigger source population
            CboSourceType_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private void CboSourceType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _cboSourceVal.Items.Clear();
            _loadedSourceReferences.Clear();
            int sel = _cboSourceType.SelectedIndex;

            if (sel == 0) // Paratext Resource
            {
                _btnBrowseFile.Visible = false;
                _btnPaste.Visible = false;
                _cboSourceVal.Visible = true;
                var installedRes = _service.GetInstalledResourceIds();
                var resources = ResourceCatalog.GetAvailableResources(installedRes);
                foreach (var r in resources)
                {
                    _cboSourceVal.Items.Add(r);
                }
                if (_cboSourceVal.Items.Count > 0)
                {
                    _cboSourceVal.SelectedIndex = 0;
                }
                else
                {
                    _lblStatus.Text = "No installed resources found in Paratext.";
                }
            }
            else if (sel == 1) // Paratext Project
            {
                _btnBrowseFile.Visible = false;
                _btnPaste.Visible = false;
                _cboSourceVal.Visible = true;
                var projects = _service.GetAllProjects();
                foreach (var p in projects)
                {
                    _cboSourceVal.Items.Add(p.ShortName);
                }
                if (_cboSourceVal.Items.Count > 0)
                {
                    _cboSourceVal.SelectedIndex = 0;
                    _lblStatus.Text = $"Source project selected: {_cboSourceVal.SelectedItem}. References will be extracted on Scan.";
                }
                else
                {
                    _lblStatus.Text = "No Paratext projects found.";
                }
            }
            else if (sel == 2) // Custom File / RegEx Pal Export
            {
                _btnBrowseFile.Visible = true;
                _btnPaste.Visible = true;
                _cboSourceVal.Visible = true;
                _lblStatus.Text = "Click 'Browse...' to choose a reference text file (.txt/.sfm) or 'Paste Clipboard' to import RegEx Pal output.";
            }
        }

        private void LoadSelectedSource()
        {
            string sourceId = string.Empty;
            if (_cboSourceType.SelectedIndex == 0 && _cboSourceVal.SelectedItem is ResourceItemInfo res)
            {
                sourceId = res.Id;
                _loadedSourceReferences.Clear();
                _lblStatus.Text = $"Selected resource: {res.DisplayName}. Click 'Preview References (Scan)' to extract live references.";
            }
            else if (_cboSourceType.SelectedIndex == 1 && _cboSourceVal.SelectedItem != null)
            {
                sourceId = _cboSourceVal.SelectedItem.ToString() ?? string.Empty;
                _loadedSourceReferences.Clear();
                _lblStatus.Text = $"Source project selected: {sourceId}. Click 'Preview References (Scan)' to extract live references.";
            }

            if (!string.IsNullOrEmpty(sourceId))
            {
                var srcProj = _service.FindProjectOrResource(sourceId);
                _sourceVernacularMgr = _service.LoadVernacularManager(srcProj);
                _sourceScriptureSettings = _service.LoadProjectScriptureSettings(srcProj);
            }
            else
            {
                _sourceVernacularMgr = new VernacularBookNameManager();
                _sourceScriptureSettings = new ProjectScriptureSettings();
            }
        }

        private void BtnBrowseFile_Click(object? sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Reference Text Files (*.txt;*.sfm)|*.txt;*.sfm|All Files (*.*)|*.*";
                ofd.Title = "Select Parallel Passage Reference File";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _cboSourceVal.Items.Clear();
                    _cboSourceVal.Items.Add(ofd.FileName);
                    _cboSourceVal.SelectedIndex = 0;
                    string text = File.ReadAllText(ofd.FileName);
                    _loadedSourceReferences = ReferenceParser.ParseReferencesText(text, "CUSTOM");
                    _lblStatus.Text = $"Loaded custom file: {Path.GetFileName(ofd.FileName)} ({_loadedSourceReferences.Count} references).";
                }
            }
        }

        private void BtnPaste_Click(object? sender, EventArgs e)
        {
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("Clipboard is empty. Please copy text from RegEx Pal or a reference list first.", "Empty Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var parsed = ReferenceParser.ParseReferencesText(text, "CLIPBOARD");
                if (parsed.Count == 0)
                {
                    MessageBox.Show("No valid parallel references could be parsed from the clipboard text.\n\nExpected RegEx Pal extract format:\nGEN 5:0: \\r (1 इति 1:1-4)\nEXO 18:12: \\r (व्य 1:9-18)", "No References Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _loadedSourceReferences = parsed;
                _cboSourceVal.Items.Clear();
                _cboSourceVal.Items.Add($"[Clipboard: {_loadedSourceReferences.Count} references loaded]");
                _cboSourceVal.SelectedIndex = 0;
                _lblStatus.Text = $"Loaded {_loadedSourceReferences.Count} references from clipboard.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read from clipboard: {ex.Message}", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CboTargetProject_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string projName = _cboTargetProject.SelectedItem?.ToString() ?? string.Empty;
            var proj = _service.GetAllProjects().FirstOrDefault(p => p.ShortName == projName);
            if (proj != null)
            {
                _vernacularMgr = _service.LoadVernacularManager(proj);
                _scriptureSettings = _service.LoadProjectScriptureSettings(proj);

                if (_scriptureSettings.IsLoaded)
                {
                    _cboNameStyle.SelectedIndex = (int)_scriptureSettings.DefaultMarkerRStyle;
                }

                if (_scriptureSettings.IsLoaded || _vernacularMgr.IsLoaded)
                {
                    _lblVernacularStatus.Text = "✓ Scripture reference settings loaded";
                    _lblVernacularStatus.ForeColor = Color.FromArgb(30, 130, 60);

                    string details = $"{_vernacularMgr.BookCount} books mapped\nChapter:Verse separator: '{_scriptureSettings.ChapterVerseSeparator}'\nRange indicator: '{_scriptureSettings.RangeIndicator}'";
                    var tt = new ToolTip();
                    tt.SetToolTip(_lblVernacularStatus, details);
                }
                else
                {
                    _lblVernacularStatus.Text = "⚠ Default English settings used";
                    _lblVernacularStatus.ForeColor = Color.FromArgb(180, 83, 9);
                }

                UpdateProposedReferencesInGrid();
            }
        }

        private void CboScope_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _cboSingleBook.Visible = (_cboScope.SelectedIndex == 3);
        }

        private void SetAllSelection(bool selected)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is ReferenceTransferProposal p)
                {
                    // Do not auto-select warnings/duplicates unless deliberate
                    if (!selected || (!p.IsDuplicate && !p.HasWarning))
                    {
                        p.IsAccepted = selected;
                        row.Cells["ColApply"].Value = selected;
                    }
                }
            }
            UpdateStatusCounts();
        }

        private void ShowSpinner(string title, string subtitle)
        {
            _lblSpinnerTitle.Text = title;
            _lblSpinnerSubtitle.Text = subtitle;
            _lblSpinnerStep1.ForeColor = Color.FromArgb(30, 130, 60);
            _lblSpinnerStep1.Text = "✓ 1. Connected to Paratext engine & resources";
            _lblSpinnerStep2.ForeColor = Color.FromArgb(37, 99, 235);
            _lblSpinnerStep2.Text = "● 2. Running RegEx Pal extraction (\\r references)...";
            _lblSpinnerStep3.ForeColor = Color.FromArgb(140, 150, 165);
            _lblSpinnerStep3.Text = "○ 3. Indexing Canonical 3-letter Bible book codes";
            _lblSpinnerStep4.ForeColor = Color.FromArgb(140, 150, 165);
            _lblSpinnerStep4.Text = "○ 4. Translating Source Vernacular → Target Vernacular";
            _spinnerOverlay.Location = new Point(
                Math.Max(10, (ClientSize.Width - _spinnerOverlay.Width) / 2),
                Math.Max(10, (ClientSize.Height - _spinnerOverlay.Height) / 2));
            _spinnerOverlay.BringToFront();
            _spinnerOverlay.Visible = true;
            Application.DoEvents();
        }

        private void SetSpinnerStep(int step, string text, bool isDone = false)
        {
            Color color = isDone ? Color.FromArgb(30, 130, 60) : Color.FromArgb(37, 99, 235);
            string prefix = isDone ? "✓ " : "● ";
            if (step == 2)
            {
                _lblSpinnerStep2.ForeColor = color;
                _lblSpinnerStep2.Text = prefix + text;
            }
            else if (step == 3)
            {
                _lblSpinnerStep3.ForeColor = color;
                _lblSpinnerStep3.Text = prefix + text;
            }
            else if (step == 4)
            {
                _lblSpinnerStep4.ForeColor = color;
                _lblSpinnerStep4.Text = prefix + text;
            }
            Application.DoEvents();
        }

        private void HideSpinner()
        {
            _spinnerOverlay.Visible = false;
        }

        private void BtnScan_Click(object? sender, EventArgs e)
        {
            string projName = _cboTargetProject.SelectedItem?.ToString() ?? string.Empty;
            var proj = _service.GetAllProjects().FirstOrDefault(p => p.ShortName == projName);
            if (proj == null)
            {
                MessageBox.Show("Please select a target Paratext project.", "Missing Target", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Determine books to scan
            var scopeType = (BookScopeType)_cboScope.SelectedIndex;
            string? singleBook = _cboSingleBook.SelectedItem?.ToString();
            var targetBookCodes = CanonicalBookLists.GetBooksForScope(scopeType, singleBook);

            string srcLabel = _cboSourceVal.SelectedItem?.ToString() ?? "Source";
            ShowSpinner("⚡ RegEx Pal Reference Extraction", $"Extracting parallel references for target project '{projName}'...");

            try
            {
                // Step 2: Handle source extraction (Resource, Project, or Preset)
                SetSpinnerStep(2, $"Running RegEx Pal extraction on {srcLabel}...");

                IReadOnlyProject? srcProj = null;

                if (_cboSourceType.SelectedIndex == 1) // Paratext Project
                {
                    string srcName = _cboSourceVal.SelectedItem?.ToString() ?? string.Empty;
                    srcProj = _service.GetAllProjects().FirstOrDefault(p => p.ShortName == srcName);
                }
                else if (_cboSourceType.SelectedIndex == 0 && _cboSourceVal.SelectedItem is ResourceItemInfo res)
                {
                    srcProj = _service.FindProjectOrResource(res.Id);
                }

                if (srcProj != null)
                {
                    _loadedSourceReferences = _service.ExtractReferencesFromProject(srcProj, targetBookCodes);
                    _sourceVernacularMgr = _service.LoadVernacularManager(srcProj);
                    _sourceScriptureSettings = _service.LoadProjectScriptureSettings(srcProj);
                }
                else if (_cboSourceType.SelectedIndex == 2 && _loadedSourceReferences.Count == 0)
                {
                    // Fallback to reload if custom file path in combobox
                    string path = _cboSourceVal.SelectedItem?.ToString() ?? string.Empty;
                    if (File.Exists(path))
                    {
                        string text = File.ReadAllText(path);
                        _loadedSourceReferences = ReferenceParser.ParseReferencesText(text, "CUSTOM");
                    }
                }

                SetSpinnerStep(2, $"Extracted {_loadedSourceReferences.Count} parallel references from source", isDone: true);

                if (_loadedSourceReferences.Count == 0)
                {
                    HideSpinner();
                    MessageBox.Show(
                        $"No parallel passage references (\\r) were found in '{srcLabel}' for the selected book scope.\n\n" +
                        "This source does not contain \\r parallel passage markers in its text.\n\n" +
                        "Please select a source that contains parallel passage references (e.g. HINOVBSI) or load references via 'Custom File' / 'Paste Clipboard'.",
                        "No References Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _lblStatus.Text = $"No parallel references found in {srcLabel}.";
                    return;
                }

                var options = new TransferOptions
                {
                    NameStyle = GetSelectedNameStyle(),
                    ScriptureSettings = _scriptureSettings,
                    UseParentheses = _chkParentheses.Checked,
                    InsertHeadingIfMissing = _chkHeading.Checked,
                    CleanTrailingPunctuation = _chkCleanPunct.Checked,
                    RemoveExistingReferences = _chkRemoveExisting.Checked
                };

                // Step 3: Indexing Canonical 3-letter Bible book codes
                SetSpinnerStep(3, "Indexing Canonical 3-letter Bible codes (GEN, 1CH, etc.)...");

                _progressBar.Visible = true;
                _progressBar.Value = 0;
                _currentProposals.Clear();

                int total = targetBookCodes.Count;
                int done = 0;

                foreach (string code in targetBookCodes)
                {
                    done++;
                    _progressBar.Value = (int)((done / (double)total) * 100);
                    _lblStatus.Text = $"Scanning {code} ({done} of {total})...";
                    Application.DoEvents();

                    int bookNum = _service.GetBookNum(code);
                    if (bookNum <= 0) continue;

                    var bookProposals = _service.ScanBook(proj, bookNum, _loadedSourceReferences, _vernacularMgr, options, _sourceVernacularMgr);
                    _currentProposals.AddRange(bookProposals);
                }

                SetSpinnerStep(3, $"Indexed {_currentProposals.Count} canonical passage mappings", isDone: true);

                // Step 4: Translating Source Vernacular -> Target Vernacular
                SetSpinnerStep(4, $"Translating citations to target '{projName}' & Scripture Settings...");
                System.Threading.Thread.Sleep(300); // Smooth animation for translator confirmation
                SetSpinnerStep(4, $"Translated {_currentProposals.Count} citations to target {projName}", isDone: true);
            }
            finally
            {
                _progressBar.Visible = false;
                HideSpinner();
            }

            PopulateGrid();
            UpdateStatusCounts();
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();

            bool useParens = _chkParentheses.Checked;
            bool cleanPunct = _chkCleanPunct.Checked;

            foreach (var p in _currentProposals)
            {
                int rowIdx = _grid.Rows.Add();
                var row = _grid.Rows[rowIdx];
                row.Tag = p;

                string sourceDisplay = FormatDisplayReference(p.SourceReference, useParens, cleanPunct);

                row.Cells["ColApply"].Value = p.IsAccepted;
                row.Cells["ColVerse"].Value = p.ReferenceLocation;
                row.Cells["ColCanonical"].Value = p.CanonicalReference;
                row.Cells["ColSource"].Value = sourceDisplay;
                row.Cells["ColProposed"].Value = p.ProposedReference;
                row.Cells["ColStatus"].Value = p.Status;
                row.Cells["ColStatus"].ToolTipText = GetStatusToolTip(p);

                if (p.IsDuplicate)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
                else if (p.HasWarning)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 235); // Amber warning
                }
            }
        }

        private static string GetStatusToolTip(ReferenceTransferProposal p)
        {
            if (p.IsDuplicate)
                return "Identical reference already exists in target text. Skipped to prevent duplicate markers.";
            if (p.HasWarning)
                return p.Warning;
            if (!string.IsNullOrEmpty(p.ExistingReference))
            {
                return p.WillReplaceExisting
                    ? $"Replaces existing reference '{p.ExistingReference}' in target text."
                    : $"Target already has reference '{p.ExistingReference}'. Skipped because 'Replace existing \\r references' is unchecked.";
            }
            if (p.IsEnglishFallback)
                return (p.NeedsHeading && p.WillInsertHeading) 
                    ? "Target project Scripture Reference Settings is blank for this book (standard English name used). Also inserting '\\s Insert Heading'."
                    : "Target project Scripture Reference Settings is blank for this book (standard English name used).";
            if (p.NeedsHeading)
            {
                return p.WillInsertHeading
                    ? "Target text has no section heading here. Plugin will insert '\\s Insert Heading' before '\\r'."
                    : "Target text has no section heading here. '\\s' will NOT be inserted because 'Insert \\s if missing' is unchecked.";
            }
            
            return "Clean insertion: A section heading already exists above this verse in the target text.";
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "ColStatus") return;

            e.PaintBackground(e.CellBounds, true);

            if (e.Value is string statusText && !string.IsNullOrWhiteSpace(statusText))
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Color bgColor;
                Color textColor;
                Color borderColor;

                if (statusText.StartsWith("Duplicate") || statusText.StartsWith("Existing Kept"))
                {
                    bgColor = Color.FromArgb(241, 245, 249);
                    textColor = Color.FromArgb(100, 116, 139);
                    borderColor = Color.FromArgb(203, 213, 225);
                }
                else if (statusText.StartsWith("Replaces"))
                {
                    bgColor = Color.FromArgb(254, 243, 199);
                    textColor = Color.FromArgb(180, 83, 9);
                    borderColor = Color.FromArgb(252, 211, 77);
                }
                else if (statusText.StartsWith("English Fallback"))
                {
                    bgColor = Color.FromArgb(243, 232, 255);
                    textColor = Color.FromArgb(126, 34, 206);
                    borderColor = Color.FromArgb(216, 180, 254);
                }
                else if (statusText.StartsWith("New"))
                {
                    bgColor = Color.FromArgb(220, 252, 231);
                    textColor = Color.FromArgb(21, 128, 61);
                    borderColor = Color.FromArgb(134, 239, 172);
                }
                else // Warning / Error
                {
                    bgColor = Color.FromArgb(254, 226, 226);
                    textColor = Color.FromArgb(185, 28, 28);
                    borderColor = Color.FromArgb(252, 165, 165);
                }

                using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                var size = TextRenderer.MeasureText(statusText, font);
                int padH = 10;
                int pillWidth = Math.Min(size.Width + padH * 2, e.CellBounds.Width - 10);
                int pillHeight = Math.Min(22, e.CellBounds.Height - 8);
                int pillX = e.CellBounds.X + (e.CellBounds.Width - pillWidth) / 2;
                int pillY = e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2;

                var pillRect = new Rectangle(pillX, pillY, pillWidth, pillHeight);

                using (var brush = new SolidBrush(bgColor))
                using (var pen = new Pen(borderColor, 1))
                using (var path = CreateRoundedRectanglePath(pillRect, 8))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                TextRenderer.DrawText(
                    g,
                    statusText,
                    font,
                    pillRect,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            e.Handled = true;
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            if (rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                var row = _grid.Rows[e.RowIndex];
                if (row.Tag is ReferenceTransferProposal p)
                {
                    p.IsAccepted = Convert.ToBoolean(row.Cells[0].Value);
                    UpdateStatusCounts();
                }
            }
        }

        private void ChkHeading_CheckedChanged(object? sender, EventArgs e)
        {
            bool headingEnabled = _chkHeading.Checked;

            foreach (var p in _currentProposals)
            {
                p.WillInsertHeading = headingEnabled;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Tag is ReferenceTransferProposal p)
                {
                    _grid.Rows[i].Cells["ColStatus"].Value = p.Status;
                    _grid.Rows[i].Cells["ColStatus"].ToolTipText = GetStatusToolTip(p);
                }
            }

            _grid.Refresh();
            UpdateStatusCounts();
        }

        private void ChkRemoveExisting_CheckedChanged(object? sender, EventArgs e)
        {
            bool replaceEnabled = _chkRemoveExisting.Checked;

            foreach (var p in _currentProposals)
            {
                if (!p.IsDuplicate && !p.HasWarning && !string.IsNullOrEmpty(p.ExistingReference))
                {
                    p.WillReplaceExisting = replaceEnabled;
                    p.IsAccepted = replaceEnabled;
                }
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Tag is ReferenceTransferProposal p)
                {
                    if (!p.IsDuplicate && !p.HasWarning && !string.IsNullOrEmpty(p.ExistingReference))
                    {
                        _grid.Rows[i].Cells["ColApply"].Value = p.IsAccepted;
                        _grid.Rows[i].Cells["ColStatus"].Value = p.Status;
                        _grid.Rows[i].Cells["ColStatus"].ToolTipText = GetStatusToolTip(p);
                    }
                }
            }

            _grid.Refresh();
            UpdateStatusCounts();
        }

        private void UpdateStatusCounts()
        {
            int total = _currentProposals.Count;
            int accepted = _currentProposals.Count(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning);
            int newClean = _currentProposals.Count(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning && !p.NeedsHeading && string.IsNullOrEmpty(p.ExistingReference));
            int newHeading = _currentProposals.Count(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning && p.NeedsHeading && string.IsNullOrEmpty(p.ExistingReference));
            int replaces = _currentProposals.Count(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning && !string.IsNullOrEmpty(p.ExistingReference));
            int existingKept = _currentProposals.Count(p => !p.IsDuplicate && !p.HasWarning && !string.IsNullOrEmpty(p.ExistingReference) && !p.WillReplaceExisting);
            int duplicates = _currentProposals.Count(p => p.IsDuplicate);
            int warnings = _currentProposals.Count(p => p.HasWarning);

            string headingDetail = newHeading > 0 ? $" ({newClean} clean, {newHeading} with \\s)" : "";
            string keptDetail = existingKept > 0 ? $" | Existing Kept: {existingKept}" : "";
            _lblStatus.Text = $"Total References: {total} | Ready: {accepted}{headingDetail} | Replaces: {replaces}{keptDetail} | Duplicates: {duplicates} | Warnings: {warnings}";
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            var approved = _currentProposals.Where(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning).ToList();
            if (approved.Count == 0)
            {
                MessageBox.Show("No approved references selected for transfer.", "None Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projName = _cboTargetProject.SelectedItem?.ToString() ?? string.Empty;
            var proj = _service.GetAllProjects().FirstOrDefault(p => p.ShortName == projName);
            if (proj == null) return;

            var confirm = MessageBox.Show(
                $"Apply {approved.Count} parallel references to project '{proj.ShortName}'?\n\n" +
                "• A Paratext History point will be created automatically.\n" +
                "• You can undo this transfer at any time from Paratext Project History.",
                "Confirm Transfer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            var options = new TransferOptions
            {
                NameStyle = GetSelectedNameStyle(),
                ScriptureSettings = _scriptureSettings,
                UseParentheses = _chkParentheses.Checked,
                InsertHeadingIfMissing = _chkHeading.Checked,
                CleanTrailingPunctuation = _chkCleanPunct.Checked,
                RemoveExistingReferences = _chkRemoveExisting.Checked
            };

            // Group by book and apply
            var byBook = approved.GroupBy(p => p.BookCode);
            int booksModified = 0;
            int refsInserted = 0;

            _progressBar.Visible = true;
            _progressBar.Value = 0;
            int totalBooks = byBook.Count();
            int currentBook = 0;

            foreach (var bGroup in byBook)
            {
                currentBook++;
                _progressBar.Value = (int)((currentBook / (double)totalBooks) * 100);
                string bCode = bGroup.Key;
                _lblStatus.Text = $"Applying references to {bCode} ({currentBook} of {totalBooks})...";
                Application.DoEvents();

                int bNum = _service.GetBookNum(bCode);
                if (bNum <= 0) continue;

                bool ok = _service.ApplyApprovedReferences(proj, _pluginObj, bNum, bGroup.ToList(), _vernacularMgr, options, out string msg);
                if (ok)
                {
                    booksModified++;
                    refsInserted += bGroup.Count();
                }
            }

            _progressBar.Visible = false;
            _lblStatus.Text = $"Transfer Complete! Transferred {refsInserted} references across {booksModified} books.";

            MessageBox.Show(
                $"Successfully transferred {refsInserted} parallel references into {booksModified} books in '{proj.ShortName}'!\n\n" +
                "Paratext editors will automatically reload with the newly inserted references.",
                "Transfer Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Re-scan to show updated state
            BtnScan_Click(this, EventArgs.Empty);
        }
    }
}
