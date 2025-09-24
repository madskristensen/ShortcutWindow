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
    }
}