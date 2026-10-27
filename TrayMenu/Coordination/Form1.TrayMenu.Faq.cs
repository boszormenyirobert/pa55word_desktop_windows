using System.Drawing;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
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
            _quickPanelFaqPanel.RefreshContent();
        }
    }
}
