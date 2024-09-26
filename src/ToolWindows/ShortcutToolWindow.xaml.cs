using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;

namespace ShortcutWindow
{
    public partial class ShortcutToolWindow : UserControl
    {
        private readonly DTE2 _dte;
        private readonly CommandBridge _service;
        private CommandEvents _events;
        private readonly Key[] _keys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift];
        private Command _lastCommand;
        private DateTime _lastCommandTime;
        private readonly Timer _timer;

        public ShortcutToolWindow(DTE2 dte, General settings, CommandBridge service)
        {
            _dte = dte;
            _service = service;
            General.Saved += OnGeneralSettingsSaved;

            InitializeComponent();
            SetFontSize(settings);

            _timer = new Timer(5 * 1000);
            _timer.Elapsed += OnTimerElapsed;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_lastCommandTime.AddSeconds(5) < DateTime.Now)
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

        protected override void OnMouseEnter(MouseEventArgs e) => btnPlayPause.Visibility = Visibility.Visible;
        protected override void OnMouseLeave(MouseEventArgs e) => btnPlayPause.Visibility = _service.IsPlaying ? Visibility.Collapsed : Visibility.Visible;

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
                    _timer.Start();

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
                btnPlayPause.Visibility = Visibility.Visible;
                _events.BeforeExecute -= OnBeforeCommandExecuted;
                _timer.Stop();
            }
            else
            {
                _service.Play();
                lblShortcut.Content = "Ready";
                lblCommand.Content = "Awaiting shortcut...";
                btnPlayPause.Content = "⏸";
                btnPlayPause.Visibility = Visibility.Collapsed;
                _events.BeforeExecute += OnBeforeCommandExecuted;
            }
        }
    }
}