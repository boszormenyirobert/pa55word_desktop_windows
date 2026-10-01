using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
        private readonly List<FaqEntry> _faqEntries = new();
        private string _expandedFaqQuestion = string.Empty;

        private void InitializeFaqPanel()
        {
            _quickPanelFaqPanel.AutoScroll = true;
            _quickPanelFaqPanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelFaqPanel.Location = new Point(32, 152);
            _quickPanelFaqPanel.Size = new Size(578, 493);
            _quickPanelFaqPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _quickPanelFaqPanel.Visible = false;
        }

        private void UpdateFaqPresentation()
        {
            bool faqActive = _trayMenuController?.ActiveSection == TrayMenuSection.Faq;
            _quickPanelFaqPanel.Visible = faqActive;

            if (!faqActive)
            {
                return;
            }

            _quickPanelDetailTitleLabel.AutoSize = true;
            _quickPanelDetailTitleLabel.Font = _quickPanelDetailDefaultTitleFont;
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);
            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            LoadFaqEntries();
            RenderFaqEntries();
        }

        private void LoadFaqEntries()
        {
            _faqEntries.Clear();

            try
            {
                string faqPath = AppPaths.GetSourceRootFilePath(Path.Combine("TrayMenu", "FAQ", "faq.md"));
                string? question = null;
                var answer = new StringBuilder();

                foreach (string sourceLine in File.ReadLines(faqPath))
                {
                    if (sourceLine.StartsWith("## ", StringComparison.Ordinal))
                    {
                        AddFaqEntry(question, answer);
                        question = NormalizeFaqText(sourceLine[3..]);
                        answer.Clear();
                        continue;
                    }

                    if (question == null)
                    {
                        continue;
                    }

                    if (sourceLine.StartsWith("### ", StringComparison.Ordinal))
                    {
                        if (answer.Length > 0)
                        {
                            answer.AppendLine();
                        }

                        answer.AppendLine(NormalizeFaqText(sourceLine[4..]));
                        continue;
                    }

                    answer.AppendLine(NormalizeFaqText(sourceLine));
                }

                AddFaqEntry(question, answer);
            }
            catch (Exception exception)
            {
                _faqEntries.Add(new FaqEntry("FAQ unavailable", exception.Message));
            }
        }

        private void AddFaqEntry(string? question, StringBuilder answer)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return;
            }

            _faqEntries.Add(new FaqEntry(question, answer.ToString().Trim()));
        }

        private void RenderFaqEntries()
        {
            _quickPanelFaqPanel.SuspendLayout();
            _quickPanelFaqPanel.Controls.Clear();

            try
            {
                int top = 0;
                foreach (FaqEntry entry in _faqEntries)
                {
                    bool expanded = string.Equals(entry.Question, _expandedFaqQuestion, StringComparison.Ordinal);
                    Control item = CreateFaqAccordionItem(entry, expanded);
                    item.Location = new Point(0, top);
                    _quickPanelFaqPanel.Controls.Add(item);
                    top = item.Bottom + 8;
                }

                _quickPanelFaqPanel.AutoScrollMinSize = new Size(0, top);
            }
            finally
            {
                _quickPanelFaqPanel.ResumeLayout(performLayout: true);
            }
        }

        private Control CreateFaqAccordionItem(FaqEntry entry, bool expanded)
        {
            const int width = 558;
            const int headerHeight = 42;
            var itemPanel = new Panel
            {
                BackColor = Color.FromArgb(46, 48, 77),
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(width, headerHeight)
            };

            var questionButton = new Button
            {
                BackColor = itemPanel.BackColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = Point.Empty,
                Padding = new Padding(14, 0, 42, 0),
                Size = new Size(width - 2, headerHeight - 2),
                Text = entry.Question,
                TextAlign = ContentAlignment.MiddleLeft,
                UseVisualStyleBackColor = false
            };
            questionButton.FlatAppearance.BorderSize = 0;
            questionButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(59, 61, 88);
            questionButton.Click += (_, _) => ToggleFaqEntry(entry.Question);
            itemPanel.Controls.Add(questionButton);

            var indicatorLabel = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 205, 0),
                Location = new Point(width - 36, 8),
                Size = new Size(24, 24),
                Text = expanded ? "-" : "+",
                TextAlign = ContentAlignment.MiddleCenter
            };
            indicatorLabel.Click += (_, _) => ToggleFaqEntry(entry.Question);
            itemPanel.Controls.Add(indicatorLabel);

            if (!expanded)
            {
                return itemPanel;
            }

            int answerHeight = MeasureFaqAnswerHeight(entry.Answer, width - 52);
            var answerPanel = new Panel
            {
                BackColor = Color.FromArgb(38, 40, 67),
                Location = new Point(0, headerHeight),
                Size = new Size(width - 2, answerHeight + 24)
            };
            var answerLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(220, 222, 241),
                Location = new Point(16, 12),
                Size = new Size(width - 52, answerHeight),
                Text = entry.Answer
            };
            answerPanel.Controls.Add(answerLabel);
            itemPanel.Controls.Add(answerPanel);
            itemPanel.Height = answerPanel.Bottom;
            return itemPanel;
        }

        private void ToggleFaqEntry(string question)
        {
            _expandedFaqQuestion = string.Equals(_expandedFaqQuestion, question, StringComparison.Ordinal)
                ? string.Empty
                : question;
            RenderFaqEntries();
        }

        private static int MeasureFaqAnswerHeight(string answer, int width)
        {
            Size measuredSize = TextRenderer.MeasureText(
                string.IsNullOrWhiteSpace(answer) ? "No answer is available." : answer,
                new Font("Segoe UI", 8.5F),
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak);
            return Math.Max(36, measuredSize.Height + 4);
        }

        private static string NormalizeFaqText(string value)
        {
            return value
                .Replace("**", string.Empty, StringComparison.Ordinal)
                .Replace("~~", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        private sealed record FaqEntry(string Question, string Answer);
    }
}
