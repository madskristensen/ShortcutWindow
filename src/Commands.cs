using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;

namespace ShortcutWindow
{
    public class Commands
    {
        // Cache the regex for better performance since it's used frequently
        private static readonly Regex CamelCaseRegex = new Regex("[a-z][A-Z]", RegexOptions.Compiled);

        public static string GetShortcut(Command cmd)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cmd == null || string.IsNullOrEmpty(cmd.Name))
            {
                return null;
            }

            var bindings = ((object[])cmd.Bindings).FirstOrDefault() as string;

            if (!string.IsNullOrEmpty(bindings))
            {
                var colonIndex = bindings.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 2 < bindings.Length)
                {
                    var shortcut = bindings.Substring(colonIndex + 2);

                    if (!IsShortcutInteresting(shortcut))
                    {
                        return null;
                    }

                    return shortcut;
                }
            }

            return null;
        }

        public static string GetShortcut(Command cmd, string pressedKeys)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cmd == null || string.IsNullOrEmpty(cmd.Name))
            {
                return null;
            }

            if (string.IsNullOrEmpty(pressedKeys))
            {
                // Fall back to the original method if no pressed keys provided
                return GetShortcut(cmd);
            }

            // Check all bindings to find the one that matches the pressed keys
            var allBindings = (object[])cmd.Bindings;
            
            foreach (string binding in allBindings)
            {
                if (string.IsNullOrEmpty(binding))
                    continue;

                var colonIndex = binding.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 2 < binding.Length)
                {
                    var shortcut = binding.Substring(colonIndex + 2);

                    if (!IsShortcutInteresting(shortcut))
                    {
                        continue;
                    }

                    // Check if this shortcut matches the pressed keys
                    if (DoesShortcutMatch(shortcut, pressedKeys))
                    {
                        return shortcut;
                    }
                }
            }

            // If no specific match found, fall back to the original method
            return GetShortcut(cmd);
        }

        public static string Prettify(Command cmd)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!cmd.LocalizedName.Contains('.'))
            {
                return cmd.LocalizedName;
            }

            var lastDotIndex = cmd.LocalizedName.LastIndexOf('.');
            if (lastDotIndex >= 0 && lastDotIndex + 1 < cmd.LocalizedName.Length)
            {
                var name = cmd.LocalizedName.Substring(lastDotIndex + 1);
                return CamelCaseRegex.Replace(name, m => $"{m.Value[0]} {m.Value[1]}");
            }

            return cmd.LocalizedName;
        }

        private static bool IsShortcutInteresting(string shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                return false;
            }

            // Use IndexOf for potentially better performance than Contains
            // Check for modifier keys
            if (shortcut.IndexOf("Ctrl", System.StringComparison.Ordinal) >= 0 || 
                shortcut.IndexOf("Alt", System.StringComparison.Ordinal) >= 0 || 
                shortcut.IndexOf("Shift", System.StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            // Check for function keys F1-F12 with word boundaries to avoid false positives like F13
            for (int i = 1; i <= 12; i++)
            {
                string functionKey = $"F{i}";
                int index = shortcut.IndexOf(functionKey, System.StringComparison.Ordinal);
                if (index >= 0)
                {
                    // Ensure it's a complete function key (not part of F13, F14, etc.)
                    // Check that the character after the function key is not a digit
                    int nextCharIndex = index + functionKey.Length;
                    if (nextCharIndex >= shortcut.Length || !char.IsDigit(shortcut[nextCharIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool DoesShortcutMatch(string shortcut, string pressedKeys)
        {
            if (string.IsNullOrEmpty(shortcut) || string.IsNullOrEmpty(pressedKeys))
            {
                return false;
            }

            // Normalize both strings for comparison (remove spaces, convert to lowercase)
            string normalizedShortcut = shortcut.Replace(" ", "").ToLowerInvariant();
            string normalizedPressed = pressedKeys.Replace(" ", "").ToLowerInvariant();

            // Direct comparison first
            if (normalizedShortcut == normalizedPressed)
            {
                return true;
            }

            // Handle common VS shortcut format variations
            // VS shortcuts might be in format like "Ctrl+Q" while pressed keys might be "CtrlQ"
            normalizedShortcut = normalizedShortcut.Replace("+", "");
            normalizedPressed = normalizedPressed.Replace("+", "");

            return normalizedShortcut == normalizedPressed;
        }
    }
}