using System.Windows.Forms;

namespace TrayApp.TrayMenu
{
    internal sealed class TrayMenuController
    {
        private readonly Dictionary<TrayMenuSection, Button> _buttons = new();
        private readonly Action<Button, bool> _applyButtonState;
        private readonly Action _stateChanged;

        public TrayMenuController(Action<Button, bool> applyButtonState, Action stateChanged)
        {
            _applyButtonState = applyButtonState;
            _stateChanged = stateChanged;
            ActiveSection = TrayMenuSection.Overview;
        }

        public TrayMenuSection ActiveSection { get; private set; }

        public void Register(TrayMenuSection section, Button button)
        {
            _buttons[section] = button;
            button.Click += (_, _) => SetActiveSection(section, triggerStateChanged: true);
            _applyButtonState(button, section == ActiveSection);
        }

        public void SetActiveSection(TrayMenuSection section, bool triggerStateChanged = false)
        {
            ActiveSection = section;

            foreach ((TrayMenuSection candidateSection, Button button) in _buttons)
            {
                _applyButtonState(button, candidateSection == section);
            }

            if (triggerStateChanged)
            {
                _stateChanged();
            }
        }
    }
}