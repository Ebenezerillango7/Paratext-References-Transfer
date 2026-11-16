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
        private ProjectScriptureSettings _scriptureSettings = new ProjectScriptureSettings();
        private List<ParallelReferenceEntry> _loadedSourceReferences = new List<ParallelReferenceEntry>();

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

            _lblVernacularStatus = new Label { Text = "Loading book names...", ForeColor = Color.FromArgb(40, 120, 40), Location = new Point(895, 52), AutoSize = true };

            // Row 3: Options
            _chkParentheses = new CheckBox { Text = "Use Parentheses (e.g. (1Chr 1:1))", Checked = true, Location = new Point(15, 88), AutoSize = true };
            _chkParentheses.CheckedChanged += (s, e) => UpdateProposedReferencesInGrid();

            _chkHeading = new CheckBox { Text = "Insert '\\s Insert Heading' if missing", Checked = true, Location = new Point(275, 88), AutoSize = true };

            _chkCleanPunct = new CheckBox { Text = "Clean trailing punctuation", Checked = true, Location = new Point(540, 88), AutoSize = true };
            _chkCleanPunct.CheckedChanged += (s, e) => UpdateProposedReferencesInGrid();

            _chkRemoveExisting = new CheckBox { Text = "Replace existing \\r references", Checked = true, Location = new Point(745, 88), AutoSize = true };

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

            // Add panels in order
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
                Width = 70
            };
            _grid.Columns.Add(chkCol);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColVerse", HeaderText = "Verse", Width = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColType", HeaderText = "Action", Width = 150, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColProposed", HeaderText = "Proposed Reference", Width = 280, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColSource", HeaderText = "Source", Width = 240, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColHeading", HeaderText = "Section Heading", Width = 220, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColWarning", HeaderText = "Notes / Warning", Width = 220, ReadOnly = true });
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
                p.ProposedReference = _vernacularMgr.TranslateCitation(p.SourceReference, useParens, cleanPunct, style, _scriptureSettings);
                if (i < _grid.Rows.Count)
                {
                    _grid.Rows[i].Cells["ColProposed"].Value = p.ProposedReference;
                }
            }
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
            else if (sel == 2) // Custom File
            {
                _btnBrowseFile.Visible = true;
                _cboSourceVal.Visible = true;
                _lblStatus.Text = "Click 'Browse...' to choose a reference text file (.txt or .sfm).";
            }
        }

        private void LoadSelectedSource()
        {
            if (_cboSourceType.SelectedIndex == 0 && _cboSourceVal.SelectedItem is ResourceItemInfo res)
            {
                if (res.HasReferences)
                {
                    _loadedSourceReferences = ResourceCatalog.LoadResource(res.Id);
                    _lblStatus.Text = $"Selected resource: {res.DisplayName} ({_loadedSourceReferences.Count} references ready).";
                }
                else
                {
                    _loadedSourceReferences.Clear();
                    _lblStatus.Text = $"Resource '{res.Id}' is installed. No pre-extracted reference stream available for this resource. Use a resource with verified references (e.g. TAOVBSI, ESVUS16, HINOVBSI) or load via 'Custom File'.";
                }
            }
            else if (_cboSourceType.SelectedIndex == 1 && _cboSourceVal.SelectedItem != null)
            {
                _loadedSourceReferences.Clear();
                _lblStatus.Text = $"Source project selected: {_cboSourceVal.SelectedItem}. References will be extracted on Scan.";
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

                string bookInfo = _vernacularMgr.IsLoaded ? $"{_vernacularMgr.BookCount} books mapped" : "BookNames not found";
                string sepInfo = _scriptureSettings.IsLoaded
                    ? $"C:V='{_scriptureSettings.ChapterVerseSeparator}', Range='{_scriptureSettings.RangeIndicator}'"
                    : "Default Separators";

                _lblVernacularStatus.Text = $"✓ {bookInfo} | {sepInfo}";
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

            // Handle Project source extraction
            if (_cboSourceType.SelectedIndex == 1) // Paratext Project
            {
                string srcName = _cboSourceVal.SelectedItem?.ToString() ?? string.Empty;
                var srcProj = _service.GetAllProjects().FirstOrDefault(p => p.ShortName == srcName);
                if (srcProj != null)
                {
                    _lblStatus.Text = $"Extracting parallel references from project '{srcName}'...";
                    Application.DoEvents();
                    _loadedSourceReferences = _service.ExtractReferencesFromProject(srcProj, targetBookCodes);
                    _lblStatus.Text = $"Extracted {_loadedSourceReferences.Count} parallel references from '{srcName}'.";
                }
            }
            else if (_loadedSourceReferences.Count == 0)
            {
                LoadSelectedSource();
            }

            if (_loadedSourceReferences.Count == 0)
            {
                MessageBox.Show("No source references available to transfer. Please select an installed Paratext Resource with available references, a source project, or load a reference file.", "No References Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                var bookProposals = _service.ScanBook(proj, bookNum, _loadedSourceReferences, _vernacularMgr, options);
                _currentProposals.AddRange(bookProposals);
            }

            _progressBar.Visible = false;
            PopulateGrid();
            UpdateStatusCounts();
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();

            foreach (var p in _currentProposals)
            {
                int rowIdx = _grid.Rows.Add();
                var row = _grid.Rows[rowIdx];
                row.Tag = p;

                row.Cells["ColApply"].Value = p.IsAccepted;
                row.Cells["ColVerse"].Value = p.ReferenceLocation;
                row.Cells["ColType"].Value = p.ActionType;
                row.Cells["ColProposed"].Value = p.ProposedReference;
                row.Cells["ColSource"].Value = p.SourceReference;
                row.Cells["ColHeading"].Value = p.ExistingHeading;
                row.Cells["ColWarning"].Value = p.Warning;

                if (p.IsDuplicate)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
                else if (p.HasWarning)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 230); // Amber warning
                }
            }

            _grid.CellValueChanged += Grid_CellValueChanged;
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

        private void UpdateStatusCounts()
        {
            int total = _currentProposals.Count;
            int accepted = _currentProposals.Count(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning);
            int duplicates = _currentProposals.Count(p => p.IsDuplicate);
            int warnings = _currentProposals.Count(p => p.HasWarning);

            _lblStatus.Text = $"Total References: {total} | Ready to Transfer: {accepted} | Duplicates: {duplicates} | Warnings: {warnings}";
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
