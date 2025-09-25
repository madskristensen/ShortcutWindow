using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;
using System.Collections.Generic;
using System;

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

        /// <summary>
        /// Gets the keyboard shortcut for a command, attempting to match the specific shortcut
        /// that was actually pressed by the user rather than just returning the first binding.
        /// </summary>
        /// <param name="cmd">The Visual Studio command</param>
        /// <param name="pressedKeys">The keys currently being pressed by the user</param>
        /// <returns>The matching keyboard shortcut string, or null if none found</returns>
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

            // If no specific match found, try a more lenient search
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

                    // More lenient matching for cases where exact match fails
                    if (IsPartialMatch(shortcut, pressedKeys))
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

            // Normalize both strings for comparison
            string normalizedShortcut = NormalizeShortcut(shortcut);
            string normalizedPressed = NormalizeShortcut(pressedKeys);

            // Direct comparison first
            if (normalizedShortcut == normalizedPressed)
            {
                return true;
            }

            // Handle VS specific shortcut formats
            // VS might have shortcuts like "Ctrl+," or "Ctrl+1, M" (with spaces and commas)
            // Try different variations of the shortcut format
            var shortcutVariations = GetShortcutVariations(shortcut);
            var pressedVariations = GetShortcutVariations(pressedKeys);

            foreach (var shortcutVar in shortcutVariations)
            {
                foreach (var pressedVar in pressedVariations)
                {
                    if (NormalizeShortcut(shortcutVar) == NormalizeShortcut(pressedVar))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeShortcut(string shortcut)
        {
            if (string.IsNullOrEmpty(shortcut))
                return string.Empty;

            return shortcut
                .Replace(" ", "")
                .Replace("+", "")
                .Replace(",", "")
                .ToLowerInvariant();
        }

        private static List<string> GetShortcutVariations(string shortcut)
        {
            var variations = new List<string> { shortcut };

            if (string.IsNullOrEmpty(shortcut))
                return variations;

            // Add variation without spaces
            variations.Add(shortcut.Replace(" ", ""));

            // Add variation with different separator
            variations.Add(shortcut.Replace("+", ""));
            variations.Add(shortcut.Replace(" + ", "+"));

            // Handle comma key specifically
            if (shortcut.Contains(","))
            {
                variations.Add(shortcut.Replace(",", "OemComma"));
                variations.Add(shortcut.Replace(",", "Comma"));
            }

            // Handle multi-key shortcuts like "Ctrl+1, M"
            if (shortcut.Contains(", "))
            {
                // Split on comma and handle as sequence
                var parts = shortcut.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    variations.Add(string.Join("", parts));
                    variations.Add(string.Join("+", parts));
                }
            }

            return variations;
        }

        private static bool IsPartialMatch(string shortcut, string pressedKeys)
        {
            if (string.IsNullOrEmpty(shortcut) || string.IsNullOrEmpty(pressedKeys))
            {
                return false;
            }

            // For partial matching, check if the pressed keys contain the main components of the shortcut
            var normalizedShortcut = NormalizeShortcut(shortcut);
            var normalizedPressed = NormalizeShortcut(pressedKeys);

            // Check if the pressed keys contain all the key components from the shortcut
            var shortcutParts = shortcut.ToLowerInvariant()
                .Replace("+", " ")
                .Replace(",", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var pressedParts = pressedKeys.ToLowerInvariant()
                .Replace("+", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // All shortcut parts should be present in pressed keys
            foreach (var part in shortcutParts)
            {
                bool found = false;
                foreach (var pressed in pressedParts)
                {
                    if (pressed.Contains(part) || part.Contains(pressed))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }

            return true;
        }
    }
}