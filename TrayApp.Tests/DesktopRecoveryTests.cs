using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using Xunit;

namespace TrayApp.Tests;

public sealed class DesktopRecoveryTests
{
    private const string RecoveryId = "1bf68dbf-bc9b-4ab1-9a61-067f61592426";
    private const string CandidateId = "e2d2c934-c054-4519-a0af-d469c1d7aff4";
    private static readonly RecoveryTarget Target = new("profile-two", "Alice", "database-two", "Work",
        "https://recovery.example:8443", "account-two", "desktop-two", "original-ciphertext");

    [Fact]
    public async Task FullWorkflow_UsesSelectedServerSevenEndpointsAndExistingSignatureProtocol()
    {
        using var fixture = new Fixture();
        using var service = fixture.Service();
        fixture.Reply(new { recoveryId = RecoveryId });
        await service.StartAsync(default);
        Assert.Equal("email_pending", service.Session.Status);
        Assert.Throws<InvalidOperationException>(() => service.QrPayload());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResendAsync(default));
        service.Session.EmailResendAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        fixture.Reply(State("email_pending"));
        await service.ResendAsync(default);
        fixture.Reply(State("email_confirmed"));
        await service.ConfirmAsync("000012345678", default);
        var qr = JsonNode.Parse(service.QrPayload())!.AsObject();
        Assert.Equal(4, qr.Count);
        Assert.Equal("recovery", qr["type"]!.GetValue<string>());
        Assert.Equal(1, qr["version"]!.GetValue<int>());
        Assert.Equal(RecoveryId, qr["recoveryId"]!.GetValue<string>());
        Assert.Equal(Target.BaseUrl, qr["apiBaseUrl"]!.GetValue<string>());

        fixture.Reply(fixture.Selection());
        await service.PairAsync("000098765432", default);
        Assert.Equal(CandidateId, fixture.Store.Saved!.Selected!.CandidateId);
        Assert.Throws<InvalidOperationException>(() => service.QrPayload());

        fixture.Reply(State("dek_ready"));
        await service.UploadDekAsync(default);
        Assert.Equal("dek_ready", service.Session.Status);
        Assert.False(service.IsTerminal);
        fixture.Reply(State("completed"));
        await service.RefreshAsync(default);
        Assert.True(service.IsTerminal);
        Assert.Null(service.Session.Selected);
        Assert.Null(fixture.Store.Saved!.PendingJson);
        Assert.Equal(1, fixture.Keys.Encryptions);
        Assert.All(fixture.Keys.DesktopIds, id => Assert.Equal(Target.DesktopDeviceId, id));
        Assert.Equal(new[] { "recovery", "recovery/resend", "recovery/confirmation",
            "recovery/pairing", "recovery/dek", "recovery/status" }, fixture.Requests.Select(r => r.Path));
        Assert.Equal("000012345678", fixture.Requests[2].Body["code"]!.GetValue<string>());
        Assert.Equal("000098765432", fixture.Requests[3].Body["pairingCode"]!.GetValue<string>());
        Assert.Equal(CandidateId, fixture.Requests[4].Body["candidateId"]!.GetValue<string>());
        Assert.Equal("mobile-new", fixture.Requests[4].Body["mobileDeviceId"]!.GetValue<string>());
        Assert.Equal(fixture.Keys.Dek, fixture.Mobile.Decrypt(
            Convert.FromBase64String(fixture.Requests[4].Body["encDek"]!.GetValue<string>()), RSAEncryptionPadding.OaepSHA1));
        Assert.DoesNotContain(Target.EncDek, fixture.Requests[4].Raw);
        Assert.Equal(fixture.Requests.Count, fixture.Requests.Select(r => r.Body["nonce"]!.GetValue<string>()).Distinct().Count());
        Assert.Equal(5, fixture.Requests.Where(r => r.Path != "recovery/status")
            .Select(r => r.Body["requestId"]!.GetValue<string>()).Distinct().Count());
    }

    [Fact]
    public async Task LostStartResponse_ResumesSameRequestWithFreshNonceWithoutNewEmailOperation()
    {
        using var fixture = new Fixture();
        fixture.Fail(new HttpRequestException("Response lost"));
        using (var first = fixture.Service())
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => first.StartAsync(default));
            Assert.True(first.HasPendingRequest);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.StartAsync(default));
        }
        fixture.Reply(new { recoveryId = RecoveryId });
        using var resumed = fixture.Service();
        await resumed.RetryAsync(default);
        Assert.Equal(RecoveryId, resumed.Session.RecoveryId);
        AssertSameMutation(fixture.Requests[0], fixture.Requests[1]);
        Assert.Equal(2, fixture.Requests.Count);
    }

    [Fact]
    public async Task LostDekResponse_ReusesPersistedCiphertextAfterReopen()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("mobile_selected");
        fixture.Store.Saved.Selected = fixture.Selection();
        fixture.Fail(new HttpRequestException("Response lost"));
        using (var first = fixture.Service())
            await Assert.ThrowsAsync<HttpRequestException>(() => first.UploadDekAsync(default));
        Assert.Equal(1, fixture.Keys.Encryptions);
        fixture.Reply(State("dek_ready"));
        using var resumed = fixture.Service();
        await resumed.RetryAsync(default);
        AssertSameMutation(fixture.Requests[0], fixture.Requests[1]);
        Assert.Equal(1, fixture.Keys.Encryptions);
        Assert.Equal("dek_ready", resumed.Session.Status);
    }

    [Fact]
    public async Task Cancellation_IsSignedAndCannotBeFollowedByTransfer()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_confirmed");
        fixture.Reply(State("cancelled"));
        using var service = fixture.Service();
        await service.CancelAsync(default);
        Assert.Equal("recovery/cancellation", fixture.Requests.Single().Path);
        Assert.Equal("cancellation", fixture.Requests.Single().Body["operation"]!.GetValue<string>());
        Assert.True(service.IsTerminal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadDekAsync(default));
        service.Reset();
        Assert.Equal("not_started", service.Session.Status);
        Assert.Null(fixture.Store.Saved);
    }

    [Theory]
    [InlineData(400, "Incorrect email code", false, "email_pending")]
    [InlineData(401, "Invalid signature", false, "revoked")]
    [InlineData(409, "Recovery delivery outcome is pending", true, "email_pending")]
    [InlineData(409, "Wrong state", false, "email_pending")]
    [InlineData(410, "Recovery expired", false, "expired")]
    [InlineData(410, "Email code expired or invalidated", false, "email_pending")]
    [InlineData(429, "Email code attempt limit reached", false, "locked")]
    [InlineData(500, "Server error", true, "email_pending")]
    public async Task ApiFailures_PreserveUncertainRequestsAndRecognizeTerminalOutcomes(
        int status, string message, bool pending, string expected)
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        fixture.Respond(_ => Error(status, message));
        using var service = fixture.Service();
        var error = await Assert.ThrowsAsync<RecoveryClientException>(() => service.ConfirmAsync("000012345678", default));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(pending, service.HasPendingRequest);
        Assert.Equal(expected, service.Session.Status);
    }

    [Fact]
    public async Task RateLimit_IsPersistedAndNeverAutomaticallyResendsEmail()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        fixture.Respond(_ =>
        {
            var response = Error(429, "Email rate limit reached");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return response;
        });
        using (var service = fixture.Service())
        {
            var error = await Assert.ThrowsAsync<RecoveryClientException>(() => service.ResendAsync(default));
            Assert.True(error.RetryAt > DateTimeOffset.UtcNow.AddSeconds(110));
            Assert.False(service.IsTerminal);
            Assert.False(service.HasPendingRequest);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResendAsync(default));
        }
        using var resumed = fixture.Service();
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.ResendAsync(default));
        Assert.Single(fixture.Requests);
    }

    [Fact]
    public async Task SmtpUncertainty_IsExplicitTerminalAndNeverAutomaticallyRetried()
    {
        using var fixture = new Fixture();
        fixture.Respond(_ => Error(503, "Email delivery failed or is uncertain; code invalidated"));
        using var service = fixture.Service();
        await Assert.ThrowsAsync<RecoveryClientException>(() => service.StartAsync(default));
        Assert.Equal("delivery_failed", service.Session.Status);
        Assert.False(service.HasPendingRequest);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetryAsync(default));
        Assert.Single(fixture.Requests);
    }

    [Fact]
    public async Task ExpiredCandidate_DoesNotMarkWholeRecoveryExpiredAndCanStillCancel()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("mobile_selected");
        fixture.Store.Saved.Selected = fixture.Selection();
        fixture.Respond(_ => Error(410, "Candidate expired"));
        using var service = fixture.Service();
        await Assert.ThrowsAsync<RecoveryClientException>(() => service.UploadDekAsync(default));
        Assert.False(service.IsTerminal);
        Assert.Equal("mobile_selected", service.Session.Status);
        fixture.Reply(State("cancelled"));
        await service.CancelAsync(default);
        Assert.True(service.IsTerminal);
    }

    [Fact]
    public async Task ExpiredEmailCode_CanBeResentWithoutStartingAnotherRecovery()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        fixture.Respond(_ => Error(410, "Email code expired or invalidated"));
        using var service = fixture.Service();
        await Assert.ThrowsAsync<RecoveryClientException>(() => service.ConfirmAsync("000012345678", default));
        fixture.Reply(State("email_pending"));
        await service.ResendAsync(default);
        Assert.Equal(RecoveryId, service.Session.RecoveryId);
        Assert.False(service.IsTerminal);
        Assert.False(service.HasPendingRequest);
        Assert.Equal(new[] { "recovery/confirmation", "recovery/resend" }, fixture.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task PersistenceFailure_PreventsNetworkAndRetainsResponseReplayWhenResultCannotBeSaved()
    {
        using var fixture = new Fixture();
        using var service = fixture.Service();
        fixture.Store.FailSave = session => session.PendingJson != null;
        await Assert.ThrowsAsync<IOException>(() => service.StartAsync(default));
        Assert.Empty(fixture.Requests);
        fixture.Store.FailSave = session => session.PendingJson == null;
        fixture.Reply(new { recoveryId = RecoveryId });
        await Assert.ThrowsAsync<IOException>(() => service.RetryAsync(default));
        Assert.True(service.HasPendingRequest);
        Assert.Equal(string.Empty, service.Session.RecoveryId);
        fixture.Store.FailSave = null;
        fixture.Reply(new { recoveryId = RecoveryId });
        await service.RetryAsync(default);
        AssertSameMutation(fixture.Requests[0], fixture.Requests[1]);
    }

    [Fact]
    public async Task SelectedCandidate_IsPersistedBeforeUploadAndInvalidKeyCannotReceiveDek()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_confirmed");
        var selected = fixture.Selection();
        selected.PublicKey = "invalid base64!";
        fixture.Reply(selected);
        using var service = fixture.Service();
        await Assert.ThrowsAsync<CryptographicException>(() => service.PairAsync("000098765432", default));
        Assert.Equal("email_confirmed", service.Session.Status);
        Assert.True(service.HasPendingRequest);
        Assert.Null(fixture.Store.Saved!.Selected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadDekAsync(default));
        Assert.Equal(0, fixture.Keys.Encryptions);
    }

    [Fact]
    public async Task InvalidResponse_DoesNotReportFalseSuccessOrLosePendingMutation()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        fixture.Reply(State("completed"));
        using var service = fixture.Service();
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ConfirmAsync("000012345678", default));
        Assert.Equal("email_pending", service.Session.Status);
        Assert.True(service.HasPendingRequest);
    }

    [Fact]
    public async Task FreshStatus_OverridesHistoricalMutationReplayAndClearsTerminalPending()
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        fixture.Fail(new HttpRequestException("Lost confirmation"));
        using var service = fixture.Service();
        await Assert.ThrowsAsync<HttpRequestException>(() => service.ConfirmAsync("000012345678", default));
        fixture.Reply(State("completed"));
        await service.RefreshAsync(default);
        Assert.Equal("completed", service.Session.Status);
        Assert.False(service.HasPendingRequest);
    }

    [Fact]
    public async Task CancellationToken_LeavesDurablePendingOperationForExplicitRetry()
    {
        using var fixture = new Fixture();
        fixture.Fail(new OperationCanceledException());
        using var service = fixture.Service();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartAsync(default));
        Assert.True(service.HasPendingRequest);
        Assert.NotNull(fixture.Store.Saved!.PendingJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData(" 12345678901")]
    [InlineData("123456789012 ")]
    [InlineData("１２３４５６７８９０１２")]
    [InlineData("12345678901a")]
    public async Task Codes_MustBeTwelveAsciiDigitsBeforeAnyRequest(string code)
    {
        using var fixture = new Fixture();
        fixture.Store.Saved = Saved("email_pending");
        using var service = fixture.Service();
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfirmAsync(code, default));
        Assert.Empty(fixture.Requests);
        Assert.False(service.HasPendingRequest);
    }

    [Theory]
    [InlineData("ftp://server.example")]
    [InlineData("https://user:password@server.example")]
    [InlineData("https://server.example/path")]
    [InlineData("https://server.example/?a=1")]
    [InlineData("https://server.example/#fragment")]
    [InlineData("not-a-url")]
    public void Target_RejectsUnsafeServerOrigins(string url) =>
        Assert.Throws<InvalidDataException>(() => RecoveryTarget.NormalizeOrigin(url));

    [Theory]
    [InlineData("http://server.example:8080/", "http://server.example:8080")]
    [InlineData("https://server.example:8443/", "https://server.example:8443")]
    public void Target_AllowsHttpAndHttpsOrigins(string url, string expected) =>
        Assert.Equal(expected, RecoveryTarget.NormalizeOrigin(url));

    [Fact]
    public async Task HttpTarget_SendsSignedRequestAndPreservesHttpInQr()
    {
        var target = Target with { BaseUrl = "http://recovery.example:8080" };
        using var fixture = new Fixture(target);
        using var service = fixture.Service();
        fixture.Reply(new { recoveryId = RecoveryId });
        await service.StartAsync(default);
        fixture.Reply(State("email_confirmed"));
        await service.ConfirmAsync("000012345678", default);
        Assert.Equal(target.BaseUrl, JsonNode.Parse(service.QrPayload())!["apiBaseUrl"]!.GetValue<string>());
        Assert.Equal(2, fixture.Requests.Count);
    }

    [Fact]
    public void Target_UsesNamespaceIdentityRatherThanActiveProfileFallback()
    {
        var server = new NamespaceItemDto
        {
            NamespaceId = "two",
            Namespace = "Work",
            Url = Target.BaseUrl,
            AccountId = "account-server-two",
            DesktopDeviceId = "desktop-server-two",
            EncDek = "dek-server-two"
        };
        var registration = new DesktopRegistration
        {
            UserId = "profile",
            UserName = "Alice",
            AccountId = "global-account",
            DesktopDeviceId = "global-desktop",
            EncDek = "global-dek",
            Namespaces = [server]
        };
        var target = RecoveryTarget.From(registration, server);
        Assert.Equal(server.DesktopDeviceId, target.DesktopDeviceId);
        Assert.Equal(server.AccountId, target.AccountId);
        Assert.Equal(server.EncDek, target.EncDek);
        server.EncDek = string.Empty;
        Assert.Throws<InvalidDataException>(() => RecoveryTarget.From(registration, server));
    }

    [Fact]
    public void Crypto_UsesOaepSha1AndExactOriginal32ByteDek()
    {
        using RSA mobile = RSA.Create(2048);
        using RSA imported = RecoveryKeyService.ImportMobileKey(Convert.ToBase64String(mobile.ExportSubjectPublicKeyInfo()));
        byte[] dek = RandomNumberGenerator.GetBytes(32);
        byte[] ciphertext = Convert.FromBase64String(RecoveryKeyService.EncryptDek(imported, dek));
        Assert.Equal(dek, mobile.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA1));
        Assert.Throws<CryptographicException>(() => mobile.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256));
        Assert.Throws<CryptographicException>(() => RecoveryKeyService.EncryptDek(imported, new byte[31]));
        using RSA weak = RSA.Create(1024);
        Assert.Throws<CryptographicException>(() => RecoveryKeyService.ImportMobileKey(Convert.ToBase64String(weak.ExportSubjectPublicKeyInfo())));
        Assert.Throws<CryptographicException>(() => RecoveryKeyService.ImportMobileKey(
            Convert.ToBase64String(mobile.ExportSubjectPublicKeyInfo()) + "\n"));
    }

    [Fact]
    public void ProtectedStore_RoundTripsPendingCodesIsolatesTargetsAndRejectsCorruption()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TrayAppRecoveryTests-" + Guid.NewGuid().ToString("N"));
        var store = new RecoverySessionStore(directory);
        try
        {
            var session = Saved("email_pending");
            session.PendingOperation = "confirmation";
            session.PendingJson = "{\"code\":\"000012345678\"}";
            store.Save(Target, session);
            string file = Assert.Single(Directory.GetFiles(directory));
            Assert.DoesNotContain("000012345678", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
            Assert.Equal(session.PendingJson, store.Load(Target)!.PendingJson);
            Assert.Null(store.Load(Target with { UserId = "different-profile" }));
            Assert.Null(store.Load(Target with { BaseUrl = "https://different.example" }));
            Assert.Null(store.Load(Target with { AccountId = "different-account" }));
            File.WriteAllBytes(file, [1, 2, 3]);
            Assert.Throws<CryptographicException>(() => store.Load(Target));
            store.Delete(Target);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public void Card_OnlyShowsQrAfterFreshConfirmationAndNeverConfusesDekReadyWithCompleted()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            var registration = Registration();
            using var form = new RecoveryControl(registration, target =>
            {
                Assert.Equal(Target, target);
                return fixture.Service();
            });
            using var host = ShowCard(form);
            T ControlNamed<T>(string name) where T : Control =>
                Assert.IsType<T>(Assert.Single(form.Controls.Find(name, searchAllChildren: true)));
            var primary = ControlNamed<Button>("RecoveryPrimary");
            var qr = ControlNamed<PictureBox>("RecoveryQr");
            var status = ControlNamed<Label>("RecoveryStatus");
            var email = ControlNamed<TextBox>("RecoveryEmailCode");
            var confirm = ControlNamed<Button>("RecoveryConfirm");
            var resend = ControlNamed<Button>("RecoveryResend");
            var pairing = ControlNamed<TextBox>("RecoveryPairingCode");
            var pair = ControlNamed<Button>("RecoveryPair");
            Assert.Null(qr.Image);
            Assert.Equal("Start recovery", primary.Text);
            fixture.Reply(new { recoveryId = RecoveryId });
            fixture.Reply(State("email_pending"));
            primary.PerformClick();
            Assert.Contains("registered email address", status.Text);
            Assert.False(resend.Enabled);
            Assert.True(confirm.Enabled);
            Assert.Null(qr.Image);
            email.Text = "000012345678";
            fixture.Reply(State("email_confirmed"));
            fixture.Reply(State("email_confirmed"));
            confirm.PerformClick();
            Assert.NotNull(qr.Image);
            Assert.True(qr.Visible);
            Assert.Empty(email.Text);
            Assert.True(pair.Enabled);
            pairing.Text = "000098765432";
            fixture.Reply(fixture.Selection());
            fixture.Reply(State("mobile_selected"));
            pair.PerformClick();
            Assert.Null(qr.Image);
            Assert.Empty(pairing.Text);
            Assert.Contains("New phone", ControlNamed<Label>("RecoverySelection").Text);
            Assert.Equal("Transfer DEK to selected mobile", primary.Text);
            fixture.Reply(State("dek_ready"));
            fixture.Reply(State("dek_ready"));
            primary.PerformClick();
            Assert.Contains("Waiting for the mobile", status.Text);
            Assert.DoesNotContain("Recovery completed", status.Text);
            fixture.Reply(State("completed"));
            ControlNamed<Button>("RecoveryRefresh").PerformClick();
            Assert.Contains("Recovery completed", status.Text);
            Assert.Equal("New recovery", primary.Text);
            Assert.False(ControlNamed<Button>("RecoveryCancel").Enabled);
            Assert.Empty(ControlNamed<Label>("RecoveryError").Text);
            host.ClientSize = new Size(420, 300);
            form.PerformLayout();
            Assert.True(form.Controls.OfType<FlowLayoutPanel>().Single().AutoScroll);
            Assert.True(form.ClientRectangle.Contains(form.Controls[0].Bounds));
            Assert.All(form.Controls.Find("RecoveryServer", true), server => Assert.True(server.Width < form.Width));
        });
    }

    [Fact]
    public void Card_SavedConfirmationRequiresFreshStatusBeforeExposingQr()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            fixture.Store.Saved = Saved("email_confirmed");
            using var form = new RecoveryControl(Registration(), _ => fixture.Service());
            using var host = ShowCard(form);
            var qr = Assert.IsType<PictureBox>(form.Controls.Find("RecoveryQr", true).Single());
            var primary = Assert.IsType<Button>(form.Controls.Find("RecoveryPrimary", true).Single());
            Assert.Equal("Resume recovery", primary.Text);
            Assert.Null(qr.Image);
            fixture.Respond(_ => Error(410, "Recovery expired"));
            primary.PerformClick();
            Assert.Null(qr.Image);
            Assert.Equal("New recovery", primary.Text);
            Assert.Single(fixture.Requests);
        });
    }

    [Fact]
    public void Card_NotStartedRecoveryDoesNotShowStaleDeadline()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            fixture.Store.Saved = new RecoverySession
            {
                TargetKey = Target.StorageKey,
                Status = "not_started",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            };
            using var card = new RecoveryControl(Registration(), _ => fixture.Service());
            using var host = ShowCard(card);

            string status = card.Controls.Find("RecoveryStatus", true).Single().Text;
            Assert.Equal("Ready. Start recovery to send an email confirmation code.", status);
            Assert.DoesNotContain("deadline has passed", status, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Card_ReloadsFirstServerWhenItsSelectionIsCommitted()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            int serviceCreations = 0;
            using var card = new RecoveryControl(Registration(), _ =>
            {
                serviceCreations++;
                return fixture.Service();
            });
            using var host = ShowCard(card);
            var server = Assert.IsType<ComboBox>(card.Controls.Find("RecoveryServer", true).Single());
            Assert.Equal(0, server.SelectedIndex);
            Assert.Equal(1, serviceCreations);

            typeof(ComboBox).GetMethod("OnSelectionChangeCommitted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(server, [EventArgs.Empty]);

            Assert.Equal(2, serviceCreations);
        });
    }

    [Fact]
    public void Card_LeavingDoesNotCancelServerAndPendingStartRemainsResumable()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            using (var form = new RecoveryControl(Registration(), _ => fixture.Service()))
            using (var host = ShowCard(form))
            {
                fixture.Fail(new HttpRequestException("Response lost"));
                var primary = Assert.IsType<Button>(form.Controls.Find("RecoveryPrimary", true).Single());
                primary.PerformClick();
                Assert.Equal("Retry last request", primary.Text);
                var error = Assert.IsType<Label>(form.Controls.Find("RecoveryError", true).Single());
                Assert.Contains("request could not be confirmed", error.Text);
                Assert.DoesNotContain("000012345678", error.Text);
                host.Close();
            }
            Assert.Single(fixture.Requests);
            Assert.NotNull(fixture.Store.Saved!.PendingJson);
            using var resumed = new RecoveryControl(Registration(), _ => fixture.Service());
            using var resumedHost = ShowCard(resumed);
            var retry = Assert.IsType<Button>(resumed.Controls.Find("RecoveryPrimary", true).Single());
            Assert.Equal("Retry last request", retry.Text);
            fixture.Reply(new { recoveryId = RecoveryId });
            fixture.Reply(State("email_pending"));
            retry.PerformClick();
            AssertSameMutation(fixture.Requests[0], fixture.Requests[1]);
            Assert.DoesNotContain(fixture.Requests, request => request.Path == "recovery/cancellation");
            resumedHost.Close();
        });
    }

    private static Form ShowCard(RecoveryControl card)
    {
        var host = new Form { ClientSize = new Size(578, 352), Opacity = 0 };
        card.Dock = DockStyle.Fill;
        host.Controls.Add(card);
        host.Show();
        return host;
    }

    [Fact]
    public void Card_NavigationPausesPollingAndRequiresFreshStatusBeforeShowingQrAgain()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            fixture.Store.Saved = Saved("email_confirmed");
            using var card = new RecoveryControl(Registration(), _ => fixture.Service());
            using var host = ShowCard(card);
            var primary = Assert.IsType<Button>(card.Controls.Find("RecoveryPrimary", true).Single());
            var qr = Assert.IsType<PictureBox>(card.Controls.Find("RecoveryQr", true).Single());
            var poll = (System.Windows.Forms.Timer)typeof(RecoveryControl).GetField("_poll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(card)!;
            fixture.Reply(State("email_confirmed"));
            primary.PerformClick();
            Assert.NotNull(qr.Image);
            Assert.True(poll.Enabled);
            card.Hide();
            Assert.False(poll.Enabled);
            Assert.Null(qr.Image);
            card.Show();
            Assert.True(poll.Enabled);
            Assert.Null(qr.Image);
            Assert.Equal("Resume recovery", primary.Text);
            Assert.Single(fixture.Requests);
            fixture.Reply(State("email_confirmed"));
            primary.PerformClick();
            Assert.NotNull(qr.Image);
            Assert.Equal(2, fixture.Requests.Count);
        });
    }

    [Fact]
    public void Card_CancellationConfirmationAndErrorsStayInline()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            fixture.Store.Saved = Saved("email_pending");
            using var card = new RecoveryControl(Registration(), _ => fixture.Service());
            using var host = ShowCard(card);
            Button ButtonNamed(string name) => Assert.IsType<Button>(card.Controls.Find(name, true).Single());
            fixture.Reply(State("email_pending"));
            ButtonNamed("RecoveryPrimary").PerformClick();
            Assert.False(ButtonNamed("RecoveryCancelConfirm").Visible);
            ButtonNamed("RecoveryCancel").PerformClick();
            Assert.True(ButtonNamed("RecoveryCancelConfirm").Visible);
            ButtonNamed("RecoveryCancelKeep").PerformClick();
            Assert.False(ButtonNamed("RecoveryCancelConfirm").Visible);
            Assert.Single(fixture.Requests);
            ButtonNamed("RecoveryCancel").PerformClick();
            fixture.Reply(State("cancelled"));
            ButtonNamed("RecoveryCancelConfirm").PerformClick();
            Assert.Equal("recovery/cancellation", fixture.Requests.Last().Path);
            Assert.Contains("Recovery cancelled", card.Controls.Find("RecoveryStatus", true).Single().Text);
            Assert.False(ButtonNamed("RecoveryCancelConfirm").Visible);
            Assert.Same(host, card.FindForm());
            Assert.Empty(host.OwnedForms);
            bool backRequested = false;
            card.BackRequested += (_, _) => backRequested = true;
            ButtonNamed("RecoveryBack").PerformClick();
            Assert.True(backRequested);
        });
    }

    [Fact]
    public void Card_DisposalDuringRequestRetainsDurablePendingOperation()
    {
        RunOnStaThread(() =>
        {
            using var fixture = new Fixture();
            var response = new TaskCompletionSource<HttpResponseMessage>();
            fixture.DelayResponse = response.Task;
            using var card = new RecoveryControl(Registration(), _ => fixture.Service());
            using var host = ShowCard(card);
            Assert.IsType<Button>(card.Controls.Find("RecoveryPrimary", true).Single()).PerformClick();
            Assert.NotNull(fixture.Store.Saved!.PendingJson);
            card.Dispose();
            response.SetCanceled();
            var serviceField = typeof(RecoveryControl).GetField("_service",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (serviceField.GetValue(card) != null && DateTime.UtcNow < timeout)
            {
                Application.DoEvents();
                Thread.Yield();
            }
            Assert.Null(serviceField.GetValue(card));
            Assert.NotNull(fixture.Store.Saved.PendingJson);
            Assert.Single(fixture.Requests);
        });
    }

    private static DesktopRegistration Registration() => new()
    {
        UserId = Target.UserId,
        UserName = Target.ProfileName,
        Namespaces = [new NamespaceItemDto { NamespaceId = Target.NamespaceId, Namespace = Target.ServerName,
                Url = Target.BaseUrl, AccountId = Target.AccountId, DesktopDeviceId = Target.DesktopDeviceId, EncDek = Target.EncDek }]
    };
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Recovery UI test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static RecoverySession Saved(string status) => new()
    {
        TargetKey = Target.StorageKey,
        RecoveryId = RecoveryId,
        Status = status,
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(25)
    };
    private static RecoveryStateDto State(string status) => new()
    {
        RecoveryId = RecoveryId,
        Status = status,
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(25)
    };
    private static HttpResponseMessage Error(int status, string message) => new((HttpStatusCode)status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            error = 70000 + status,
            message,
            internalCode = $"e_recovery_{status}"
        }), Encoding.UTF8, "application/json")
    };
    private static void AssertSameMutation(Captured first, Captured second)
    {
        Assert.Equal(first.Path, second.Path);
        Assert.NotEqual(first.Body["nonce"]!.GetValue<string>(), second.Body["nonce"]!.GetValue<string>());
        var left = first.Body.DeepClone().AsObject(); var right = second.Body.DeepClone().AsObject();
        left.Remove("nonce"); right.Remove("nonce");
        Assert.Equal(left.ToJsonString(), right.ToJsonString());
    }

    private sealed record Captured(string Path, string Raw, JsonObject Body);
    private sealed class Fixture : IDisposable
    {
        public RSA Desktop { get; } = RSA.Create(2048);
        public RSA Mobile { get; } = RSA.Create(2048);
        public FakeStore Store { get; } = new();
        public FakeKeys Keys { get; }
        public List<Captured> Requests { get; } = [];
        private readonly Queue<Func<Captured, HttpResponseMessage>> _responses = new();
        private readonly HttpClient _client;
        public Task<HttpResponseMessage>? DelayResponse { get; set; }
        private readonly RecoveryTarget _target;
        public Fixture(RecoveryTarget? target = null)
        {
            _target = target ?? Target;
            Keys = new FakeKeys(Desktop);
            _client = new HttpClient(new Handler(this));
        }
        public DesktopRecoveryService Service() => new(_target, Store, Keys, new HttpApiClient(_client));
        public RecoverySelectedDto Selection() => new()
        {
            RecoveryId = RecoveryId,
            Status = "mobile_selected",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(25),
            CandidateId = CandidateId,
            MobileDeviceId = "mobile-new",
            DeviceName = "New phone",
            PublicKey = Convert.ToBase64String(Mobile.ExportSubjectPublicKeyInfo())
        };
        public void Reply(object body) => Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
        public void Fail(Exception error) => Respond(_ => throw error);
        public void Respond(Func<Captured, HttpResponseMessage> response) => _responses.Enqueue(response);
        public void Dispose() { _client.Dispose(); Desktop.Dispose(); Mobile.Dispose(); CryptographicOperations.ZeroMemory(Keys.Dek); }
        private sealed class Handler(Fixture owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(owner._target.BaseUrl, request.RequestUri!.GetLeftPart(UriPartial.Authority));
                Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
                string raw = await request.Content.ReadAsStringAsync(token);
                string signature = Assert.Single(request.Headers.GetValues("x-signature"));
                Assert.True(owner.Desktop.VerifyData(Encoding.UTF8.GetBytes(raw), Convert.FromBase64String(signature),
                    HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));
                Assert.False(owner.Desktop.VerifyData(Encoding.UTF8.GetBytes(raw), Convert.FromBase64String(signature),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
                var body = JsonNode.Parse(raw)!.AsObject();
                Assert.True(Guid.TryParseExact(body["nonce"]!.GetValue<string>(), "D", out _));
                Assert.False(body.ContainsKey("accountId"));
                string path = request.RequestUri.AbsolutePath["/api/v1/".Length..];
                var captured = new Captured(path, raw, body);
                owner.Requests.Add(captured);
                if (path == "recovery/status") Assert.False(body.ContainsKey("requestId"));
                else
                {
                    Assert.True(Guid.TryParseExact(body["requestId"]!.GetValue<string>(), "D", out _));
                    Assert.NotNull(owner.Store.Saved!.PendingJson);
                    var saved = JsonNode.Parse(owner.Store.Saved.PendingJson)!.AsObject();
                    Assert.Equal(body["requestId"]!.GetValue<string>(), saved["requestId"]!.GetValue<string>());
                    Assert.False(saved.ContainsKey("nonce"));
                }
                var operations = new Dictionary<string, string>
                {
                    ["recovery"] = "start",
                    ["recovery/confirmation"] = "confirmation",
                    ["recovery/resend"] = "resend",
                    ["recovery/status"] = "status",
                    ["recovery/pairing"] = "pairing",
                    ["recovery/dek"] = "dek",
                    ["recovery/cancellation"] = "cancellation"
                };
                Assert.Equal(operations[path], body["operation"]!.GetValue<string>());
                return owner.DelayResponse != null
                    ? await owner.DelayResponse.WaitAsync(token)
                    : owner._responses.Dequeue()(captured);
            }
        }
    }

    private sealed class FakeStore : IRecoverySessionStore
    {
        public RecoverySession? Saved { get; set; }
        public Func<RecoverySession, bool>? FailSave { get; set; }
        public RecoverySession? Load(RecoveryTarget target) => Clone(Saved);
        public void Save(RecoveryTarget target, RecoverySession session)
        {
            if (FailSave?.Invoke(session) == true) throw new IOException("Disk unavailable");
            Saved = Clone(session);
        }
        public void Delete(RecoveryTarget target) => Saved = null;
        private static RecoverySession? Clone(RecoverySession? session) => session == null ? null :
            JsonSerializer.Deserialize<RecoverySession>(JsonSerializer.Serialize(session));
    }
    private sealed class FakeKeys(RSA desktop) : IRecoveryKeyService
    {
        public List<string> DesktopIds { get; } = [];
        public int Encryptions { get; private set; }
        public byte[] Dek { get; } = RandomNumberGenerator.GetBytes(32);
        public IReadOnlyDictionary<string, string> Sign(string desktopId, string json)
        {
            DesktopIds.Add(desktopId);
            return new Dictionary<string, string>
            {
                ["x-signature"] = Convert.ToBase64String(
                desktop.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1))
            };
        }
        public string ReencryptDek(RecoveryTarget target, string publicKey)
        {
            Assert.Equal(Target, target);
            Encryptions++;
            using RSA key = RecoveryKeyService.ImportMobileKey(publicKey);
            return RecoveryKeyService.EncryptDek(key, Dek);
        }
    }
}
