using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using Xunit;

namespace TrayApp.Tests;

[CollectionDefinition("Device loading", DisableParallelization = true)]
public sealed class DeviceLoadingCollection;

[Collection("Device loading")]
public sealed class DeviceLoadingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverlappingLoads_OnlyNewestCanApplyResults(bool olderCompletesFirst)
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task older = fixture.Load();
            Task newer = fixture.Load();
            Assert.Equal(2, fixture.Service.Requests.Count);

            if (olderCompletesFirst)
            {
                fixture.Complete(0, "Old device");
                Assert.True(older.IsCompletedSuccessfully);
                Assert.True(fixture.Loading);
                Assert.False(fixture.Loaded);
                Assert.Empty(fixture.DeviceNames);
            }

            fixture.Complete(1, "New device");
            if (!olderCompletesFirst) fixture.Complete(0, "Old device");

            Assert.True(older.IsCompletedSuccessfully);
            Assert.True(newer.IsCompletedSuccessfully);
            Assert.Equal(new[] { "New device" }, fixture.DeviceNames);
            Assert.True(fixture.Loaded);
            Assert.False(fixture.Loading);
            Assert.Empty(fixture.Error);
        });
    }

    [Fact]
    public void OlderFailure_DoesNotClearBusyStateOrSetAnError()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task older = fixture.Load();
            Task newer = fixture.Load();
            fixture.Fail(0, "Old error");
            Assert.True(older.IsCompletedSuccessfully);
            Assert.True(fixture.Loading);
            Assert.False(fixture.Loaded);
            Assert.Empty(fixture.Error);
            fixture.Complete(1, "New device");
            Assert.True(newer.IsCompletedSuccessfully);
            Assert.Equal(new[] { "New device" }, fixture.DeviceNames);
            Assert.Empty(fixture.Error);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reset_InvalidatesPendingSuccessAndFailure(bool failure)
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task loading = fixture.Load();
            fixture.Reset();
            if (failure) fixture.Fail(0, "Stale error");
            else fixture.Complete(0, "Stale device");

            Assert.True(loading.IsCompletedSuccessfully);
            Assert.False(fixture.Loading);
            Assert.False(fixture.Loaded);
            Assert.Empty(fixture.DeviceNames);
            Assert.Empty(fixture.Error);
            Assert.Equal("All devices", Assert.Single(fixture.View.SourceComboBox.Items.Cast<object>()).ToString());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfileChange_RejectsOldResultsEvenWithoutReset(bool failure)
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task older = fixture.Load();
            fixture.SelectProfile("profile-b");
            if (failure) fixture.Fail(0, "Other profile error");
            else fixture.Complete(0, "Other profile device");
            Assert.True(older.IsCompletedSuccessfully);
            Assert.Empty(fixture.DeviceNames);
            Assert.False(fixture.Loaded);
            Assert.Empty(fixture.Error);

            Task current = fixture.Load();
            fixture.Complete(1, "Current profile device");
            Assert.True(current.IsCompletedSuccessfully);
            Assert.Equal(new[] { "Current profile device" }, fixture.DeviceNames);
            Assert.False(fixture.Loading);
        });
    }

    [Fact]
    public void OlderFailure_DoesNotOverwriteNewerFailure()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task older = fixture.Load();
            Task newer = fixture.Load();
            fixture.Fail(1, "Current error");
            fixture.Fail(0, "Stale error");
            Assert.True(older.IsCompletedSuccessfully);
            Assert.True(newer.IsCompletedSuccessfully);
            Assert.Contains("Current error", fixture.Error);
            Assert.DoesNotContain("Stale error", fixture.Error);
            Assert.True(fixture.Loaded);
            Assert.False(fixture.Loading);
        });
    }

    [Fact]
    public void ResetBeforeFirstContinuation_DoesNotStartBackendRequests()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new DeviceFixture();
            Task loading = fixture.Load(runContinuations: false);
            fixture.Reset();
            fixture.Drain();
            Assert.True(loading.IsCompletedSuccessfully);
            Assert.Empty(fixture.Service.Requests);
            Assert.False(fixture.Loading);
            Assert.False(fixture.Loaded);
            Assert.Empty(fixture.Error);
        });
    }

    private sealed class DeviceFixture : IDisposable
    {
        private readonly string _configPath = Path.GetTempFileName();
        private readonly string _sessionPath = Path.GetTempFileName();
        private readonly object _originalSelection;
        private readonly SynchronizationContext? _originalContext = SynchronizationContext.Current;
        private readonly QueuedContext _context = new();
        private readonly List<Control> _controls = new();
        private readonly Form1 _form = (Form1)RuntimeHelpers.GetUninitializedObject(typeof(Form1));
        internal ControlledDeviceService Service { get; } = new();
        internal DevicesControl View { get; } = new();
        internal bool Loading => GetField<bool>(_form, "_devicesLoading");
        internal bool Loaded => GetField<bool>(_form, "_devicesLoadedOnce");
        internal string Error => GetField<string>(_form, "_devicesErrorMessage");
        internal string?[] DeviceNames => GetField<IReadOnlyList<DeviceListItem>>(_form, "_visibleAccountDevices")
            .Select(item => item.Device.DeviceName).ToArray();

        internal DeviceFixture()
        {
            _originalSelection = SelectionField.GetValue(null)!;
            SelectProfile("profile-a");
            var profiles = new[] { "profile-a", "profile-b" }.Select(userId => new DesktopRegistration
            {
                UserId = userId,
                Namespaces = new List<NamespaceItemDto>
                {
                    new() { NamespaceId = "server", Namespace = "Test", Url = "https://devices.test",
                        AccountId = "account", DesktopDeviceId = "desktop", EncDek = "encrypted" }
                }
            }).ToArray();
            File.WriteAllText(_configPath, JsonSerializer.Serialize(profiles));
            var config = (RegistrationConfigStore)RuntimeHelpers.GetUninitializedObject(typeof(RegistrationConfigStore));
            SetField(config, "filePath", _configPath);
            var registered = (RegisteredDeviceService)RuntimeHelpers.GetUninitializedObject(typeof(RegisteredDeviceService));
            SetField(registered, "_configStore", config);
            var sessions = (AuthorizationSessionStore)RuntimeHelpers.GetUninitializedObject(typeof(AuthorizationSessionStore));
            SetField(sessions, "_syncRoot", new object());
            SetField(sessions, "_filePath", _sessionPath);
            string snapshots = JsonSerializer.Serialize(new[] { "profile-a", "profile-b" }.Select(userId => new
            {
                Active = true, BoundUserId = userId, NamespaceId = "server", BaseUrl = "https://devices.test",
                AccountId = "account", DesktopId = "desktop"
            }));
            FieldInfo snapshotsField = Field(typeof(AuthorizationSessionStore), "_snapshots");
            snapshotsField.SetValue(sessions, JsonSerializer.Deserialize(snapshots, snapshotsField.FieldType));
            var controller = new TrayMenuController((_, _) => { }, () => { });
            controller.SetActiveSection(TrayMenuSection.Devices);
            SetField(_form, "_trayMenuController", controller);
            SetField(_form, "_registeredDeviceService", registered);
            SetField(_form, "_authorizationSessionStore", sessions);
            SetField(_form, "_accountDeviceService", Service);
            SetField(_form, "_devicesView", View);
            SetField(_form, "_devicesErrorMessage", "");
            SetField(_form, "_devicePendingDeletionKey", "");
            SetField(_form, "_selectedDeviceSourceValue", "__all_devices__");
            SetField(_form, "_visibleAccountDevices", Array.Empty<DeviceListItem>());
            FieldInfo resultsField = Field(typeof(Form1), "_accountDeviceServerResults");
            resultsField.SetValue(_form, Array.CreateInstance(resultsField.FieldType.GenericTypeArguments[0], 0));
            AddControl("_quickPanelContainer", new Panel());
            AddControl("_quickPanelDetailPanel", new Panel { Height = 700 });
            AddControl("_quickPanelDetailTitleLabel", new Label());
            AddControl("_quickPanelDetailDescriptionLabel", new Label());
            AddControl("_quickPanelSelectionCard", new Panel());
            AddControl("_quickPanelMetricsCard", new Panel());
            AddControl("_quickPanelInsightCard", new Panel());
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        internal Task Load(bool runContinuations = true)
        {
            var task = (Task)Invoke("LoadAccountDevicesAsync")!;
            if (runContinuations) _context.Drain();
            return task;
        }

        internal void Drain() => _context.Drain();

        internal void Complete(int index, string name)
        {
            Service.Requests[index].SetResult(new[] { new AccountDeviceDto { DeviceName = name, MobileDeviceId = name } });
            _context.Drain();
        }

        internal void Fail(int index, string error)
        {
            Service.Requests[index].SetException(new InvalidOperationException(error));
            _context.Drain();
        }

        internal void Reset() => Invoke("ResetDevicesState");

        internal void SelectProfile(string userId)
        {
            ConstructorInfo constructor = SelectionField.FieldType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single(item => item.GetParameters().Length == 2);
            SelectionField.SetValue(null, constructor.Invoke(new object[] { userId, "server" }));
        }

        private static FieldInfo SelectionField => typeof(AppEnvironment).GetField("_activeSelection", BindingFlags.Static | BindingFlags.NonPublic)!;
        private object? Invoke(string name) => typeof(Form1).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_form, null);
        private void AddControl(string field, Control control)
        {
            _controls.Add(control);
            SetField(_form, field, control);
        }

        public void Dispose()
        {
            SelectionField.SetValue(null, _originalSelection);
            View.Dispose();
            foreach (Control control in _controls) control.Dispose();
            SynchronizationContext.SetSynchronizationContext(_originalContext);
            File.Delete(_configPath);
            File.Delete(_sessionPath);
        }
    }

    private sealed class ControlledDeviceService : IAccountDeviceService
    {
        internal List<TaskCompletionSource<IReadOnlyList<AccountDeviceDto>>> Requests { get; } = new();
        public Task<IReadOnlyList<AccountDeviceDto>> ReadDevicesAsync(string baseUrl, string accountId, string desktopDeviceId)
        {
            var response = new TaskCompletionSource<IReadOnlyList<AccountDeviceDto>>();
            Requests.Add(response);
            return response.Task;
        }
        public Task DeleteDeviceAsync(AccountDeviceDto device, string baseUrl, string accountId,
            string desktopDeviceId, bool removeLocalRegistrationOnDeviceNotFound = true) => throw new NotSupportedException();
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Enqueue((callback, state));
        internal void Drain()
        {
            while (_callbacks.TryDequeue(out var work)) work.Callback(work.State);
        }
    }

    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(type.Name, name);
    private static T GetField<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target)!;
    private static void SetField(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}