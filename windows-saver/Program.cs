using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Stackchan Screensaver")]
[assembly: AssemblyDescription("Stack-chan face screensaver for Windows")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace Stackchan {
    internal static class Program {
        [STAThread]
        private static int Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Options options;
            try { options = Options.Parse(args, Path.GetExtension(Application.ExecutablePath).Equals(".scr", StringComparison.OrdinalIgnoreCase)); }
            catch (ArgumentException) { return 2; }
            if (options.Mode == RunMode.Embedded && !Native.IsWindow(options.Parent)) return 2;
            if (options.Mode == RunMode.Configure) {
                ShowConfiguration(options.Parent);
                return 0;
            }
            using (SaverSession session = new SaverSession(options)) Application.Run(session);
            return 0;
        }
        private sealed class Owner : IWin32Window {
            private readonly IntPtr handle;
            internal Owner(IntPtr value) { handle = value; }
            public IntPtr Handle { get { return handle; } }
        }
        private static void ShowConfiguration(IntPtr parent) {
            using (Form dialog = new Form()) {
                dialog.Text = "Stackchan スクリーンセーバー";
                dialog.ClientSize = new System.Drawing.Size(510, 245);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false; dialog.MinimizeBox = false;
                dialog.StartPosition = FormStartPosition.CenterScreen;
                Label text = new Label { AutoSize = false, Left = 20, Top = 18, Width = 470, Height = 160,
                    Text = "スタックちゃんの顔が、PCの状態で変わります。\n\nCPU使用率 > 70% → 怒り / AC接続 → 嬉しい\n電池 < 20% → 悲しい / 表示5分超 → 眠い\n\n開始までの時間と復帰時のサインインは、Windowsの\n「スクリーン セーバーの設定」で指定してください。\n\nプレビューは普通のウィンドウで開きます。" };
                Button preview = new Button { Text = "ウィンドウで試す", Left = 20, Top = 195, Width = 200, Height = 32 };
                preview.Click += delegate {
                    using (SaverWindow window = new SaverWindow(RunMode.Preview, IntPtr.Zero)) window.ShowDialog(dialog);
                };
                Button close = new Button { Text = "閉じる", Left = 380, Top = 195, Width = 110, Height = 32, DialogResult = DialogResult.OK };
                dialog.Controls.AddRange(new Control[] { text, preview, close });
                dialog.AcceptButton = close; dialog.CancelButton = close;
                if (parent != IntPtr.Zero && Native.IsWindow(parent)) dialog.ShowDialog(new Owner(parent));
                else dialog.ShowDialog();
            }
        }
    }
}
