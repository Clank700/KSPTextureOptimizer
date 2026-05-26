using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace KSPTextureOptimizer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        private readonly TextBox rootBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button scanButton = new Button();
        private readonly Button previewButton = new Button();
        private readonly Button optimizeButton = new Button();
        private readonly Button restoreButton = new Button();
        private readonly Button selectAllFoldersButton = new Button();
        private readonly Button deselectAllFoldersButton = new Button();
        private readonly ComboBox targetCombo = new ComboBox();
        private readonly TreeView folderTree = new TreeView();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox logBox = new TextBox();
        private readonly Label summaryLabel = new Label();
        private readonly CheckBox includeStockBox = new CheckBox();
        private readonly CheckBox includePngTgaBox = new CheckBox();
        private readonly CheckBox includeNpotBox = new CheckBox();
        private readonly CheckBox forceMipmapsBox = new CheckBox();
        private readonly CheckBox preserveTimesBox = new CheckBox();
        private readonly BindingSource binding = new BindingSource();

        private List<TextureItem> items = new List<TextureItem>();
        private bool previewCurrent;

        public MainForm()
        {
            Text = "KSP Universal Texture Optimizer";
            MinimumSize = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;

            string defaultRoot = FindDefaultGameData();
            rootBox.Text = defaultRoot;

            BuildLayout();
            WireEvents();
            LoadFolderTree(false);
        }

        private void BuildLayout()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(10) };
            Controls.Add(top);

            var rootLabel = new Label { Text = "GameData or mod folder", AutoSize = true, Location = new Point(10, 13) };
            top.Controls.Add(rootLabel);
            rootBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            rootBox.Location = new Point(10, 34);
            rootBox.Width = 620;
            top.Controls.Add(rootBox);

            browseButton.Text = "Browse GameData";
            browseButton.Location = new Point(640, 32);
            browseButton.Size = new Size(120, 27);
            top.Controls.Add(browseButton);

            var targetLabel = new Label { Text = "Max res", AutoSize = true, Location = new Point(775, 13) };
            top.Controls.Add(targetLabel);
            targetCombo.Location = new Point(775, 34);
            targetCombo.Width = 90;
            targetCombo.DropDownStyle = ComboBoxStyle.DropDown;
            targetCombo.Items.AddRange(new object[] { "4096", "2048", "1024", "512" });
            targetCombo.Text = "2048";
            top.Controls.Add(targetCombo);

            scanButton.Text = "Scan";
            scanButton.Location = new Point(880, 32);
            scanButton.Size = new Size(78, 27);
            top.Controls.Add(scanButton);

            previewButton.Text = "Preview";
            previewButton.Location = new Point(966, 32);
            previewButton.Size = new Size(78, 27);
            top.Controls.Add(previewButton);

            optimizeButton.Text = "Optimize";
            optimizeButton.Location = new Point(1052, 32);
            optimizeButton.Size = new Size(88, 27);
            optimizeButton.Enabled = false;
            top.Controls.Add(optimizeButton);

            restoreButton.Text = "Restore Backup";
            restoreButton.Location = new Point(10, 62);
            restoreButton.Size = new Size(120, 23);
            top.Controls.Add(restoreButton);

            summaryLabel.Text = "Scan a folder to begin.";
            summaryLabel.AutoSize = true;
            summaryLabel.Location = new Point(145, 66);
            top.Controls.Add(summaryLabel);

            var options = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 4, 10, 4) };
            Controls.Add(options);

            includeStockBox.Text = "Include stock/DLC";
            includeStockBox.AutoSize = true;
            includeStockBox.Location = new Point(10, 8);
            options.Controls.Add(includeStockBox);

            includePngTgaBox.Text = "Include PNG/TGA";
            includePngTgaBox.Checked = true;
            includePngTgaBox.AutoSize = true;
            includePngTgaBox.Location = new Point(145, 8);
            options.Controls.Add(includePngTgaBox);

            includeNpotBox.Text = "Include non-power-of-two DDS";
            includeNpotBox.AutoSize = true;
            includeNpotBox.Location = new Point(275, 8);
            options.Controls.Add(includeNpotBox);

            forceMipmapsBox.Text = "Force mipmaps";
            forceMipmapsBox.AutoSize = true;
            forceMipmapsBox.Location = new Point(485, 8);
            options.Controls.Add(forceMipmapsBox);

            preserveTimesBox.Text = "Preserve timestamps";
            preserveTimesBox.Checked = true;
            preserveTimesBox.AutoSize = true;
            preserveTimesBox.Location = new Point(600, 8);
            options.Controls.Add(preserveTimesBox);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 250 };
            Controls.Add(split);
            split.BringToFront();

            folderTree.Dock = DockStyle.Fill;
            folderTree.CheckBoxes = true;
            var folderPanel = new Panel { Dock = DockStyle.Fill };
            var folderButtons = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 5, 6, 4) };
            selectAllFoldersButton.Text = "Select all";
            selectAllFoldersButton.Location = new Point(6, 5);
            selectAllFoldersButton.Size = new Size(88, 24);
            deselectAllFoldersButton.Text = "Deselect all";
            deselectAllFoldersButton.Location = new Point(100, 5);
            deselectAllFoldersButton.Size = new Size(96, 24);
            folderButtons.Controls.Add(selectAllFoldersButton);
            folderButtons.Controls.Add(deselectAllFoldersButton);
            folderPanel.Controls.Add(folderTree);
            folderPanel.Controls.Add(folderButtons);
            split.Panel1.Controls.Add(folderPanel);

            var rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 430 };
            split.Panel2.Controls.Add(rightSplit);

            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.DataSource = binding;
            rightSplit.Panel1.Controls.Add(grid);
            AddGridColumns();

            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Font = new Font(FontFamily.GenericMonospace, 9);
            rightSplit.Panel2.Controls.Add(logBox);
        }

        private void AddGridColumns()
        {
            AddTextColumn("Checked", "Use", 42, true);
            AddTextColumn("RelativePath", "Path", 360, false);
            AddTextColumn("Format", "Format", 70, false);
            AddTextColumn("Width", "W", 55, false);
            AddTextColumn("Height", "H", 55, false);
            AddTextColumn("MipCount", "Mips", 55, false);
            AddTextColumn("SizeText", "Size", 75, false);
            AddTextColumn("TargetWidth", "Target W", 70, false);
            AddTextColumn("TargetHeight", "Target H", 70, false);
            AddTextColumn("EstimatedText", "Est.", 75, false);
            AddTextColumn("SavingsText", "Save", 55, false);
            AddTextColumn("Status", "Status", 70, false);
            AddTextColumn("Warning", "Warning", 260, false);
        }

        private void AddTextColumn(string property, string title, int width, bool editable)
        {
            DataGridViewColumn col;
            if (property == "Checked")
            {
                col = new DataGridViewCheckBoxColumn();
            }
            else
            {
                col = new DataGridViewTextBoxColumn();
            }
            col.DataPropertyName = property;
            col.HeaderText = title;
            col.Width = width;
            col.ReadOnly = !editable;
            grid.Columns.Add(col);
        }

        private void WireEvents()
        {
            browseButton.Click += delegate { BrowseRoot(); };
            scanButton.Click += delegate { RunScan(); };
            previewButton.Click += delegate { RunPreview(); };
            optimizeButton.Click += delegate { RunOptimize(); };
            restoreButton.Click += delegate { RunRestore(); };
            selectAllFoldersButton.Click += delegate { SetAllFolderChecks(true); };
            deselectAllFoldersButton.Click += delegate { SetAllFolderChecks(false); };
            rootBox.TextChanged += delegate { MarkScanDirty(); };
            includeStockBox.CheckedChanged += delegate { MarkScanDirty(); };
            includePngTgaBox.CheckedChanged += delegate { previewCurrent = false; optimizeButton.Enabled = false; };
            includeNpotBox.CheckedChanged += delegate { previewCurrent = false; optimizeButton.Enabled = false; };
            forceMipmapsBox.CheckedChanged += delegate { previewCurrent = false; optimizeButton.Enabled = false; };
            targetCombo.TextChanged += delegate { previewCurrent = false; optimizeButton.Enabled = false; };
            folderTree.AfterCheck += delegate { MarkScanDirty(); };
            grid.CellValueChanged += delegate { UpdateSummary(); };
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private string FindDefaultGameData()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(appDir);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "GameData");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            string cwdCandidate = Path.Combine(Environment.CurrentDirectory, "GameData");
            return Directory.Exists(cwdCandidate) ? cwdCandidate : Environment.CurrentDirectory;
        }

        private OptimizerOptions ReadOptions()
        {
            int target;
            if (!int.TryParse(targetCombo.Text.Trim(), out target) || target < 64)
                throw new InvalidOperationException("Target max resolution must be a number >= 64.");
            return new OptimizerOptions
            {
                TargetMaxResolution = target,
                IncludeStockDlc = includeStockBox.Checked,
                IncludePngTga = includePngTgaBox.Checked,
                IncludeNonPowerOfTwo = includeNpotBox.Checked,
                ForceMipmaps = forceMipmapsBox.Checked,
                PreserveTimestamps = preserveTimesBox.Checked
            };
        }

        private void BrowseRoot()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select GameData or a specific mod folder";
                dialog.SelectedPath = Directory.Exists(rootBox.Text) ? rootBox.Text : Environment.CurrentDirectory;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    rootBox.Text = dialog.SelectedPath;
                    LoadFolderTree(false);
                }
            }
        }

        private void LoadFolderTree(bool preserveChecks)
        {
            var previous = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (preserveChecks)
            {
                foreach (TreeNode node in folderTree.Nodes)
                {
                    previous[node.Text] = node.Checked;
                }
            }

            folderTree.Nodes.Clear();
            string root = rootBox.Text.Trim();
            if (!Directory.Exists(root)) return;
            foreach (string folder in TextureScanner.ListTopLevelFolders(root))
            {
                bool isChecked = true;
                if (preserveChecks && previous.ContainsKey(folder))
                {
                    isChecked = previous[folder];
                }
                folderTree.Nodes.Add(new TreeNode(folder) { Checked = isChecked });
            }
        }

        private HashSet<string> SelectedTopFolders()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TreeNode node in folderTree.Nodes)
            {
                if (node.Checked) set.Add(node.Text);
            }
            return set;
        }

        private void SetAllFolderChecks(bool isChecked)
        {
            folderTree.BeginUpdate();
            try
            {
                foreach (TreeNode node in folderTree.Nodes)
                {
                    node.Checked = isChecked;
                }
            }
            finally
            {
                folderTree.EndUpdate();
            }
            previewCurrent = false;
            optimizeButton.Enabled = false;
        }

        private void MarkScanDirty()
        {
            previewCurrent = false;
            optimizeButton.Enabled = false;
        }

        private void RunScan()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                logBox.Clear();
                string root = rootBox.Text.Trim();
                if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
                LoadFolderTree(true);
                Log("Scanning " + root);
                items = TextureScanner.Scan(root, SelectedTopFolders(), ReadOptions());
                binding.DataSource = items;
                previewCurrent = false;
                optimizeButton.Enabled = false;
                Log("Scanned " + items.Count + " texture/container files.");
                UpdateSummary();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RunPreview()
        {
            try
            {
                RunScan();
                TextureScanner.Preview(items, ReadOptions());
                binding.ResetBindings(false);
                previewCurrent = true;
                optimizeButton.Enabled = items.Exists(delegate(TextureItem i) { return i.Checked && i.IsOptimizable; });
                UpdateSummary();
                Log("Preview complete. Review checked rows before optimizing.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RunOptimize()
        {
            try
            {
                if (!previewCurrent) throw new InvalidOperationException("Run Preview before Optimize.");
                if (OptimizerEngine.IsKspRunning()) throw new InvalidOperationException("KSP_x64.exe is running. Close KSP before optimizing.");

                int selected = 0;
                foreach (TextureItem item in items)
                    if (item.Checked && item.IsOptimizable) selected++;
                if (selected == 0) throw new InvalidOperationException("No ready textures are selected.");

                DialogResult confirm = MessageBox.Show(this,
                    "This will back up and replace " + selected + " texture files in place.\r\n\r\nContinue?",
                    "Confirm optimization",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                Cursor = Cursors.WaitCursor;
                var engine = new OptimizerEngine(AppDomain.CurrentDomain.BaseDirectory);
                Manifest manifest = engine.Optimize(rootBox.Text.Trim(), items, ReadOptions(), Log);
                Log("Optimized " + manifest.files.Count + " files.");
                MessageBox.Show(this, "Optimization complete.\r\nRun ID: " + manifest.runId, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RunScan();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RunRestore()
        {
            try
            {
                if (OptimizerEngine.IsKspRunning()) throw new InvalidOperationException("KSP_x64.exe is running. Close KSP before restoring backups.");
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select manifest.json to restore";
                    dialog.Filter = "Manifest files|manifest.json|JSON files|*.json|All files|*.*";
                    string runs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runs");
                    if (Directory.Exists(runs)) dialog.InitialDirectory = runs;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    DialogResult confirm = MessageBox.Show(this,
                        "Restore originals from this manifest?\r\n\r\nFiles changed after optimization will be skipped.",
                        "Confirm restore",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes) return;

                    Cursor = Cursors.WaitCursor;
                    var engine = new OptimizerEngine(AppDomain.CurrentDomain.BaseDirectory);
                    RestoreResult result = engine.Restore(dialog.FileName);
                    Log("Restored " + result.Restored.Count + " files.");
                    foreach (string skipped in result.Skipped) Log("Skipped restore: " + skipped);
                    MessageBox.Show(this,
                        "Restored: " + result.Restored.Count + "\r\nSkipped: " + result.Skipped.Count,
                        "Restore complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    RunScan();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateSummary()
        {
            long original = 0;
            long estimated = 0;
            int ready = 0;
            int checkedCount = 0;
            foreach (TextureItem item in items)
            {
                if (item.IsOptimizable) ready++;
                if (item.Checked && item.IsOptimizable)
                {
                    checkedCount++;
                    original += item.SizeBytes;
                    estimated += item.EstimatedBytes;
                }
            }
            long savings = Math.Max(0, original - estimated);
            summaryLabel.Text = items.Count + " scanned, " + ready + " ready, " + checkedCount + " selected, estimated savings " + TextureItem.FormatBytes(savings) + ".";
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private void ShowError(Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "KSP Texture Optimizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
