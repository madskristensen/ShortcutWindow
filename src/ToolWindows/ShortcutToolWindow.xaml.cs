using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using static ShortcutWindow.OptionsProvider;

namespace ShortcutWindow
{
    public partial class ShortcutToolWindow : UserControl
    {
        private readonly DTE2 _dte;
        private readonly General _settings;
        private readonly CommandBridge _service;
        private CommandEvents _events;
        private readonly Key[] _keys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift];
        private Command _lastCommand;
        private DateTime _lastCommandTime;
        private readonly Timer _timer;

        public ShortcutToolWindow(DTE2 dte, General settings, CommandBridge service)
        {
            _dte = dte;
            _settings = settings;
            _service = service;
            General.Saved += OnGeneralSettingsSaved;

            InitializeComponent();
            SetFontSize(settings);

            _timer = new Timer();
            _timer.Elapsed += OnTimerElapsed;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_lastCommandTime.AddSeconds(_settings.Timeout) < DateTime.Now)
            {
                _lastCommand = null;
                _timer.Stop();

                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    lblShortcut.Content = "Ready";
                    lblCommand.Content = "Awaiting shortcut...";
                }).FireAndForget();
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e) => pnlControls.Visibility = Visibility.Visible;
        protected override void OnMouseLeave(MouseEventArgs e) => pnlControls.Visibility = Visibility.Collapsed;

        private void OnGeneralSettingsSaved(General settings)
        {
            SetFontSize(settings);

            if (settings.Timeout > 0)
            {
                _timer.Interval = settings.Timeout * 1000;
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
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

        private void OnBeforeCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_keys.Any(Keyboard.IsKeyDown))
            {
                return;
            }

            Command cmd = _dte.Commands.Item(Guid, ID);

            if (cmd == _lastCommand)
            {
                _lastCommandTime = DateTime.Now;
                return;
            }

            _lastCommand = cmd;

            Debouncer.Debounce(Guid + ID, () =>
            {
                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var shortcut = Commands.GetShortcut(cmd);

                    if (!string.IsNullOrEmpty(shortcut) && !string.IsNullOrEmpty(cmd.Name))
                    {
                        lblShortcut.Content = shortcut;
                        lblCommand.Content = Commands.Prettify(cmd);
                        lblCommand.ToolTip = new ToolTip()
                        {
                            Content = cmd.LocalizedName
                        };
                    }

                    _lastCommandTime = DateTime.Now;

                    if (_settings.Timeout > 0)
                    {
                        _timer.Interval = _settings.Timeout * 1000;
                        _timer.Start();
                    }
                }).FireAndForget();
            }, 300);
        }

        private void btnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_service.IsPlaying)
            {
                _service.Stop();
                lblShortcut.Content = "Paused";
                lblCommand.Content = "\x00A0"; // no breaking space
                btnPlayPause.Content = "▶️";
                _events.BeforeExecute -= OnBeforeCommandExecuted;
                _timer.Stop();
            }
            else
            {
                _service.Play();
                lblShortcut.Content = "Ready";
                lblCommand.Content = "Awaiting shortcut...";
                btnPlayPause.Content = "⏸";
                _events.BeforeExecute += OnBeforeCommandExecuted;
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            VsShellUtilities.ShowToolsOptionsPage(typeof(GeneralOptions).GUID);
        }
    }
}