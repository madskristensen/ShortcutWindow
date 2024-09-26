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
        private bool _showShortcut;

        public ShortcutToolWindow(DTE2 dte)
        {
            _dte = dte;
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.OnInitialized(e);

            _events = _dte.Events.CommandEvents;
            _events.BeforeExecute += OnBeforeCommandExecuted;
            _events.AfterExecute += OnAfterCommandExecuted;
        }

        private void OnAfterCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_showShortcut)
            {
                return;
            }

            Command cmd = _dte.Commands.Item(Guid, ID);
            var shortcut = Commands.GetShortcut(cmd);

            lblShortcut.Content = shortcut;
            lblCommand.Content = Commands.Prettify(cmd);
        }

        private void OnBeforeCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            _showShortcut = _keys.Any(Keyboard.IsKeyDown);
        }
    }
}