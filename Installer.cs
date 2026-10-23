using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WorkGameModeSwitcherSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox installPathBox;
        private readonly CheckBox desktopBox;
        private readonly CheckBox startMenuBox;
        private readonly Label statusLabel;

        public SetupForm()
        {
            Text = "工作游戏模式一键切换 安装程序";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(620, 270);
            Font = new Font("Microsoft YaHei UI", 10F);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 6 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(main);

            var title = new Label { Text = "安装工作游戏模式一键切换", Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            main.Controls.Add(title, 0, 0);
            main.SetColumnSpan(title, 3);

            main.Controls.Add(new Label { Text = "安装位置", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            installPathBox = new TextBox { Dock = DockStyle.Fill, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WorkGameModeSwitcher") };
            main.Controls.Add(installPathBox, 1, 1);
            var browseButton = new Button { Text = "浏览", Dock = DockStyle.Fill };
            browseButton.Click += delegate { BrowseInstallPath(); };
            main.Controls.Add(browseButton, 2, 1);

            desktopBox = new CheckBox { Text = "创建桌面图标", Dock = DockStyle.Fill, Checked = true };
            startMenuBox = new CheckBox { Text = "创建开始菜单入口", Dock = DockStyle.Fill, Checked = true };
            main.Controls.Add(desktopBox, 1, 2);
            main.Controls.Add(startMenuBox, 1, 3);

            statusLabel = new Label { Text = "安装版会保留你的配置文件，升级时不会覆盖现有 modes.config.xml。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(90, 96, 106) };
            main.Controls.Add(statusLabel, 1, 4);
            main.SetColumnSpan(statusLabel, 2);

            var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
            var installButton = new Button { Text = "安装", Dock = DockStyle.Fill };
            installButton.Click += delegate { Install(); };
            main.Controls.Add(cancelButton, 1, 5);
            main.Controls.Add(installButton, 2, 5);
        }

        private void BrowseInstallPath()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择安装位置";
                dialog.SelectedPath = installPathBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) installPathBox.Text = dialog.SelectedPath;
            }
        }

        private void Install()
        {
            try
            {
                var sourceDir = AppDomain.CurrentDomain.BaseDirectory;
                var sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v8.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v7.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v6.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v5.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v4.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v3.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher-v2.exe");
                if (!File.Exists(sourceExe)) sourceExe = Path.Combine(sourceDir, "WorkGameModeSwitcher.exe");
                if (!File.Exists(sourceExe))
                {
                    MessageBox.Show(this, "安装包不完整，找不到主程序。", "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var targetDir = installPathBox.Text.Trim();
                Directory.CreateDirectory(targetDir);
                var targetExe = Path.Combine(targetDir, "WorkGameModeSwitcher.exe");
                File.Copy(sourceExe, targetExe, true);

                var sourceConfig = Path.Combine(sourceDir, "modes.config.xml");
                var targetConfig = Path.Combine(targetDir, "modes.config.xml");
                if (File.Exists(sourceConfig) && !File.Exists(targetConfig)) File.Copy(sourceConfig, targetConfig);

                var uninstall = Path.Combine(targetDir, "卸载工作游戏模式一键切换.bat");
                File.WriteAllText(uninstall, "@echo off\r\ncd /d \"%LOCALAPPDATA%\\Programs\"\r\nrmdir /s /q \"WorkGameModeSwitcher\"\r\n", System.Text.Encoding.Default);

                if (desktopBox.Checked)
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateUrlShortcut(Path.Combine(desktop, "工作游戏模式一键切换.url"), targetExe);
                }
                if (startMenuBox.Checked)
                {
                    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "工作游戏模式一键切换");
                    Directory.CreateDirectory(startMenu);
                    CreateUrlShortcut(Path.Combine(startMenu, "工作游戏模式一键切换.url"), targetExe);
                    CreateUrlShortcut(Path.Combine(startMenu, "卸载工作游戏模式一键切换.url"), uninstall);
                }

                statusLabel.Text = "安装完成。";
                if (MessageBox.Show(this, "安装完成，要现在打开吗？", "完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start(targetExe);
                }
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "安装失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CreateUrlShortcut(string shortcutPath, string targetPath)
        {
            var uri = new Uri(targetPath).AbsoluteUri;
            var content = "[InternetShortcut]\r\nURL=" + uri + "\r\nIconFile=" + targetPath + "\r\nIconIndex=0\r\n";
            File.WriteAllText(shortcutPath, content, System.Text.Encoding.Default);
        }
    }
}

