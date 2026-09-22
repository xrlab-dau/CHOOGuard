#if UNITY_INCLUDE_TESTS
using System;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;
using ChooGuard.Presentation.Commands;
using ChooGuard.Presentation.Selection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ChooGuard.Presentation.Input;
using System.Reflection;
using static ChooGuard.Tests.EditMode.Stories.CSPLAY0201Tests;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPLAY0202Tests
    {
        private sealed class Port : IOperationsPort
        {
            public readonly TaskCompletionSource<PreviewResult> Preview = new TaskCompletionSource<PreviewResult>();
            public readonly TaskCompletionSource<CommandReceipt> Submit = new TaskCompletionSource<CommandReceipt>();
            public CommandIntent SeenPreview, SeenSubmit;
            public ReceiptKey LookupKey;
            public int Submits;
            public ReceiptLookup Lookup = new ReceiptLookup(false, null);
            public Task<PreviewResult> PreviewAsync(CommandIntent intent, CancellationToken token) { SeenPreview = intent; return Preview.Task; }
            public Task<CommandReceipt> SubmitAsync(CommandIntent intent, CancellationToken token) { SeenSubmit = intent; Submits++; return Submit.Task; }
            public Task<ReceiptLookup> ReadReceiptAsync(ReceiptKey key, CancellationToken token) { LookupKey = key; return Task.FromResult(Lookup); }
            public Task<SessionProjection> ReadProjectionAsync(ProjectionQuery query, CancellationToken token) => throw new NotSupportedException();
        }
        private sealed class Fixture : IDisposable
        {
            public readonly Port Port = new Port();
            public readonly SelectionService Selection;
            public readonly CommandPreviewController Presenter;
            public readonly CommandIntent Intent = new CommandIntent(new ReceiptKey(Id("run"), Id("requester"), Id("intent")), Id("agency"),
                new[] { Id("team") }, Id("action"), new[] { Id("task"), Id("task2") },
                new[] { new EntityRevision(Id("team"), 1) }, Source, new UtcTimestamp(DateTimeOffset.UtcNow));
            public Fixture()
            {
                var authority = new Authority(); authority.Entities.Add(Id("team"), Entity("team"));
                Selection = new SelectionService(authority); Selection.Replace(new[] { Id("team") }, Id("floor"));
                Presenter = new CommandPreviewController(Port, Selection);
            }
            public TargetResult[] Results => new[] {
                new TargetResult(Id("task"), TargetStatus.QUEUED, Array.Empty<Reason>()),
                new TargetResult(Id("task2"), TargetStatus.REJECTED, new[] {
                    new Reason(Id("NO_AUTHORITY"), ReasonAxis.authority, Id("task2"), new[] { Source }, "Not authorized") }) };
            public PreviewResult Preview => new PreviewResult(Intent.Key, Intent.ReadSet, Results, Source, 5);
            public CommandReceipt Receipt => new CommandReceipt(Intent.Key, new string('a', 64), ReceiptStatus.ACCEPTED, "commit", new Sequence(1), Results);
            public Task Begin() => Presenter.PreviewAsync(Intent, Selection.Current, 1);
            public void Ready()
            {
                var work = Begin(); Port.Preview.SetResult(Preview); work.GetAwaiter().GetResult(); Presenter.Pump();
            }
            public void Dispose() => Presenter.Dispose();
        }
        [Test]
        public void PreviewRequiresExplicitConfirmationAndPumpPreservesUiThread()
        {
            using (var f = new Fixture())
            {
                int uiThread = Thread.CurrentThread.ManagedThreadId;
                f.Presenter.Changed += () => Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(uiThread));
                var work = f.Begin();
                Task.Run(() => f.Port.Preview.SetResult(f.Preview)).GetAwaiter().GetResult();
                work.GetAwaiter().GetResult();
                Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.Loading));
                Assert.That(f.Port.Submits, Is.Zero);
                f.Presenter.Pump();
                Assert.That(f.Presenter.CanConfirm, Is.True);
                Assert.That(f.Presenter.Preview.TargetResults[1].Reasons[0].Code, Is.EqualTo(Id("NO_AUTHORITY")));
                var submit = f.Presenter.ConfirmAsync(); f.Presenter.ConfirmAsync();
                Assert.That(f.Port.Submits, Is.EqualTo(1));
                Assert.That(f.Port.SeenSubmit, Is.SameAs(f.Port.SeenPreview));
                f.Port.Submit.SetResult(f.Receipt); submit.GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.Receipt.TargetResults[0].Status, Is.EqualTo(TargetStatus.QUEUED));
                Assert.That(f.Presenter.Receipt.TargetResults[1].Status, Is.EqualTo(TargetStatus.REJECTED));
                Assert.That(f.Presenter.Receipt.CommitId, Is.EqualTo("commit"));
                f.Port.Lookup = new ReceiptLookup(true, f.Receipt);
                f.Presenter.ConfirmAsync().GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Port.Submits, Is.EqualTo(1));
                Assert.That(f.Port.LookupKey, Is.SameAs(f.Intent.Key));
            }
        }
        [Test]
        public void ExpiryAtBoundaryAndSelectionChangeBlockConfirmation()
        {
            using (var f = new Fixture())
            {
                f.Ready(); f.Presenter.ObserveRevision(5);
                Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.Stale));
                Assert.Throws<InvalidOperationException>(() => f.Presenter.ConfirmAsync());
            }
            using (var f = new Fixture())
            {
                f.Ready(); f.Selection.Clear();
                Assert.Throws<InvalidOperationException>(() => f.Presenter.ConfirmAsync());
                Assert.That(f.Port.Submits, Is.Zero);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void LatePreviewCannotResurrectCancelledOrChangedSelection(bool changeSelection)
        {
            using (var f = new Fixture())
            {
                var work = f.Begin();
                if (changeSelection) f.Selection.Clear(); else f.Presenter.Cancel();
                f.Port.Preview.SetResult(f.Preview); work.GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.Preview, Is.Null);
                Assert.That(f.Presenter.CanConfirm, Is.False);
            }
        }
        [Test]
        public void CancelAfterSubmitRecoversSameKeyAndDoesNotTreatLateResponseAsRollback()
        {
            using (var f = new Fixture())
            {
                f.Ready(); var work = f.Presenter.ConfirmAsync(); f.Presenter.Cancel();
                f.Port.Submit.SetResult(f.Receipt); work.GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.RecoveryRequired));
                Assert.That(f.Presenter.Receipt, Is.Null);
                f.Port.Lookup = new ReceiptLookup(true, f.Receipt);
                f.Presenter.LookupReceiptAsync().GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.Receipt, Is.SameAs(f.Port.Lookup.Receipt));
                Assert.That(f.Port.LookupKey, Is.EqualTo(f.Intent.Key));
            }
        }
        [Test]
        public void FailedSubmitAndMissingLookupRemainUnresolvedWithoutInventingReceipt()
        {
            using (var f = new Fixture())
            {
                f.Ready(); var work = f.Presenter.ConfirmAsync();
                f.Port.Submit.SetException(new InvalidOperationException("PERSISTENCE_UNAVAILABLE"));
                work.GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.Error, Is.EqualTo("PERSISTENCE_UNAVAILABLE"));
                f.Presenter.LookupReceiptAsync().GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.RecoveryRequired));
                Assert.That(f.Presenter.Receipt, Is.Null);
                Assert.Throws<InvalidOperationException>(() => f.Begin());
            }
        }
        [Test]
        public void DisposeIgnoresLateCompletionAndUnsubscribesSelection()
        {
            var f = new Fixture(); var work = f.Begin(); f.Presenter.Dispose();
            f.Port.Preview.SetResult(f.Preview); work.GetAwaiter().GetResult(); f.Selection.Clear();
            Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.Disposed));
            Assert.Throws<ObjectDisposedException>(() => f.Presenter.Pump());
        }
        [Test]
        public void RetryAfterCancelPreservesOriginalIntentAndSingleFlight()
        {
            using (var f = new Fixture())
            {
                f.Ready(); var first = f.Presenter.ConfirmAsync(); f.Presenter.Cancel();
                Assert.That(f.Presenter.CanRetry, Is.True);
                var retry = f.Presenter.RetryOriginalAsync(); f.Presenter.RetryOriginalAsync();
                Assert.That(f.Port.Submits, Is.EqualTo(2));
                Assert.That(f.Port.SeenSubmit, Is.SameAs(f.Intent));
                var receipt = f.Receipt; f.Port.Submit.SetResult(receipt);
                Task.WhenAll(first, retry).GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.Receipt, Is.SameAs(receipt));
                Assert.That(f.Presenter.HasUnresolvedSubmission, Is.False);
            }
        }

        // These tests require Unity; the BCL runner must not claim host lifecycle coverage.
        private sealed class HostFixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("preview", typeof(RectTransform));
            public readonly GameObject RouterObject = new GameObject("router");
            public readonly CommandPreviewPresenter Host;
            public readonly InputContextRouter Router;
            public readonly Button Confirm;
            public readonly TMP_Text Status, Details;
            public HostFixture()
            {
                Root.SetActive(false);
                Host = Root.AddComponent<CommandPreviewPresenter>();
                Router = RouterObject.AddComponent<InputContextRouter>();
                Status = Child("status").AddComponent<TextMeshProUGUI>();
                var viewport = Child("viewport");
                var detailObject = Child("details"); detailObject.transform.SetParent(viewport.transform, false);
                Details = detailObject.AddComponent<TextMeshProUGUI>();
                var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform; scroll.content = Details.rectTransform;
                Field("statusText", Status); Field("targetsText", Details); Field("detailsScroll", scroll);
                foreach (var field in new[] { "confirmButton", "cancelButton", "lookupButton", "retryButton", "previousButton", "nextButton" })
                {
                    var button = Child(field).AddComponent<Button>(); Field(field, button);
                    if (field == "confirmButton") Confirm = button;
                }
            }
            private GameObject Child(string name) { var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(Root.transform, false); return child; }
            private void Field(string name, object value) => typeof(CommandPreviewPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Host, value);
            public void Pump() => Host.SendMessage("Update", SendMessageOptions.RequireReceiver);
            public void Dispose() { UnityEngine.Object.DestroyImmediate(Root); UnityEngine.Object.DestroyImmediate(RouterObject); }
        }
        [Test]
        public void HostDisconnectedIsNotConfirmableAndBrokenWiringRejectsBind()
        {
            using (var f = new Fixture())
            using (var h = new HostFixture())
            {
                h.Root.SetActive(true);
                Assert.That(h.Status.text, Does.Contain("disconnected")); Assert.That(h.Confirm.interactable, Is.False);
                Assert.Throws<ArgumentNullException>(() => h.Host.Bind(null, f.Selection, h.Router));
                UnityEngine.Object.DestroyImmediate(h.Details);
                Assert.Throws<InvalidOperationException>(() => h.Host.Bind(f.Port, f.Selection, h.Router));
                Assert.That(h.Confirm.interactable, Is.False);
            }
        }
        [Test]
        public void HostExplicitConfirmDisableLateResponseAndReenableRecoverOriginalKey()
        {
            using (var f = new Fixture())
            using (var h = new HostFixture())
            {
                h.Host.Bind(f.Port, f.Selection, h.Router); h.Root.SetActive(true);
                var work = h.Host.Begin(f.Intent, f.Selection.Current, 1);
                f.Port.Preview.SetResult(f.Preview); work.GetAwaiter().GetResult(); h.Pump();
                Assert.That(f.Port.Submits, Is.Zero); Assert.That(h.Confirm.interactable, Is.True);
                Assert.That(h.Details.richText, Is.False); Assert.That(h.Status.richText, Is.False);
                h.Confirm.onClick.Invoke(); h.Confirm.onClick.Invoke();
                Assert.That(f.Port.Submits, Is.EqualTo(1)); Assert.That(h.Confirm.interactable, Is.False);
                h.Root.SetActive(false);
                Assert.That(h.Router.ActiveModal, Is.Null);
                Assert.That(h.Host.UnresolvedIntent, Is.SameAs(f.Intent));
                f.Port.Submit.SetResult(f.Receipt);
                h.Root.SetActive(true); h.Pump();
                Assert.That(h.Router.ActiveModal, Is.SameAs(h.Root));
                Assert.That(h.Router.KeyboardOwner, Is.EqualTo(InputContext.Modal));
                Assert.That(h.Host.UnresolvedIntent, Is.SameAs(f.Intent));
                Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.RecoveryRequired));
                f.Port.Lookup = new ReceiptLookup(true, f.Receipt); h.Host.Lookup(); h.Pump();
                Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.Receipt));
                Assert.That(f.Port.LookupKey, Is.SameAs(f.Intent.Key));
                Assert.That(h.Status.text, Does.Contain("not task completion"));
            }
        }
        [Test]
        public void HostRecoveryReenableDefersToOtherModalWithoutLosingOriginalIntent()
        {
            using (var f = new Fixture())
            using (var h = new HostFixture())
            {
                var other = new GameObject("other-modal");
                try
                {
                    h.Host.Bind(f.Port, f.Selection, h.Router); h.Root.SetActive(true);
                    var work = h.Host.Begin(f.Intent, f.Selection.Current, 1);
                    f.Port.Preview.SetResult(f.Preview); work.GetAwaiter().GetResult(); h.Pump();
                    h.Confirm.onClick.Invoke(); h.Root.SetActive(false);
                    h.Router.OpenModal(other);
                    h.Root.SetActive(true); h.Pump();
                    Assert.That(h.Router.ActiveModal, Is.SameAs(other));
                    Assert.That(other.activeSelf, Is.True);
                    Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.RecoveryRequired));
                    Assert.That(h.Host.UnresolvedIntent, Is.SameAs(f.Intent));
                    Assert.That(h.Host.UnresolvedIntent.Key, Is.SameAs(f.Intent.Key));
                    Assert.That(h.Status.text, Does.Contain("interaction deferred"));
                    foreach (var button in h.Root.GetComponentsInChildren<Button>())
                        Assert.That(button.interactable, Is.False, button.name);
                    Assert.That(h.Root.GetComponentInChildren<ScrollRect>().enabled, Is.False);
                    h.Host.Lookup(); h.Host.RetryOriginal();
                    Assert.That(f.Port.LookupKey, Is.Null);
                    Assert.That(f.Port.Submits, Is.EqualTo(1));
                    Assert.That(h.Router.ActiveModal, Is.SameAs(other));
                    Assert.That(other.activeSelf, Is.True);
                    h.Router.CloseModal();
                    Assert.That(other.activeSelf, Is.False);
                    Assert.That(h.Router.ActiveModal, Is.SameAs(h.Root));
                    Assert.That(h.Router.KeyboardOwner, Is.EqualTo(InputContext.Modal));
                    Assert.That(h.Host.UnresolvedIntent, Is.SameAs(f.Intent));
                    Assert.That(h.Root.transform.Find("lookupButton").GetComponent<Button>().interactable, Is.True);
                    Assert.That(h.Root.transform.Find("retryButton").GetComponent<Button>().interactable, Is.True);
                    Assert.That(f.Port.Submits, Is.EqualTo(1));
                    h.Host.Lookup(); h.Pump();
                    Assert.That(f.Port.LookupKey, Is.SameAs(f.Intent.Key));
                    Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.RecoveryRequired));
                    h.Router.CloseModal();
                    Assert.That(h.Router.ActiveModal, Is.Null);
                    Assert.That(h.Root.activeSelf, Is.False);
                    Assert.That(h.Host.UnresolvedIntent, Is.SameAs(f.Intent));
                }
                finally { UnityEngine.Object.DestroyImmediate(other); }
            }
        }
        [Test]
        public void HostOtherModalCloseDoesNotCancelAndDisableInvalidatesLatePreview()
        {
            using (var f = new Fixture())
            using (var h = new HostFixture())
            {
                h.Host.Bind(f.Port, f.Selection, h.Router); h.Root.SetActive(true);
                var work = h.Host.Begin(f.Intent, f.Selection.Current, 1);
                h.Host.SendMessage("OnModalClosed", h.RouterObject, SendMessageOptions.RequireReceiver);
                Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.Loading));
                h.Root.SetActive(false); f.Port.Preview.SetResult(f.Preview); work.GetAwaiter().GetResult();
                h.Root.SetActive(true); h.Pump();
                Assert.That(h.Host.State, Is.EqualTo(CommandPreviewState.Cancelled));
                Assert.That(h.Confirm.interactable, Is.False); Assert.That(f.Port.Submits, Is.Zero);
            }
        }
        [Test]
        public void WrongPreviewKeyIsErrorNotConfirmation()
        {
            using (var f = new Fixture())
            {
                var work = f.Begin();
                f.Port.Preview.SetResult(new PreviewResult(new ReceiptKey(Id("wrong"), Id("requester"), Id("intent")), f.Intent.ReadSet, f.Results, Source, 5));
                work.GetAwaiter().GetResult(); f.Presenter.Pump();
                Assert.That(f.Presenter.State, Is.EqualTo(CommandPreviewState.Error));
                Assert.That(f.Presenter.CanConfirm, Is.False);
            }
        }
    }
}
#endif
