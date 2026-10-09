using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class ExtensionSessionStateStore
    {
        private readonly object _syncRoot = new();
        private readonly ExtensionSessionStateResponseDto _state = new()
        {
            Authorized = false,
            CloseExtension = true,
            AuthVersion = 0,
            Reason = "authorization_missing"
        };

        public event Action<ExtensionSessionStateResponseDto>? StateChanged;

        public ExtensionSessionStateResponseDto GetSnapshot()
        {
            lock (_syncRoot)
            {
                return CreateSnapshotUnsafe();
            }
        }

        public void MarkAuthorized()
        {
            ExtensionSessionStateResponseDto? snapshot = null;

            lock (_syncRoot)
            {
                if (_state.Authorized && !_state.CloseExtension && string.IsNullOrWhiteSpace(_state.Reason))
                {
                    return;
                }

                _state.Authorized = true;
                _state.CloseExtension = false;
                _state.Reason = null;
                _state.AuthVersion++;
                snapshot = CreateSnapshotUnsafe();
            }

            StateChanged?.Invoke(snapshot);
        }

        public void MarkInvalid(string reason)
        {
            ExtensionSessionStateResponseDto? snapshot = null;

            lock (_syncRoot)
            {
                if (!_state.Authorized && _state.CloseExtension && string.Equals(_state.Reason, reason, StringComparison.Ordinal))
                {
                    return;
                }

                _state.Authorized = false;
                _state.CloseExtension = true;
                _state.Reason = reason;
                _state.AuthVersion++;
                snapshot = CreateSnapshotUnsafe();
            }

            StateChanged?.Invoke(snapshot);
        }

        private ExtensionSessionStateResponseDto CreateSnapshotUnsafe()
        {
            return new ExtensionSessionStateResponseDto
            {
                Authorized = _state.Authorized,
                CloseExtension = _state.CloseExtension,
                AuthVersion = _state.AuthVersion,
                Reason = _state.Reason
            };
        }
    }
}