using System;
using System.IO;
using System.Text;

namespace VoiceTyper
{
    public class FormConsoleWriter : TextWriter
    {
        private readonly DebugLogForm debugForm;
        private StringBuilder currentLine = new StringBuilder();

        public FormConsoleWriter(DebugLogForm form)
        {
            debugForm = form;
        }

        public override void Write(char value)
        {
            if (value == '\n')
            {
                // Flush the current line
                debugForm.AppendLog(currentLine.ToString());
                currentLine.Clear();
            }
            else if (value != '\r') // Ignore carriage returns
            {
                currentLine.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            
            // Split by newlines and process each line
            var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            for (int i = 0; i < lines.Length; i++)
            {
                currentLine.Append(lines[i]);
                
                // If this isn't the last line or the string ends with a newline
                if (i < lines.Length - 1 || value.EndsWith('\n'))
                {
                    debugForm.AppendLog(currentLine.ToString());
                    currentLine.Clear();
                }
            }
        }

        public override Encoding Encoding => Encoding.UTF8;
    }
} 