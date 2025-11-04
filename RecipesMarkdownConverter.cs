using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public static class NotesMarkdownConverter
    {
        public class CheckboxInfo
        {
            public int LineNumber;
            public bool IsChecked;
            public string Text;
            public int StartIndex;
            public int Length;
        }

        public static string MarkdownToUGUI(string markdown, out List<CheckboxInfo> checkboxes, int fontSizeOffset = 0)
        {
            checkboxes = new List<CheckboxInfo>();

            if (string.IsNullOrEmpty(markdown))
                return markdown;

            string result = markdown;
            int lineNumber = 0;

            var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
            var processedLines = new List<string>();

            foreach (var line in lines)
            {
                string processedLine = line;

                var checkboxMatch = Regex.Match(processedLine, @"^(\s*)-\s+\[([ xX])\]\s+(.+)$");
                if (checkboxMatch.Success)
                {
                    bool isChecked = checkboxMatch.Groups[2].Value.ToLower() == "x";
                    string text = checkboxMatch.Groups[3].Value;
                    string indent = checkboxMatch.Groups[1].Value;

                    checkboxes.Add(new CheckboxInfo
                    {
                        LineNumber = lineNumber,
                        IsChecked = isChecked,
                        Text = text,
                        StartIndex = processedLine.IndexOf('['),
                        Length = checkboxMatch.Value.Length
                    });

                    string checkmark = isChecked ? "☑" : "☐";
                    processedLine = $"{indent}{checkmark} {text}";
                }
                else if (Regex.IsMatch(processedLine, @"^(\s*)([-*])\s+(.+)$"))
                {
                    processedLine = Regex.Replace(processedLine, @"^(\s*)([-*])\s+(.+)$", "$1• $3");
                }
                else if (Regex.IsMatch(processedLine, @"^(\s*)(\d+)\.\s+(.+)$"))
                {
                    processedLine = Regex.Replace(processedLine, @"^(\s*)(\d+)\.\s+(.+)$", "$1$2. $3");
                }

                processedLines.Add(processedLine);
                lineNumber++;
            }

            result = string.Join("\n", processedLines.ToArray());

            result = Regex.Replace(result, @"^######\s+(.+)$", $"<size={14 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^#####\s+(.+)$", $"<size={16 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^####\s+(.+)$", $"<size={18 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^###\s+(.+)$", $"<size={20 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^##\s+(.+)$", $"<size={22 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);
            result = Regex.Replace(result, @"^#\s+(.+)$", $"<size={24 + fontSizeOffset}><b>$1</b></size>", RegexOptions.Multiline);

            result = Regex.Replace(result, @"```([^`]+)```",
                m => "<color=#00ff00><b>" + m.Groups[1].Value.Replace("<", "&lt;").Replace(">", "&gt;") + "</b></color>",
                RegexOptions.Singleline);

            result = Regex.Replace(result, @"==(.+?)==", "<color=#ffff00><b>$1</b></color>");

            result = Regex.Replace(result, @"\*\*\*(.+?)\*\*\*", "<b><i>$1</i></b>");
            result = Regex.Replace(result, @"___(.+?)___", "<b><i>$1</i></b>");

            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "<b>$1</b>");
            result = Regex.Replace(result, @"__(.+?)__", "<b>$1</b>");

            result = Regex.Replace(result, @"(?<!\w)\*(.+?)\*(?!\w)", "<i>$1</i>");
            result = Regex.Replace(result, @"(?<!\w)_(.+?)_(?!\w)", "<i>$1</i>");

            result = Regex.Replace(result, @"`(.+?)`", "<color=#00ff00>$1</color>");

            result = Regex.Replace(result, @"^(---+|\*\*\*+)$", "─────────────────────", RegexOptions.Multiline);

            result = Regex.Replace(result, @"^>\s+(.+)$", "<i><color=#888888>❝ $1</color></i>", RegexOptions.Multiline);

            return result;
        }

        public static string UpdateCheckbox(string markdown, int lineNumber, bool newState)
        {
            var lines = markdown.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);

            if (lineNumber < 0 || lineNumber >= lines.Length)
                return markdown;

            var line = lines[lineNumber];
            var checkboxMatch = Regex.Match(line, @"^(\s*-\s+\[)([ xX])(\]\s+.+)$");

            if (checkboxMatch.Success)
            {
                string newCheckState = newState ? "x" : " ";
                lines[lineNumber] = checkboxMatch.Groups[1].Value + newCheckState + checkboxMatch.Groups[3].Value;
            }

            return string.Join("\n", lines);
        }
    }
}