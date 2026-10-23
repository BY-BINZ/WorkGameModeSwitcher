using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace OneKeyModeManager
{
    public class AppEntry
    {
        public string Name { get; set; }
        public string Note { get; set; }
        public string Path { get; set; }
        public string ProcessNames { get; set; }
    }

    public class ModeRule
    {
        public string AppName { get; set; }
        public string Action { get; set; }
    }

    public class ModeProfile
    {
        public string Name { get; set; }
        public List<ModeRule> Rules { get; set; }

        public ModeProfile()
        {
            Rules = new List<ModeRule>();
        }
    }

    public class AppConfig
    {
        public List<AppEntry> Apps { get; set; }
        public List<ModeProfile> Modes { get; set; }

        public AppConfig()
        {
            Apps = new List<AppEntry>();
            Modes = new List<ModeProfile>();
        }
    }

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

    internal sealed class MainForm : Form
    {
        private readonly string configPath;
        private AppConfig config;
        private BindingList<AppEntry> apps;
        private BindingList<ModeProfile> modes;
        private ComboBox modeBox;
        private DataGridView grid;
        private Label statusLabel;
        private bool refreshingGrid;

        public MainForm()
        {
            Text = "模式切换器";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);
            Size = new Size(1120, 680);
            Font = new Font("Microsoft YaHei UI", 9.5F);
            BackColor = Color.FromArgb(243, 246, 250);

            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modes.config.xml");
            LoadOrCreateConfig();
            BuildUi();
            RefreshModeList();
            RefreshGrid();
            EnableDropAdd(this);
        }

        private void BuildUi()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 5, ColumnCount = 1 };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(main);

            var title = new Label
            {
                Text = "模式切换器",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 31, 38)
            };
            main.Controls.Add(title, 0, 0);

            var hero = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, Padding = new Padding(0, 3, 0, 8) };
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            main.Controls.Add(hero, 0, 1);

            var workButton = MakeHeroButton("工作模式", "", Color.FromArgb(38, 115, 255), Color.FromArgb(24, 168, 120));
            workButton.Click += delegate { ApplyModeByName("工作模式"); };
            hero.Controls.Add(workButton, 0, 0);

            var gameButton = MakeHeroButton("游戏模式", "", Color.FromArgb(38, 42, 54), Color.FromArgb(92, 107, 192));
            gameButton.Click += delegate { ApplyModeByName("游戏模式"); };
            hero.Controls.Add(gameButton, 1, 0);

            var retouchButton = MakeHeroButton("修图模式", "", Color.FromArgb(144, 82, 255), Color.FromArgb(40, 170, 220));
            retouchButton.Click += delegate { ApplyModeByName("修图模式"); };
            hero.Controls.Add(retouchButton, 2, 0);

            var editButton = MakeHeroButton("剪辑模式", "", Color.FromArgb(218, 88, 72), Color.FromArgb(232, 156, 72));
            editButton.Click += delegate { ApplyModeByName("剪辑模式"); };
            hero.Controls.Add(editButton, 3, 0);

            var summaryPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2, 0, 10, 0), BackColor = Color.White };
            summaryPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(224, 230, 238)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, summaryPanel.Width - 1, summaryPanel.Height - 1);
                }
            };
            hero.Controls.Add(summaryPanel, 4, 0);

            statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 86, 98)
            };
            summaryPanel.Controls.Add(statusLabel);

            var refreshTopButton = MakeHeroButton("刷新状态", "", Color.FromArgb(88, 105, 128), Color.FromArgb(73, 142, 166));
            refreshTopButton.Click += delegate { RefreshRuntimeStatus(); };
            hero.Controls.Add(refreshTopButton, 5, 0);

            var modeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, Padding = new Padding(0, 3, 0, 3) };
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            main.Controls.Add(modeRow, 0, 2);

            modeRow.Controls.Add(MakeLabel("其他模式"), 0, 0);
            modeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9.5F) };
            modeBox.SelectedIndexChanged += delegate { RefreshGrid(); };
            modeRow.Controls.Add(modeBox, 1, 0);

            var applyButton = MakeButton("应用");
            applyButton.Click += delegate { ApplyCurrentMode(); };
            modeRow.Controls.Add(applyButton, 2, 0);

            var addModeButton = MakeButton("添加模式");
            addModeButton.Click += delegate { AddMode(); };
            modeRow.Controls.Add(addModeButton, 3, 0);

            var renameModeButton = MakeButton("改名");
            renameModeButton.Click += delegate { RenameMode(); };
            modeRow.Controls.Add(renameModeButton, 4, 0);

            var deleteModeButton = MakeButton("删除模式");
            deleteModeButton.Click += delegate { DeleteMode(); };
            modeRow.Controls.Add(deleteModeButton, 5, 0);

            var importButton = MakeButton("导入配置");
            importButton.Click += delegate { ImportConfig(); };
            modeRow.Controls.Add(importButton, 7, 0);

            var exportButton = MakeButton("导出配置");
            exportButton.Click += delegate { ExportConfig(); };
            modeRow.Controls.Add(exportButton, 8, 0);

            var saveButton = MakeButton("保存");
            saveButton.Click += delegate { SaveConfig(); MessageBox.Show(this, "配置已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            modeRow.Controls.Add(saveButton, 9, 0);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(229, 234, 241),
                RowTemplate = { Height = 31 },
                ColumnHeadersHeight = 32,
                EnableHeadersVisualStyles = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(43, 49, 58);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F);
            grid.DefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 234, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(24, 31, 42);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 253);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "软件", Name = "Name", ReadOnly = true, Width = 142 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "程序路径", Name = "Path", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "进程名", Name = "ProcessNames", ReadOnly = true, Width = 145 });
            var actionColumn = new DataGridViewComboBoxColumn { HeaderText = "当前模式动作", Name = "Action", Width = 128, FlatStyle = FlatStyle.Flat };
            actionColumn.Items.AddRange("不处理", "启动", "关闭");
            grid.Columns.Add(actionColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", Name = "Status", ReadOnly = true, Width = 86 });
            grid.CellValueChanged += delegate { SaveGridRules(); };
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellDoubleClick += delegate { EditSelectedApp(); };
            grid.CellMouseDown += ShowNoteOnNameRightClick;
            EnableDropAdd(grid);
            main.Controls.Add(grid, 0, 3);

            var bottomRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, Padding = new Padding(0, 4, 0, 0) };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            main.Controls.Add(bottomRow, 0, 4);

            var addAppButton = MakeButton("添加软件");
            addAppButton.Click += delegate { AddApp(); };
            bottomRow.Controls.Add(addAppButton, 0, 0);

            var editAppButton = MakeButton("修改软件");
            editAppButton.Click += delegate { EditSelectedApp(); };
            bottomRow.Controls.Add(editAppButton, 1, 0);

            var deleteAppButton = MakeButton("删除软件");
            deleteAppButton.Click += delegate { DeleteSelectedApp(); };
            bottomRow.Controls.Add(deleteAppButton, 2, 0);

            var startButton = MakeButton("启动选中");
            startButton.Click += delegate { StartSelected(); };
            bottomRow.Controls.Add(startButton, 3, 0);

            var stopButton = MakeButton("关闭选中");
            stopButton.Click += delegate { StopSelected(); };
            bottomRow.Controls.Add(stopButton, 4, 0);

            var hint = new Label
            {
                Text = "双击软件行可修改路径；下拉动作会保存到当前模式。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(89, 96, 106)
            };
            bottomRow.Controls.Add(hint, 5, 0);

            var openConfigButton = MakeButton("打开配置所在文件夹");
            openConfigButton.Click += delegate { Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory); };
            bottomRow.Controls.Add(openConfigButton, 6, 0);

            FormClosing += delegate { SaveGridRules(); SaveConfig(); };
        }

        private void EnableDropAdd(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += delegate(object sender, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            control.DragDrop += delegate(object sender, DragEventArgs e)
            {
                var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dropped == null || dropped.Length == 0) return;
                AddAppsFromDroppedFiles(dropped);
            };
        }

        private static Label MakeLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(46, 50, 56) };
        }

        private static Button MakeButton(string text)
        {
            var button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(43, 50, 60), Font = new Font("Microsoft YaHei UI", 9F), TextAlign = ContentAlignment.MiddleCenter, UseCompatibleTextRendering = true };
            button.Padding = new Padding(0);
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 229);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 244, 252);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 236, 250);
            return button;
        }

        private static Button MakeHeroButton(string title, string subtitle, Color startColor, Color endColor)
        {
            return new HeroButton(title, subtitle, startColor, endColor) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        }

        private void LoadOrCreateConfig()
        {
            config = TryReadConfig(configPath) ?? CreateDefaultConfig();
            EnsureEssentialModes(config);
            apps = new BindingList<AppEntry>(config.Apps);
            modes = new BindingList<ModeProfile>(config.Modes);
            SaveConfig();
        }

        private static AppConfig TryReadConfig(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return (AppConfig)new XmlSerializer(typeof(AppConfig)).Deserialize(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        private AppConfig CreateDefaultConfig()
        {
            var cfg = new AppConfig();
            cfg.Apps.Add(new AppEntry { Name = "QuickLook", Note = "空格预览", Path = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\QuickLook\QuickLook.exe"), ProcessNames = "QuickLook" });
            cfg.Apps.Add(new AppEntry { Name = "滴答清单", Note = "待办清单", Path = @"D:\Program Files (x86)\滴答清单\TickTick.exe", ProcessNames = "TickTick" });
            cfg.Apps.Add(new AppEntry { Name = "WGestures", Note = "鼠标手势", Path = @"C:\Program Files (x86)\WGestures\WGestures.exe", ProcessNames = "WGestures" });
            cfg.Apps.Add(new AppEntry { Name = "Listary", Note = "快速搜索", Path = @"C:\Program Files\Listary\Listary.exe", ProcessNames = "Listary" });
            cfg.Apps.Add(new AppEntry { Name = "LocalSend", Note = "局域网传文件", Path = @"D:\Program Files\LocalSend\localsend_app.exe", ProcessNames = "localsend_app, LocalSend" });
            cfg.Apps.Add(new AppEntry { Name = "Eagle", Note = "素材管理", Path = @"D:\Program Files\Eagle\Eagle.exe", ProcessNames = "Eagle" });
            cfg.Apps.Add(new AppEntry { Name = "QQ", Note = "聊天", Path = @"D:\Program Files\Tencent\QQNT\QQ.exe", ProcessNames = "QQ" });
            cfg.Apps.Add(new AppEntry { Name = "豆包", Note = "AI 助手", Path = @"D:\Program Files\Doubao\Doubao.exe", ProcessNames = "Doubao" });
            cfg.Apps.Add(new AppEntry { Name = "PixPin", Note = "截图贴图", Path = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\PixPin\PixPin.exe"), ProcessNames = "PixPin" });
            cfg.Apps.Add(new AppEntry { Name = "微信", Note = "聊天", Path = @"D:\Program Files\Tencent\Weixin\Weixin.exe", ProcessNames = "Weixin, WeChat" });
            cfg.Apps.Add(new AppEntry { Name = "AppLauncher.exe", Note = "应用启动器", Path = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\AppLauncher\AppLauncher.exe"), ProcessNames = "AppLauncher" });

            cfg.Modes.Add(MakeMode("工作模式", cfg.Apps.Select(a => a.Name), new string[0]));
            cfg.Modes.Add(MakeMode("剪辑模式", new[] { "Eagle", "PixPin", "LocalSend", "豆包", "AppLauncher.exe" }, new[] { "QQ", "微信" }));
            cfg.Modes.Add(MakeMode("修图模式", new[] { "Eagle", "PixPin", "QuickLook", "LocalSend" }, new[] { "QQ", "微信", "滴答清单" }));
            cfg.Modes.Add(MakeMode("游戏模式", new string[0], cfg.Apps.Select(a => a.Name)));
            cfg.Modes.Add(MakeMode("魔兽世界模式", new string[0], cfg.Apps.Select(a => a.Name)));
            cfg.Modes.Add(MakeMode("影音模式", new[] { "LocalSend" }, new[] { "WGestures", "Listary", "滴答清单", "QQ", "微信", "豆包", "PixPin" }));
            return cfg;
        }

        private static void EnsureEssentialModes(AppConfig cfg)
        {
            if (cfg.Apps == null) cfg.Apps = new List<AppEntry>();
            if (cfg.Modes == null) cfg.Modes = new List<ModeProfile>();
            if (!cfg.Modes.Any(m => SameName(m.Name, "工作模式")))
            {
                cfg.Modes.Insert(0, MakeMode("工作模式", cfg.Apps.Select(a => a.Name), new string[0]));
            }
            if (!cfg.Modes.Any(m => SameName(m.Name, "游戏模式")))
            {
                var wowMode = cfg.Modes.FirstOrDefault(m => SameName(m.Name, "魔兽世界模式"));
                if (wowMode != null)
                {
                    cfg.Modes.Insert(Math.Min(1, cfg.Modes.Count), new ModeProfile
                    {
                        Name = "游戏模式",
                        Rules = wowMode.Rules.Select(r => new ModeRule { AppName = r.AppName, Action = r.Action }).ToList()
                    });
                }
                else
                {
                    cfg.Modes.Insert(Math.Min(1, cfg.Modes.Count), MakeMode("游戏模式", new string[0], cfg.Apps.Select(a => a.Name)));
                }
            }
        }

        private static ModeProfile MakeMode(string name, IEnumerable<string> starts, IEnumerable<string> stops)
        {
            var mode = new ModeProfile { Name = name };
            foreach (var appName in starts) mode.Rules.Add(new ModeRule { AppName = appName, Action = "启动" });
            foreach (var appName in stops) mode.Rules.Add(new ModeRule { AppName = appName, Action = "关闭" });
            return mode;
        }

        private void SaveConfig()
        {
            try
            {
                config.Apps = apps.ToList();
                config.Modes = modes.ToList();
                using (var stream = File.Create(configPath))
                {
                    new XmlSerializer(typeof(AppConfig)).Serialize(stream, config);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存配置失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ModeProfile CurrentMode
        {
            get { return modeBox.SelectedItem as ModeProfile; }
        }

        private AppEntry SelectedApp
        {
            get
            {
                if (grid.CurrentRow == null || grid.CurrentRow.Tag == null) return null;
                return grid.CurrentRow.Tag as AppEntry;
            }
        }

        private void RefreshModeList()
        {
            var selectedName = CurrentMode == null ? null : CurrentMode.Name;
            modeBox.DataSource = null;
            modeBox.DisplayMember = "Name";
            modeBox.DataSource = modes;
            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                for (var i = 0; i < modeBox.Items.Count; i++)
                {
                    if (((ModeProfile)modeBox.Items[i]).Name == selectedName)
                    {
                        modeBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (modeBox.SelectedIndex < 0 && modeBox.Items.Count > 0) modeBox.SelectedIndex = 0;
        }

        private void RefreshGrid()
        {
            if (grid == null) return;
            refreshingGrid = true;
            grid.Rows.Clear();
            var mode = CurrentMode;
            foreach (var app in apps)
            {
                var rowIndex = grid.Rows.Add(app.Name, app.Path, app.ProcessNames, GetAction(mode, app.Name), "");
                grid.Rows[rowIndex].Tag = app;
            }
            refreshingGrid = false;
            RefreshRuntimeStatus();
        }

        private static string GetAction(ModeProfile mode, string appName)
        {
            if (mode == null) return "不处理";
            var rule = mode.Rules.FirstOrDefault(r => SameName(r.AppName, appName));
            if (rule == null || string.IsNullOrWhiteSpace(rule.Action)) return "不处理";
            return rule.Action;
        }

        private void SaveGridRules()
        {
            if (refreshingGrid) return;
            var mode = CurrentMode;
            if (mode == null || grid == null) return;
            mode.Rules.Clear();
            foreach (DataGridViewRow row in grid.Rows)
            {
                var app = row.Tag as AppEntry;
                if (app == null) continue;
                var action = Convert.ToString(row.Cells["Action"].Value);
                if (action == "启动" || action == "关闭")
                {
                    mode.Rules.Add(new ModeRule { AppName = app.Name, Action = action });
                }
            }
            SaveConfig();
        }

        private void AddMode()
        {
            using (var dialog = new TextInputForm("添加模式", "模式名称", "新模式"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (modes.Any(m => SameName(m.Name, dialog.Value)))
                {
                    MessageBox.Show(this, "已经有同名模式。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                modes.Add(new ModeProfile { Name = dialog.Value });
                RefreshModeList();
                modeBox.SelectedItem = modes.Last();
                SaveConfig();
            }
        }

        private void RenameMode()
        {
            var mode = CurrentMode;
            if (mode == null) return;
            using (var dialog = new TextInputForm("模式改名", "模式名称", mode.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (modes.Any(m => !ReferenceEquals(m, mode) && SameName(m.Name, dialog.Value)))
                {
                    MessageBox.Show(this, "已经有同名模式。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                mode.Name = dialog.Value;
                RefreshModeList();
                SaveConfig();
            }
        }

        private void DeleteMode()
        {
            var mode = CurrentMode;
            if (mode == null) return;
            if (modes.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个模式。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "确定删除“" + mode.Name + "”吗？", "删除模式", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            modes.Remove(mode);
            RefreshModeList();
            SaveConfig();
        }

        private void AddApp()
        {
            var app = new AppEntry();
            using (var dialog = new EditAppForm(app))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                apps.Add(app);
                RefreshGrid();
                SaveConfig();
            }
        }

        private void AddAppsFromDroppedFiles(IEnumerable<string> droppedFiles)
        {
            var added = 0;
            var skipped = 0;
            foreach (var droppedFile in droppedFiles)
            {
                var app = AppEntryFromDroppedFile(droppedFile);
                if (app == null)
                {
                    skipped++;
                    continue;
                }

                var existing = apps.FirstOrDefault(a => SameName(a.Name, app.Name) || SamePath(a.Path, app.Path));
                if (existing != null)
                {
                    existing.Name = app.Name;
                    existing.Path = app.Path;
                    existing.ProcessNames = app.ProcessNames;
                    if (string.IsNullOrWhiteSpace(existing.Note)) existing.Note = app.Note;
                }
                else
                {
                    apps.Add(app);
                    added++;
                }
            }

            RefreshGrid();
            SaveConfig();
            if (added > 0 || skipped > 0)
            {
                statusLabel.Text = skipped > 0 ? "已添加 " + added + " 个，跳过 " + skipped + " 个" : "已添加 " + added + " 个软件";
            }
        }

        private AppEntry AppEntryFromDroppedFile(string droppedFile)
        {
            if (string.IsNullOrWhiteSpace(droppedFile)) return null;
            var targetPath = droppedFile;
            var extension = Path.GetExtension(droppedFile).ToLowerInvariant();

            if (extension == ".lnk")
            {
                targetPath = ResolveShortcutTarget(droppedFile);
            }
            else if (extension == ".url")
            {
                targetPath = ResolveUrlShortcutTarget(droppedFile);
            }

            if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath)) return null;
            if (!SameName(Path.GetExtension(targetPath), ".exe")) return null;

            var processName = Path.GetFileNameWithoutExtension(targetPath);
            var displayName = Path.GetFileNameWithoutExtension(droppedFile);
            if (string.IsNullOrWhiteSpace(displayName) || extension == ".exe") displayName = processName;

            return new AppEntry
            {
                Name = displayName,
                Note = "",
                Path = targetPath,
                ProcessNames = processName
            };
        }

        private static string ResolveShortcutTarget(string shortcutPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                var shell = Activator.CreateInstance(shellType);
                var shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                var target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null));
                return Environment.ExpandEnvironmentVariables(target ?? "");
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveUrlShortcutTarget(string shortcutPath)
        {
            try
            {
                foreach (var line in File.ReadAllLines(shortcutPath))
                {
                    if (!line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) continue;
                    var url = line.Substring(4).Trim();
                    Uri uri;
                    if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.IsFile) return uri.LocalPath;
                }
            }
            catch { }
            return null;
        }

        private void EditSelectedApp()
        {
            var selected = SelectedApp;
            if (selected == null) return;
            var oldName = selected.Name;
            var copy = new AppEntry { Name = selected.Name, Note = selected.Note, Path = selected.Path, ProcessNames = selected.ProcessNames };
            using (var dialog = new EditAppForm(copy))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                selected.Name = copy.Name;
                selected.Note = copy.Note;
                selected.Path = copy.Path;
                selected.ProcessNames = copy.ProcessNames;
                if (!SameName(oldName, selected.Name))
                {
                    foreach (var mode in modes)
                    {
                        foreach (var rule in mode.Rules.Where(r => SameName(r.AppName, oldName)))
                        {
                            rule.AppName = selected.Name;
                        }
                    }
                }
                RefreshGrid();
                SaveConfig();
            }
        }

        private void ShowNoteOnNameRightClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name != "Name") return;

            grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var app = grid.Rows[e.RowIndex].Tag as AppEntry;
            if (app == null) return;

            var menu = new ContextMenuStrip();
            var noteText = string.IsNullOrWhiteSpace(app.Note) ? "暂无备注" : app.Note.Trim();
            var noteItem = new ToolStripMenuItem(noteText) { Enabled = false };
            var editItem = new ToolStripMenuItem("修改备注");
            editItem.Click += delegate { EditSelectedApp(); };
            menu.Items.Add(noteItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(editItem);
            menu.Show(grid, grid.PointToClient(Cursor.Position));
        }

        private void DeleteSelectedApp()
        {
            var selected = SelectedApp;
            if (selected == null) return;
            if (MessageBox.Show(this, "确定删除“" + selected.Name + "”吗？", "删除软件", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            apps.Remove(selected);
            foreach (var mode in modes) mode.Rules.RemoveAll(r => SameName(r.AppName, selected.Name));
            RefreshGrid();
            SaveConfig();
        }

        private void ImportConfig()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "导入配置";
                dialog.Filter = "配置文件 (*.xml)|*.xml|所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var imported = TryReadConfig(dialog.FileName);
                if (imported == null)
                {
                    MessageBox.Show(this, "这个配置文件无法读取。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                config = imported;
                EnsureEssentialModes(config);
                apps = new BindingList<AppEntry>(config.Apps ?? new List<AppEntry>());
                modes = new BindingList<ModeProfile>(config.Modes ?? new List<ModeProfile>());
                if (modes.Count == 0) modes.Add(new ModeProfile { Name = "默认模式" });
                RefreshModeList();
                RefreshGrid();
                SaveConfig();
            }
        }

        private void ExportConfig()
        {
            SaveGridRules();
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "导出配置";
                dialog.Filter = "配置文件 (*.xml)|*.xml";
                dialog.FileName = "模式切换器配置.xml";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.Copy(configPath, dialog.FileName, true);
                MessageBox.Show(this, "配置已导出。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ApplyCurrentMode()
        {
            SaveGridRules();
            ApplyMode(CurrentMode);
        }

        private void ApplyModeByName(string modeName)
        {
            SaveGridRules();
            var mode = modes.FirstOrDefault(m => SameName(m.Name, modeName));
            if (mode == null && SameName(modeName, "游戏模式")) mode = modes.FirstOrDefault(m => SameName(m.Name, "魔兽世界模式"));
            if (mode == null)
            {
                MessageBox.Show(this, "没有找到“" + modeName + "”，可以在“添加模式”里创建。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            modeBox.SelectedItem = mode;
            ApplyMode(mode);
        }

        private void ApplyMode(ModeProfile mode)
        {
            if (mode == null) return;
            if (mode.Rules.Any(r => r.Action == "关闭"))
            {
                if (MessageBox.Show(this, "这个模式会关闭部分软件，请确认未保存内容已处理。继续吗？", "应用模式", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }
            SetBusy(true);
            try
            {
                var startList = new List<AppEntry>();
                var stopList = new List<AppEntry>();
                foreach (var rule in mode.Rules)
                {
                    var app = apps.FirstOrDefault(a => SameName(a.Name, rule.AppName));
                    if (app == null) continue;
                    if (rule.Action == "启动") startList.Add(app);
                    if (rule.Action == "关闭") stopList.Add(app);
                }
                if (stopList.Count > 0)
                {
                    StopAppsFast(stopList);
                }
                foreach (var app in startList)
                {
                    StartApp(app);
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(80);
                }
                statusLabel.Text = "已应用：" + mode.Name;
            }
            finally
            {
                SetBusy(false);
                RefreshRuntimeStatus();
            }
        }

        private void StartSelected()
        {
            var selected = SelectedApp;
            if (selected == null) return;
            StartApp(selected);
            RefreshRuntimeStatus();
        }

        private void StopSelected()
        {
            var selected = SelectedApp;
            if (selected == null) return;
            StopAppsFast(new[] { selected });
            RefreshRuntimeStatus();
        }

        private void SetBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            foreach (Control control in Controls) control.Enabled = !busy;
            Enabled = true;
            Application.DoEvents();
        }

        private void StartApp(AppEntry app)
        {
            if (IsRunning(app)) return;
            if (string.IsNullOrWhiteSpace(app.Path) || !File.Exists(app.Path))
            {
                MessageBox.Show(this, "找不到“" + app.Name + "”的程序路径，请修改后再启动。", "未找到程序", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(app.Path) { WorkingDirectory = Path.GetDirectoryName(app.Path), UseShellExecute = true });
        }

        private void StopAppsFast(IEnumerable<AppEntry> appList)
        {
            var processes = appList
                .SelectMany(GetAppProcesses)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow();
                }
                catch { }
            }

            var timeout = DateTime.Now.AddMilliseconds(400);
            while (DateTime.Now < timeout)
            {
                if (processes.All(HasExited)) return;
                Application.DoEvents();
                System.Threading.Thread.Sleep(50);
            }

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch { }
            }
        }

        private static bool HasExited(Process process)
        {
            try { return process.HasExited; }
            catch { return true; }
        }

        private bool IsRunning(AppEntry app)
        {
            return GetAppProcesses(app).Any();
        }

        private IEnumerable<Process> GetAppProcesses(AppEntry app)
        {
            foreach (var name in SplitProcessNames(app.ProcessNames))
            {
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { found = new Process[0]; }
                foreach (var process in found) yield return process;
            }
        }

        private static IEnumerable<string> SplitProcessNames(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;
            foreach (var raw in value.Split(new[] { ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
                if (name.Length > 0) yield return name;
            }
        }

        private void RefreshRuntimeStatus()
        {
            if (grid == null) return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                var app = row.Tag as AppEntry;
                if (app == null) continue;
                var running = IsRunning(app);
                row.Cells["Status"].Value = running ? "运行中" : "未运行";
                row.DefaultCellStyle.ForeColor = running ? Color.FromArgb(24, 128, 72) : Color.FromArgb(62, 68, 76);
            }
        }

        private static bool SameName(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal sealed class HeroButton : Button
    {
        private readonly string title;
        private readonly string subtitle;
        private readonly Color startColor;
        private readonly Color endColor;
        private bool hovering;
        private bool pressing;

        public HeroButton(string title, string subtitle, Color startColor, Color endColor)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.startColor = startColor;
            this.endColor = endColor;
            Text = "";
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressing = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            pressing = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressing = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var inner = new Rectangle(rect.X + 1, rect.Y + 1 + (pressing ? 2 : 0), rect.Width - 2, rect.Height - 2);
            using (var path = RoundedRect(inner, 10))
            using (var brush = new LinearGradientBrush(inner, hovering ? Lighten(startColor, 12) : startColor, hovering ? Lighten(endColor, 12) : endColor, 28F))
            {
                e.Graphics.FillPath(brush, path);
                using (var shine = new SolidBrush(Color.FromArgb(hovering ? 40 : 28, Color.White)))
                {
                    e.Graphics.FillEllipse(shine, inner.Right - 118, inner.Top - 74, 172, 126);
                }
                using (var pen = new Pen(Color.FromArgb(75, Color.White), 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            using (var titleFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold))
            using (var subFont = new Font("Microsoft YaHei UI", 8.8F))
            using (var white = new SolidBrush(Color.White))
            using (var soft = new SolidBrush(Color.FromArgb(220, Color.White)))
            {
                if (string.IsNullOrWhiteSpace(subtitle))
                {
                    using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                    {
                        e.Graphics.DrawString(title, titleFont, white, new RectangleF(18, 0 + (pressing ? 2 : 0), Width - 42, Height), format);
                    }
                }
                else
                {
                    e.Graphics.DrawString(title, titleFont, white, new RectangleF(18, 16 + (pressing ? 2 : 0), Width - 42, 28));
                    e.Graphics.DrawString(subtitle, subFont, soft, new RectangleF(19, 48 + (pressing ? 2 : 0), Width - 38, 28));
                }
            }

            using (var arrowFont = new Font("Segoe UI Symbol", 12F, FontStyle.Bold))
            using (var arrowBrush = new SolidBrush(Color.FromArgb(225, Color.White)))
            {
                e.Graphics.DrawString(">", arrowFont, arrowBrush, new RectangleF(Width - 36, Height - 34 + (pressing ? 2 : 0), 24, 24));
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Lighten(Color color, int amount)
        {
            return Color.FromArgb(color.A, Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));
        }
    }

    internal sealed class EditAppForm : Form
    {
        private readonly AppEntry entry;
        private readonly TextBox nameBox;
        private readonly TextBox noteBox;
        private readonly TextBox pathBox;
        private readonly TextBox processBox;

        public EditAppForm(AppEntry entry)
        {
            this.entry = entry;
            Text = string.IsNullOrWhiteSpace(entry.Name) ? "添加软件" : "修改软件";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(680, 292);
            Font = new Font("Microsoft YaHei UI", 10F);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 6 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(main);

            nameBox = new TextBox { Dock = DockStyle.Fill, Text = entry.Name };
            noteBox = new TextBox { Dock = DockStyle.Fill, Text = entry.Note };
            pathBox = new TextBox { Dock = DockStyle.Fill, Text = entry.Path };
            processBox = new TextBox { Dock = DockStyle.Fill, Text = entry.ProcessNames };

            AddLabel(main, "软件名称", 0);
            main.Controls.Add(nameBox, 1, 0);
            main.SetColumnSpan(nameBox, 2);

            AddLabel(main, "备注", 1);
            main.Controls.Add(noteBox, 1, 1);
            main.SetColumnSpan(noteBox, 2);

            AddLabel(main, "程序路径", 2);
            main.Controls.Add(pathBox, 1, 2);
            var browseButton = new Button { Text = "浏览", Dock = DockStyle.Fill };
            browseButton.Click += delegate { BrowsePath(); };
            main.Controls.Add(browseButton, 2, 2);

            AddLabel(main, "进程名", 3);
            main.Controls.Add(processBox, 1, 3);
            main.SetColumnSpan(processBox, 2);

            var hint = new Label
            {
                Text = "备注会在右键软件名称时显示；进程名可填多个，用逗号分隔。",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(89, 96, 106),
                TextAlign = ContentAlignment.MiddleLeft
            };
            main.Controls.Add(hint, 1, 4);
            main.SetColumnSpan(hint, 2);

            var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
            var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Fill };
            okButton.Click += delegate { ApplyAndValidate(); };
            main.Controls.Add(cancelButton, 1, 5);
            main.Controls.Add(okButton, 2, 5);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static void AddLabel(TableLayoutPanel panel, string text, int row)
        {
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        }

        private void BrowsePath()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择程序";
                dialog.Filter = "程序文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                if (File.Exists(pathBox.Text)) dialog.InitialDirectory = Path.GetDirectoryName(pathBox.Text);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                pathBox.Text = dialog.FileName;
                if (string.IsNullOrWhiteSpace(nameBox.Text)) nameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(processBox.Text)) processBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }

        private void ApplyAndValidate()
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(this, "请填写软件名称。", "需要补充", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrWhiteSpace(pathBox.Text))
            {
                MessageBox.Show(this, "请选择程序路径。", "需要补充", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrWhiteSpace(processBox.Text)) processBox.Text = Path.GetFileNameWithoutExtension(pathBox.Text);
            entry.Name = nameBox.Text.Trim();
            entry.Note = noteBox.Text.Trim();
            entry.Path = pathBox.Text.Trim();
            entry.ProcessNames = processBox.Text.Trim();
        }
    }

    internal sealed class TextInputForm : Form
    {
        private readonly TextBox inputBox;
        public string Value { get; private set; }

        public TextInputForm(string title, string label, string value)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(430, 150);
            Font = new Font("Microsoft YaHei UI", 10F);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 2 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(main);

            main.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            inputBox = new TextBox { Dock = DockStyle.Fill, Text = value };
            main.Controls.Add(inputBox, 1, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 82 };
            var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 82 };
            okButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    MessageBox.Show(this, "名称不能为空。", "需要补充", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                Value = inputBox.Text.Trim();
            };
            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);
            main.Controls.Add(buttons, 1, 1);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }
    }
}

