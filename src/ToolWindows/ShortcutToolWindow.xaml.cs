using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using EnvDTE;
using EnvDTE80;
using static ShortcutWindow.OptionsProvider;
using System.Collections.Generic;
using System;

namespace ShortcutWindow
{
    public partial class ShortcutToolWindow : UserControl, IDisposable
    {
        // Static readonly arrays to avoid recreating on every keystroke
        private static readonly Key[] FunctionKeys = new Key[]
        {
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
            Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12
        };

        private static readonly string[] FunctionKeyNames = new string[]
        {
            "F1", "F2", "F3", "F4", "F5", "F6",
            "F7", "F8", "F9", "F10", "F11", "F12"
        };

        private static readonly Dictionary<Key, string> CommonKeys = new Dictionary<Key, string>
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

        private readonly DTE2 _dte;
        private readonly General _settings;
        private readonly CommandBridge _service;
        private CommandEvents _events;
        private readonly Key[] _keys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12];
        private Command _lastCommand;
        private string _lastCommandKey; // Store command identifier for reliable repeat detection
        private DateTime _lastCommandTime;
        private int _repeatCount = 1;
        private const double RepeatDetectionWindowSeconds = 3.0; // Time window for counting repeated shortcuts
        private readonly Timer _timer;
        private bool _disposed = false;
        private int _currentTimeoutInterval = -1; // Track current interval to avoid unnecessary timer restarts

        // Keyboard hook for detecting shortcuts intercepted by other extensions
        private readonly KeyboardHook _keyboardHook;
        private string _lastDetectedShortcut;
        private DateTime _lastDetectedTime;
        private bool _commandHandledShortcut; // Flag to track if BeforeExecute handled the shortcut

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

            // Initialize keyboard hook for capturing intercepted shortcuts
            _keyboardHook = new KeyboardHook();
            _keyboardHook.ShortcutDetected += OnShortcutDetected;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_lastCommandTime.AddSeconds(_settings.Timeout) < DateTime.Now)
            {
                _lastCommand = null;
                _lastCommandKey = null;
                _repeatCount = 1;
                _timer.Stop();

                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Fade out animation
                    PlayFadeOutAnimation();

                    lblShortcut.Content = "Ready";
                    lblCommand.Content = "Awaiting shortcut...";

                    // Hide chord elements and repeat counter
                    lblChordSeparator.Visibility = Visibility.Collapsed;
                    lblShortcutChord.Visibility = Visibility.Collapsed;
                    lblRepeatCount.Visibility = Visibility.Collapsed;
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
            lblChordSeparator.FontSize = settings.FontSizeShortcut;
            lblShortcutChord.FontSize = settings.FontSizeShortcut;
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
            // Function keys - use cached arrays to avoid Enum.Parse and string allocation
            for (int i = 0; i < FunctionKeys.Length; i++)
            {
                if (Keyboard.IsKeyDown(FunctionKeys[i]))
                {
                    pressedKeys.Add(FunctionKeyNames[i]);
                }
            }

            // Check common keys using the static dictionary
            foreach (var kvp in CommonKeys)
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

            // Mark that a command handled this shortcut (so keyboard hook won't show "intercepted")
            _commandHandledShortcut = true;

            // Capture the currently pressed keys
            string pressedKeys = GetCurrentlyPressedKeys();

            if (string.IsNullOrEmpty(pressedKeys))
            {
                return;
            }

            Command cmd = _dte.Commands.Item(Guid, ID);
            string shortcut = Commands.GetShortcut(cmd, pressedKeys);

            if (string.IsNullOrEmpty(shortcut) || string.IsNullOrEmpty(cmd.Name))
            {
                return;
            }

            // Use command key for reliable repeat detection (object reference may differ)
            string commandKey = $"{Guid}{ID}";
            bool isSameCommand = commandKey == _lastCommandKey;
            bool isWithinRepeatWindow = (DateTime.Now - _lastCommandTime).TotalSeconds < RepeatDetectionWindowSeconds;

            if (isSameCommand && isWithinRepeatWindow)
            {
                _lastCommandTime = DateTime.Now;
                _repeatCount++;
                UpdateRepeatCountDisplay();
                return;
            }

            _lastCommand = cmd;
            _lastCommandKey = commandKey;
            _repeatCount = 1;

            Debouncer.Debounce(commandKey, () =>
            {
                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Display chord shortcuts with visual separation
                    DisplayShortcut(shortcut);

                    lblCommand.Content = Commands.Prettify(cmd);
                    // Set tooltip text directly instead of creating new ToolTip object
                    lblCommand.ToolTip = cmd.LocalizedName;

                    // Play fade in animation
                    PlayFadeInAnimation();

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

        /// <summary>
        /// Called when the keyboard hook detects a shortcut-like key combination.
        /// This handles shortcuts that may have been intercepted by other extensions.
        /// </summary>
        private void OnShortcutDetected(object sender, ShortcutDetectedEventArgs e)
        {
            // Store the detected shortcut and time
            _lastDetectedShortcut = e.Shortcut;
            _lastDetectedTime = DateTime.Now;

            // Reset the flag - will be set to true if BeforeExecute fires
            _commandHandledShortcut = false;

            // Use debouncer to wait a bit and see if a command handles this shortcut
            // The delay should be slightly longer than the command debounce (300ms)
            Debouncer.Debounce("keyboard_hook", () =>
            {
                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Only show if no command handled it and we're still playing
                    if (!_commandHandledShortcut && _service.IsPlaying)
                    {
                        // Check if this shortcut was recently detected (within 500ms)
                        if ((DateTime.Now - _lastDetectedTime).TotalMilliseconds < 500)
                        {
                            DisplayShortcut(_lastDetectedShortcut);
                            lblCommand.Content = " ";
                            lblCommand.ToolTip = null;

                            PlayFadeInAnimation();

                            _lastCommandTime = DateTime.Now;

                            var timeoutMilliseconds = _settings.Timeout * 1000;
                            if (_settings.Timeout > 0)
                            {
                                if (_currentTimeoutInterval != timeoutMilliseconds)
                                {
                                    _timer.Interval = timeoutMilliseconds;
                                    _currentTimeoutInterval = timeoutMilliseconds;
                                }
                                _timer.Start();
                            }
                        }
                    }
                }).FireAndForget();
            }, 350); // Slightly longer than the command debounce delay
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
                _keyboardHook.IsEnabled = false;
                _timer.Stop();

                // Hide chord elements and repeat counter
                lblChordSeparator.Visibility = Visibility.Collapsed;
                lblShortcutChord.Visibility = Visibility.Collapsed;
                lblRepeatCount.Visibility = Visibility.Collapsed;

                // Reset repeat tracking
                _lastCommand = null;
                _lastCommandKey = null;
                _repeatCount = 1;
            }
            else
            {
                _service.Play();
                lblShortcut.Content = " ";
                lblCommand.Content = "Ready";
                btnPlayPause.Content = "⏸";
                _events.BeforeExecute += OnBeforeCommandExecuted;
                _keyboardHook.IsEnabled = true;
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            VsShellUtilities.ShowToolsOptionsPage(typeof(GeneralOptions).GUID);
        }

        /// <summary>
        /// Displays a shortcut, compressing chord shortcuts (e.g., "Ctrl+K, Ctrl+C" becomes "Ctrl+K+C").
        /// </summary>
        private void DisplayShortcut(string shortcut)
        {
            // Hide chord elements - we now display everything in a single label
            lblChordSeparator.Visibility = Visibility.Collapsed;
            lblShortcutChord.Visibility = Visibility.Collapsed;

            // Check if this is a chord shortcut (contains ", " pattern)
            if (shortcut.Contains(", "))
            {
                lblShortcut.Content = CompressChordShortcut(shortcut);
            }
            else
            {
                lblShortcut.Content = shortcut;
            }

            // Update repeat counter based on current count (handles debounce race condition)
            UpdateRepeatCountDisplay();
        }

        /// <summary>
        /// Compresses a chord shortcut like "Ctrl+K, Ctrl+C" to "Ctrl+K+C".
        /// Extracts the final key from each chord part and combines them.
        /// </summary>
        private static string CompressChordShortcut(string shortcut)
        {
            var parts = shortcut.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return shortcut;
            }

            // Start with the first part as-is (e.g., "Ctrl+K")
            var result = parts[0];

            // For subsequent parts, extract just the final key
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                int lastPlusIndex = part.LastIndexOf('+');

                // Extract just the key portion (e.g., "Ctrl+C" -> "C")
                string key = lastPlusIndex >= 0 && lastPlusIndex < part.Length - 1
                    ? part.Substring(lastPlusIndex + 1)
                    : part;

                result += "+" + key;
            }

            return result;
        }

        /// <summary>
        /// Updates the repeat count display when the same shortcut is pressed multiple times.
        /// </summary>
        private void UpdateRepeatCountDisplay()
        {
            if (_repeatCount > 1)
            {
                lblRepeatCount.Content = $"(x{_repeatCount})";
                lblRepeatCount.Visibility = Visibility.Visible;
            }
            else
            {
                lblRepeatCount.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Plays the fade-in animation on the shortcut panel.
        /// </summary>
        private void PlayFadeInAnimation()
        {
            if (TryFindResource("FadeInStoryboard") is Storyboard fadeIn)
            {
                fadeIn.Begin(pnlShortcut);
            }
        }

        /// <summary>
        /// Plays the fade-out animation on the shortcut panel.
        /// </summary>
        private void PlayFadeOutAnimation()
        {
            if (TryFindResource("FadeOutStoryboard") is Storyboard fadeOut)
            {
                fadeOut.Begin(pnlShortcut);
            }
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

                // Clean up keyboard hook
                if (_keyboardHook != null)
                {
                    _keyboardHook.ShortcutDetected -= OnShortcutDetected;
                    _keyboardHook.Dispose();
                }

                // Dispose timer
                _timer?.Stop();
                _timer?.Dispose();

                _disposed = true;
            }
        }
    }
}