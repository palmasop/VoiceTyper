using System;
using System.Drawing;
using System.Windows.Forms;

namespace VoiceTyper
{
    public partial class DebugLogForm : Form
    {
        private readonly RichTextBox logTextBox = new();

        public DebugLogForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Debug Logs";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            logTextBox.Dock = DockStyle.Fill;
            logTextBox.ReadOnly = true;
            logTextBox.BackColor = Color.FromArgb(30, 30, 30);
            logTextBox.ForeColor = Color.White;
            logTextBox.Font = new Font("Consolas", 10F);
            logTextBox.BorderStyle = BorderStyle.None;

            this.Controls.Add(logTextBox);

            // Keep form in background but don't close on main form close
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    this.Hide();
                    e.Cancel = true;
                }
            };
        }

        public void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLog(message)));
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            logTextBox.ScrollToCaret();
        }

        public void Clear()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Clear));
                return;
            }

            logTextBox.Clear();
        }
    }
} 