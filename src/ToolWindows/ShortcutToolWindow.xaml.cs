using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using static ShortcutWindow.OptionsProvider;
using System.Collections.Generic;

namespace ShortcutWindow
{
    public partial class ShortcutToolWindow : UserControl, IDisposable
    {
        private readonly DTE2 _dte;
        private readonly General _settings;
        private readonly CommandBridge _service;
        private CommandEvents _events;
        private readonly Key[] _keys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12];
        private Command _lastCommand;
        private DateTime _lastCommandTime;
        private readonly Timer _timer;
        private bool _disposed = false;
        private int _currentTimeoutInterval = -1; // Track current interval to avoid unnecessary timer restarts

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

            var timeoutMilliseconds = settings.Timeout * 1000;
            if (settings.Timeout > 0)
            {
                // Only update timer if interval changed
                if (_currentTimeoutInterval != timeoutMilliseconds)
                {
                    _timer.Interval = timeoutMilliseconds;
                    _currentTimeoutInterval = timeoutMilliseconds;
                }
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                _currentTimeoutInterval = -1;
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

        private string GetCurrentlyPressedKeys()
        {
            var pressedKeys = new List<string>();

            // Check modifier keys
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                pressedKeys.Add("Ctrl");
            }
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                pressedKeys.Add("Alt");
            }
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                pressedKeys.Add("Shift");
            }

            // Check for other keys that might be pressed
            // Function keys
            for (int i = 1; i <= 12; i++)
            {
                Key functionKey = (Key)Enum.Parse(typeof(Key), $"F{i}");
                if (Keyboard.IsKeyDown(functionKey))
                {
                    pressedKeys.Add($"F{i}");
                }
            }

            // Common keys
            var commonKeys = new Dictionary<Key, string>
            {
                { Key.Q, "Q" },
                { Key.W, "W" },
                { Key.E, "E" },
                { Key.R, "R" },
                { Key.T, "T" },
                { Key.Y, "Y" },
                { Key.U, "U" },
                { Key.I, "I" },
                { Key.O, "O" },
                { Key.P, "P" },
                { Key.A, "A" },
                { Key.S, "S" },
                { Key.D, "D" },
                { Key.F, "F" },
                { Key.G, "G" },
                { Key.H, "H" },
                { Key.J, "J" },
                { Key.K, "K" },
                { Key.L, "L" },
                { Key.Z, "Z" },
                { Key.X, "X" },
                { Key.C, "C" },
                { Key.V, "V" },
                { Key.B, "B" },
                { Key.N, "N" },
                { Key.M, "M" },
                { Key.D1, "1" },
                { Key.D2, "2" },
                { Key.D3, "3" },
                { Key.D4, "4" },
                { Key.D5, "5" },
                { Key.D6, "6" },
                { Key.D7, "7" },
                { Key.D8, "8" },
                { Key.D9, "9" },
                { Key.D0, "0" },
                { Key.OemComma, "," },
                { Key.OemPeriod, "." },
                { Key.OemQuestion, "/" },
                { Key.OemSemicolon, ";" },
                { Key.OemQuotes, "'" },
                { Key.OemOpenBrackets, "[" },
                { Key.OemCloseBrackets, "]" },
                { Key.OemPipe, "\\" },
                { Key.OemMinus, "-" },
                { Key.OemPlus, "=" },
                { Key.Space, "Space" },
                { Key.Enter, "Enter" },
                { Key.Escape, "Escape" },
                { Key.Tab, "Tab" },
                { Key.Back, "Backspace" },
                { Key.Delete, "Delete" },
                { Key.Insert, "Insert" },
                { Key.Home, "Home" },
                { Key.End, "End" },
                { Key.PageUp, "PageUp" },
                { Key.PageDown, "PageDown" },
                { Key.Up, "Up" },
                { Key.Down, "Down" },
                { Key.Left, "Left" },
                { Key.Right, "Right" }
            };

            foreach (var kvp in commonKeys)
            {
                if (Keyboard.IsKeyDown(kvp.Key))
                {
                    pressedKeys.Add(kvp.Value);
                }
            }

            // Return empty string if no interesting keys are pressed
            if (pressedKeys.Count == 0)
            {
                return string.Empty;
            }

            // Return the keys in a format similar to Visual Studio shortcuts
            return string.Join("+", pressedKeys);
        }

        private void OnBeforeCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Capture the currently pressed keys
            string pressedKeys = GetCurrentlyPressedKeys();

            if (string.IsNullOrEmpty(pressedKeys))
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

            // Use string interpolation for better performance
            string debounceKey = $"{Guid}{ID}";
            
            Debouncer.Debounce(debounceKey, () =>
            {
                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var shortcut = Commands.GetShortcut(cmd, pressedKeys);

                    if (!string.IsNullOrEmpty(shortcut) && !string.IsNullOrEmpty(cmd.Name))
                    {
                        lblShortcut.Content = shortcut;
                        lblCommand.Content = Commands.Prettify(cmd);
                        // Set tooltip text directly instead of creating new ToolTip object
                        lblCommand.ToolTip = cmd.LocalizedName;
                    }

                    _lastCommandTime = DateTime.Now;

                    var timeoutMilliseconds = _settings.Timeout * 1000;
                    if (_settings.Timeout > 0)
                    {
                        // Only update timer if interval changed
                        if (_currentTimeoutInterval != timeoutMilliseconds)
                        {
                            _timer.Interval = timeoutMilliseconds;
                            _currentTimeoutInterval = timeoutMilliseconds;
                        }
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
                lblShortcut.Content = " ";
                lblCommand.Content = "Paused"; // no breaking space
                btnPlayPause.Content = "▶️";
                _events.BeforeExecute -= OnBeforeCommandExecuted;
                _timer.Stop();
            }
            else
            {
                _service.Play();
                lblShortcut.Content = " ";
                lblCommand.Content = "Ready";
                btnPlayPause.Content = "⏸";
                _events.BeforeExecute += OnBeforeCommandExecuted;
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            VsShellUtilities.ShowToolsOptionsPage(typeof(GeneralOptions).GUID);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Clean up event handlers
                General.Saved -= OnGeneralSettingsSaved;
                
                if (_events != null)
                {
                    _events.BeforeExecute -= OnBeforeCommandExecuted;
                }

                // Dispose timer
                _timer?.Stop();
                _timer?.Dispose();

                _disposed = true;
            }
        }
    }
}