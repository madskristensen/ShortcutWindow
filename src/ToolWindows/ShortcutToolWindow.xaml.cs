using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;

namespace ShortcutWindow
{
    public partial class ShortcutToolWindow : UserControl
    {
        private readonly DTE2 _dte;
        private CommandEvents _events;
        private readonly Key[] _keys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift];

        public ShortcutToolWindow(DTE2 dte, General settings)
        {
            _dte = dte;

            General.Saved += OnGeneralSettingsSaved;

            InitializeComponent();
            SetFontSize(settings);
        }

        private void OnGeneralSettingsSaved(General settings)
        {
            SetFontSize(settings);
        }

        protected override void OnInitialized(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.OnInitialized(e);

            _events = _dte.Events.CommandEvents;
            _events.BeforeExecute += OnBeforeCommandExecuted;
        }

        private void SetFontSize(General settings)
        {
            lblShortcut.FontSize = settings.FontSizeShortcut;
            lblCommand.FontSize = settings.FontSizeCommand;
        }

        bool _isProcessing;

        private void OnBeforeCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            if (_isProcessing || !_keys.Any(Keyboard.IsKeyDown)) return;

            _isProcessing = true;

            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Command cmd = _dte.Commands.Item(Guid, ID);
                var shortcut = Commands.GetShortcut(cmd);

                lblShortcut.Content = shortcut;
                lblCommand.Content = Commands.Prettify(cmd);
                lblCommand.ToolTip = new ToolTip()
                {
                    Content = cmd.LocalizedName
                };

                _isProcessing = false;
            }).FireAndForget();
        }
    }
}