using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ShortcutWindow
{
    /// <summary>
    /// Captures keyboard input at a low level using WPF's InputManager.
    /// This allows detecting shortcuts even when intercepted by other extensions.
    /// </summary>
    public sealed class KeyboardHook : IDisposable
    {
        private bool _disposed;
        private bool _isEnabled = true;

        /// <summary>
        /// Fired when an interesting key combination is detected (modifier + key, or function key).
        /// The string contains the formatted shortcut (e.g., "Ctrl+Shift+K").
        /// </summary>
        public event EventHandler<ShortcutDetectedEventArgs> ShortcutDetected;

        public KeyboardHook()
        {
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        /// <summary>
        /// Gets or sets whether the hook is actively monitoring keyboard input.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!_isEnabled || _disposed)
                return;

            // Only process keyboard events
            if (e.StagingItem.Input is not KeyEventArgs keyArgs)
                return;

            // Only process KeyDown events (not KeyUp)
            if (keyArgs.RoutedEvent != Keyboard.KeyDownEvent)
                return;

            // Get the actual key (handling system keys like Alt combinations)
            Key key = keyArgs.Key == Key.System ? keyArgs.SystemKey : keyArgs.Key;

            // Skip if only modifier keys are pressed (we want modifier + something)
            if (IsModifierKey(key))
                return;

            // Build the shortcut string
            string shortcut = BuildShortcutString(key);

            // Only fire if it's an "interesting" shortcut
            if (!string.IsNullOrEmpty(shortcut))
            {
                ShortcutDetected?.Invoke(this, new ShortcutDetectedEventArgs(shortcut, key));
            }
        }

        /// <summary>
        /// Builds a formatted shortcut string from the currently pressed keys.
        /// Returns null if the combination isn't interesting (no modifiers and not a function key).
        /// </summary>
        private string BuildShortcutString(Key key)
        {
            bool hasCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool hasAlt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            bool hasShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool hasWin = Keyboard.Modifiers.HasFlag(ModifierKeys.Windows);

            bool hasAnyModifier = hasCtrl || hasAlt || hasShift || hasWin;
            bool isFunctionKey = IsFunctionKey(key);

            // Filter: Must have modifier OR be a function key
            if (!hasAnyModifier && !isFunctionKey)
                return null;

            var parts = new List<string>();

            if (hasCtrl)
                parts.Add("Ctrl");
            if (hasAlt)
                parts.Add("Alt");
            if (hasShift)
                parts.Add("Shift");
            if (hasWin)
                parts.Add("Win");

            // Add the main key
            string keyName = GetKeyDisplayName(key);
            if (!string.IsNullOrEmpty(keyName))
                parts.Add(keyName);

            return parts.Count > 0 ? string.Join("+", parts) : null;
        }

        /// <summary>
        /// Gets a display-friendly name for a key.
        /// </summary>
        private static string GetKeyDisplayName(Key key)
        {
            return key switch
            {
                // Function keys
                Key.F1 => "F1",
                Key.F2 => "F2",
                Key.F3 => "F3",
                Key.F4 => "F4",
                Key.F5 => "F5",
                Key.F6 => "F6",
                Key.F7 => "F7",
                Key.F8 => "F8",
                Key.F9 => "F9",
                Key.F10 => "F10",
                Key.F11 => "F11",
                Key.F12 => "F12",

                // Letter keys
                Key.A => "A",
                Key.B => "B",
                Key.C => "C",
                Key.D => "D",
                Key.E => "E",
                Key.F => "F",
                Key.G => "G",
                Key.H => "H",
                Key.I => "I",
                Key.J => "J",
                Key.K => "K",
                Key.L => "L",
                Key.M => "M",
                Key.N => "N",
                Key.O => "O",
                Key.P => "P",
                Key.Q => "Q",
                Key.R => "R",
                Key.S => "S",
                Key.T => "T",
                Key.U => "U",
                Key.V => "V",
                Key.W => "W",
                Key.X => "X",
                Key.Y => "Y",
                Key.Z => "Z",

                // Number keys
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",

                // Numpad
                Key.NumPad0 => "Num0",
                Key.NumPad1 => "Num1",
                Key.NumPad2 => "Num2",
                Key.NumPad3 => "Num3",
                Key.NumPad4 => "Num4",
                Key.NumPad5 => "Num5",
                Key.NumPad6 => "Num6",
                Key.NumPad7 => "Num7",
                Key.NumPad8 => "Num8",
                Key.NumPad9 => "Num9",

                // Special keys
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Escape => "Esc",
                Key.Tab => "Tab",
                Key.Back => "Backspace",
                Key.Delete => "Del",
                Key.Insert => "Ins",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Left => "Left",
                Key.Right => "Right",

                // Punctuation
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemQuestion => "/",
                Key.OemSemicolon => ";",
                Key.OemQuotes => "'",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemPipe => "\\",
                Key.OemMinus => "-",
                Key.OemPlus => "=",
                Key.OemTilde => "`",

                _ => null // Unknown or modifier-only key
            };
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin ||
                   key == Key.System; // Alt key shows as System
        }

        private static bool IsFunctionKey(Key key)
        {
            return key >= Key.F1 && key <= Key.F12;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                InputManager.Current.PreProcessInput -= OnPreProcessInput;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Event arguments for when a shortcut is detected.
    /// </summary>
    public class ShortcutDetectedEventArgs : EventArgs
    {
        public ShortcutDetectedEventArgs(string shortcut, Key key)
        {
            Shortcut = shortcut;
            Key = key;
        }

        /// <summary>
        /// The formatted shortcut string (e.g., "Ctrl+Shift+K").
        /// </summary>
        public string Shortcut { get; }

        /// <summary>
        /// The main key that was pressed (excluding modifiers).
        /// </summary>
        public Key Key { get; }
    }
}
