using System.Linq;
using EnvDTE;

namespace ShortcutWindow
{
    public class Commands
    {
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
                var index = bindings.IndexOf(':') + 2;
                var shortcut = bindings.Substring(index);

                if (!IsShortcutInteresting(shortcut))
                {
                    shortcut = null;
                }

                return shortcut;
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

            var index = cmd.LocalizedName.LastIndexOf('.') + 1;
            return cmd.LocalizedName.Substring(index);
        }

        private static bool IsShortcutInteresting(string shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                return false;
            }

            return shortcut.Contains("Ctrl") || shortcut.Contains("Alt") || shortcut.Contains("Shift");
        }
    }
}