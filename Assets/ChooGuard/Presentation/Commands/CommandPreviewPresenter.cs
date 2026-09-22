using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;
using ChooGuard.Presentation.Selection;
using ChooGuard.Presentation.Input;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.Presentation.Commands
{
    // The composition owner must retain this host while a submission is unresolved. Disabling
    // cancels observation, not effects; re-enabling keeps the original controller and key.
    [DisallowMultipleComponent]
    public sealed class CommandPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text targetsText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button lookupButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private ScrollRect detailsScroll;
        private CommandPreviewController controller;
        private InputContextRouter router;
        private bool listening;
        private string hostError;
        private int targetIndex, reasonIndex, evidencePage;
        private const int EvidencePageSize = 16;
        public CommandPreviewState State => controller?.State ?? CommandPreviewState.Empty;
        public CommandIntent UnresolvedIntent => controller != null && controller.HasUnresolvedSubmission ? controller.Intent : null;
        public string DisplayError => hostError ?? controller?.Error;
        // The owner can retain this immutable intent before destroying the host and recover
        // directly through IOperationsPort.ReadReceiptAsync(intent.Key), never a fresh key.
        public event Action<CommandIntent> RecoveryRequiredOnDestroy;
        private bool ViewReady => statusText != null && targetsText != null && confirmButton != null &&
            cancelButton != null && lookupButton != null && retryButton != null && previousButton != null &&
            nextButton != null && detailsScroll != null && detailsScroll.content == targetsText.rectTransform &&
            detailsScroll.viewport != null;

        public void Bind(IOperationsPort operations, SelectionService selection, InputContextRouter inputRouter)
        {
            if (operations == null || selection == null || inputRouter == null) throw new ArgumentNullException("Command preview dependencies must be explicitly supplied.");
            if (controller != null) throw new InvalidOperationException("Already bound; retain the original session/controller for recovery.");
            if (!ViewReady) { hostError = "UI wiring incomplete; command preview unavailable."; Render(); throw new InvalidOperationException(hostError); }
            router = inputRouter;
            controller = new CommandPreviewController(operations, selection);
            hostError = null;
            if (isActiveAndEnabled) Subscribe();
            Render();
        }
        public Task Begin(CommandIntent intent, SelectionSet snapshot, long revision)
        {
            RequireActive();
            if (router.ActiveModal != null && router.ActiveModal != gameObject)
                throw new InvalidOperationException("Another modal owns input.");
            targetIndex = reasonIndex = evidencePage = 0;
            var work = controller.PreviewAsync(intent, snapshot, revision);
            if (router.ActiveModal == null) router.OpenModal(gameObject, cancelButton.gameObject);
            return work;
        }
        public void ObserveRevision(long revision) { RequireActive(); controller.ObserveRevision(revision); }
        private void RequireActive()
        {
            if (!isActiveAndEnabled || controller == null || router == null || !ViewReady)
                throw new InvalidOperationException("Command preview is inactive, disconnected, or incorrectly wired.");
        }
        private void OnEnable() { Subscribe(); RestoreRecoveryModal(); Render(); }
        private void RestoreRecoveryModal()
        {
            // Do not toggle the host while OnEnable/CloseModal is on the stack. If another
            // modal owns input, retain the request and wait for its ModalClosed event.
            if (isActiveAndEnabled && ViewReady && controller != null && controller.HasUnresolvedSubmission &&
                router != null && router.ActiveModal == null)
                router.OpenModal(gameObject, cancelButton.gameObject);
        }
        private void Subscribe()
        {
            if (listening || controller == null || !ViewReady || router == null) return;
            listening = true;
            controller.Changed += Render;
            router.ModalClosed += OnModalClosed;
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
            lookupButton.onClick.AddListener(Lookup);
            retryButton.onClick.AddListener(RetryOriginal);
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
        }
        private void Unsubscribe()
        {
            if (!listening) return;
            listening = false;
            controller.Changed -= Render;
            if (router != null) router.ModalClosed -= OnModalClosed;
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            if (lookupButton != null) lookupButton.onClick.RemoveListener(Lookup);
            if (retryButton != null) retryButton.onClick.RemoveListener(RetryOriginal);
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        }
        private void OnDisable()
        {
            Unsubscribe();
            controller?.Cancel();
            if (router != null && router.ActiveModal == gameObject) router.CloseModal();
            Render();
        }
        private void OnDestroy()
        {
            Unsubscribe();
            var unresolved = UnresolvedIntent;
            controller?.Dispose();
            if (unresolved != null)
            {
                Debug.LogWarning("Command preview destroyed with unresolved submission; receipt lookup is still required for " + KeyText(unresolved.Key), this);
                RecoveryRequiredOnDestroy?.Invoke(unresolved);
            }
            RecoveryRequiredOnDestroy = null;
        }
        private void Update() { controller?.Pump(); }
        private void OnModalClosed(GameObject closed)
        {
            if (closed == gameObject) controller?.Cancel();
            else RestoreRecoveryModal();
            Render();
        }
        public void Cancel()
        {
            controller?.Cancel();
            if (router != null && router.ActiveModal == gameObject) router.CloseModal();
            else gameObject.SetActive(false);
        }
        // UI event handlers never await into Unity APIs; controller completion is pumped in Update.
        private void Invoke(Func<Task> action)
        {
            try
            {
                RequireActive();
                if (router.ActiveModal != null && router.ActiveModal != gameObject)
                    throw new InvalidOperationException("Another modal owns input.");
                if (router.ActiveModal == null) router.OpenModal(gameObject, cancelButton.gameObject);
                hostError = null; action();
            }
            catch (Exception error) { hostError = "UI/transport error (not a domain reason): " + error.Message; }
            Render();
        }
        public void Confirm() { Invoke(() => { if (controller.CanConfirm) return controller.ConfirmAsync(); return Task.CompletedTask; }); }
        public void Lookup() { Invoke(() => controller.LookupReceiptAsync()); }
        public void RetryOriginal() { Invoke(() => controller.RetryOriginalAsync()); }
        private IReadOnlyList<TargetResult> Results => controller?.Receipt?.TargetResults ?? controller?.Preview?.TargetResults;
        private void Previous()
        {
            var results = Results;
            if (results == null) return;
            if (evidencePage > 0) --evidencePage;
            else
            {
                if (reasonIndex > 0) --reasonIndex;
                else if (targetIndex > 0) { --targetIndex; reasonIndex = Math.Max(0, results[targetIndex].Reasons.Count - 1); }
                evidencePage = LastEvidencePage(results[targetIndex], reasonIndex);
            }
            Render();
        }
        private void Next()
        {
            var results = Results;
            if (results == null) return;
            if (evidencePage < LastEvidencePage(results[targetIndex], reasonIndex)) ++evidencePage;
            else if (reasonIndex + 1 < results[targetIndex].Reasons.Count) { ++reasonIndex; evidencePage = 0; }
            else if (targetIndex + 1 < results.Count) { ++targetIndex; reasonIndex = evidencePage = 0; }
            Render();
        }
        private static int LastEvidencePage(TargetResult target, int reason) => target.Reasons.Count == 0 ? 0 :
            Math.Max(0, (target.Reasons[reason].EvidenceRefs.Count - 1) / EvidencePageSize);
        private static string KeyText(ReceiptKey key) => key.RunId.Value + "/" + key.RequesterId.Value + "/" + key.IntentId.Value;
        public void Localize(TMP_FontAsset font)
        {
            foreach(var label in GetComponentsInChildren<TMP_Text>(true)) if(font!=null) label.font=font;
            Caption(confirmButton,"승인"); Caption(cancelButton,"닫기"); Caption(lookupButton,"처리결과 재조회");
            Caption(retryButton,"원래 명령 재시도"); Caption(previousButton,"이전"); Caption(nextButton,"다음");
            Render();
        }
        private static void Caption(Button button,string text)
        { if(button!=null) { var label=button.GetComponentInChildren<TMP_Text>(true); if(label!=null) label.text=text; } }
        private static string KoreanValue(string value)
        {
            switch(value) {
                case "Empty": return "명령 선택 대기"; case "Loading": return "검토 중";
                case "Ready": return "승인 대기"; case "Stale": return "상태 변경 · 다시 검토 필요";
                case "Submitting": return "처리 중"; case "Receipt": return "처리결과 확인";
                case "RecoveryRequired": return "처리결과 재조회 필요"; case "Cancelled": return "검토 취소";
                case "Error": return "처리 상태 확인 필요"; case "Disposed": return "종료";
                case "ACCEPTED": return "접수됨"; case "REJECTED": return "거절됨";
                case "CONFLICT": return "기존 명령과 충돌"; case "QUEUED": return "처리 대기";
                case "platform": return "승강장"; case "concourse": return "대합실"; case "exit": return "출입구";
                case "ops-1": return "역무 훈련팀"; case "fire-1": return "소방 훈련팀"; case "medical-1": return "의료 훈련팀";
                case "move": return "이동"; case "hold": return "현재 위치 대기";
                default: return "훈련 대상";
            }
        }
        private void Render()
        {
            bool blockedByModal = router != null && router.ActiveModal != null && router.ActiveModal != gameObject;
            bool usable = isActiveAndEnabled && ViewReady && controller != null && router != null && !blockedByModal &&
                (!controller.HasUnresolvedSubmission || router.ActiveModal == gameObject);
            if (detailsScroll != null) detailsScroll.enabled = usable;
            if (confirmButton != null) confirmButton.interactable = usable && controller.CanConfirm;
            if (lookupButton != null) lookupButton.interactable = usable && controller.CanLookup;
            if (retryButton != null) retryButton.interactable = usable && controller.CanRetry;
            if (cancelButton != null) cancelButton.interactable = usable;
            if (statusText != null)
            {
                statusText.richText = false;
                statusText.parseCtrlCharacters = false;
                statusText.text = !ViewReady ? "명령 화면을 준비할 수 없습니다." : controller == null || router == null ?
                    "훈련 세션 연결 대기" : "명령 검토  ·  " + KoreanValue(controller.State.ToString()) +
                    (controller.Intent == null ? "\n팀과 목적지를 선택해 주세요." : "\n" +
                    KoreanValue(controller.Intent.ActingTeamIds[0].Value) + " / " + KoreanValue(controller.Intent.ActionId.Value)) +
                    (controller.Receipt == null ? "\n승인 전에는 훈련 상태에 반영되지 않습니다." : "\n처리결과: " + KoreanValue(controller.Receipt.Status.ToString()) +
                    "\n접수 결과이며, 실제 현장 임무 완료를 의미하지 않습니다.") +
                    (blockedByModal ? "\n다른 화면을 먼저 닫아 주세요." : "") +
                    (DisplayError == null ? "" : "\n처리 여부가 불확실합니다. 원래 명령의 처리결과를 재조회하세요.");
            }
            var results = Results;
            if (results != null && results.Count > 0)
            {
                targetIndex = Math.Min(targetIndex, results.Count - 1);
                reasonIndex = Math.Min(reasonIndex, Math.Max(0, results[targetIndex].Reasons.Count - 1));
            }
            bool hasResults = results != null && results.Count > 0;
            if (previousButton != null) previousButton.interactable = usable && hasResults && (targetIndex > 0 || reasonIndex > 0 || evidencePage > 0);
            if (nextButton != null) nextButton.interactable = usable && hasResults &&
                (targetIndex + 1 < results.Count || reasonIndex + 1 < results[targetIndex].Reasons.Count || evidencePage < LastEvidencePage(results[targetIndex], reasonIndex));
            if (targetsText == null) return;
            targetsText.richText = false;
            targetsText.parseCtrlCharacters = false;
            // One target/reason at a time bounds TMP geometry while every DTO detail remains
            // reachable through Previous/Next and vertical scrolling; no inferred eligibility.
            var text = new StringBuilder();
            if (!hasResults) text.Append("명령 검토 결과를 기다리고 있습니다.");
            else
            {
                var target = results[targetIndex];
                text.Append("목적지 ").Append(targetIndex + 1).Append('/').Append(results.Count).Append(": ").Append(KoreanValue(target.TargetId.Value))
                    .Append("\n처리 상태: ").Append(KoreanValue(target.Status.ToString())).Append("\n\n");
                if (target.Reasons.Count == 0) text.Append("추가 제한 사유가 없습니다.");
                else
                {
                    var reason = target.Reasons[reasonIndex];
                    text.Append("안내 ").Append(reasonIndex + 1).Append('/').Append(target.Reasons.Count).Append("\n");
                    bool korean=false; foreach(var character in reason.Message) if(character >= '\uac00' && character <= '\ud7a3') { korean=true; break; }
                    text.Append(korean ? reason.Message : "훈련 명령의 조건을 확인할 수 없습니다. 팀과 목적지를 다시 선택해 검토해 주세요.");
                    evidencePage = Math.Min(evidencePage, LastEvidencePage(target, reasonIndex));
                    if(reason.EvidenceRefs.Count > 0) text.Append("\n참고자료 ").Append(reason.EvidenceRefs.Count).Append("건 · ").Append(evidencePage + 1).Append('/').Append(LastEvidencePage(target, reasonIndex) + 1);
                }
                text.Append("\n\n가상 훈련 데이터에만 적용되는 명령입니다.");
            }
            targetsText.text = text.ToString();
            if (detailsScroll != null && detailsScroll.content != null)
            {
                detailsScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(300f, targetsText.preferredHeight + 24f));
                detailsScroll.verticalNormalizedPosition = 1;
            }
        }
    }

    public enum CommandPreviewState { Empty, Loading, Ready, Stale, Submitting, Receipt, RecoveryRequired, Cancelled, Error, Disposed }

    // Framework-neutral controller. Construct and call on UI thread. Unity Update must call
    // Pump(): async continuations enqueue DTOs ONLY, never touch views or raise UI events.
    // Wire explicit confirm button to ConfirmAsync; router ModalClosed to Cancel (not
    // InputsCancelled, which is also raised by OpenModal). No keyboard/input ownership here.
    public sealed class CommandPreviewController : IDisposable
    {
        private readonly IOperationsPort operations;
        private readonly SelectionService selection;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ConcurrentQueue<Action> completions = new ConcurrentQueue<Action>();
        private readonly HashSet<ReceiptKey> submittedKeys = new HashSet<ReceiptKey>();
        private CancellationTokenSource cancellation;
        private long generation;
        private long revision;
        private long selectionVersion;
        private bool submitted;
        private bool inFlight;
        private volatile bool disposed;
        public CommandPreviewState State { get; private set; }
        public CommandIntent Intent { get; private set; }
        public PreviewResult Preview { get; private set; }
        public CommandReceipt Receipt { get; private set; }
        public string Error { get; private set; }
        public bool HasUnresolvedSubmission => submitted && Receipt == null;
        public bool CanLookup => submitted && !inFlight;
        public bool CanRetry => HasUnresolvedSubmission && !inFlight && State == CommandPreviewState.RecoveryRequired;
        public bool CanConfirm => State == CommandPreviewState.Ready && Preview != null &&
            selection.Current.Version == selectionVersion && revision < Preview.PreviewExpiresAtRevision;
        // ACCEPTED/QUEUED is never a task-completion signal. Render each DTO target and reasons.
        public event Action Changed;
        public CommandPreviewController(IOperationsPort operations, SelectionService selection)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            selection.Changed += SelectionChanged;
        }
        public Task PreviewAsync(CommandIntent intent, SelectionSet snapshot, long currentRevision)
        {
            Check();
            if (inFlight || (submitted && Receipt == null)) throw new InvalidOperationException("Recover the existing request before opening another preview.");
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (!ReferenceEquals(snapshot, selection.Current) || snapshot.EntityIds.Count == 0)
                throw new ArgumentException("Use the current nonempty selection snapshot.", nameof(snapshot));
            if (currentRevision < revision) throw new ArgumentOutOfRangeException(nameof(currentRevision));
            // Entity/team/vehicle -> command mapping belongs to the source-authorized adapter.
            // Do not silently preview an intent unrelated to what the user selected.
            foreach (var id in snapshot.EntityIds)
                if (!Contains(intent.ActingTeamIds, id) && !Contains(intent.TargetIds, id))
                    throw new ArgumentException("Selection requires an explicit source-authorized command mapping.", nameof(intent));
            if (submittedKeys.Contains(intent.Key)) throw new InvalidOperationException("Previously submitted key: use receipt lookup, not a new preview.");
            Invalidate();
            Intent = intent; Preview = null; Receipt = null; submitted = false;
            selectionVersion = snapshot.Version; revision = currentRevision;
            return Start(CommandPreviewState.Loading, ct => operations.PreviewAsync(intent, ct), result =>
            {
                if (result == null || !intent.Key.Equals(result.Key)) throw new InvalidOperationException("Preview key mismatch.");
                ValidateTargets(intent, result.TargetResults);
                Preview = result;
                SetState(revision >= result.PreviewExpiresAtRevision ? CommandPreviewState.Stale : CommandPreviewState.Ready);
            });
        }
        public Task ConfirmAsync()
        {
            Check();
            if (inFlight) return Task.CompletedTask;
            if (submitted) return LookupReceiptAsync();
            if (!CanConfirm) throw new InvalidOperationException("A current preview and explicit confirmation are required.");
            submitted = true; submittedKeys.Add(Intent.Key);
            var intent = Intent;
            return Start(CommandPreviewState.Submitting, ct => operations.SubmitAsync(intent, ct), AcceptReceipt);
        }
        public Task LookupReceiptAsync()
        {
            Check();
            if (!submitted || Intent == null) throw new InvalidOperationException("No submitted request to recover.");
            if (inFlight) return Task.CompletedTask;
            var key = Intent.Key;
            return Start(CommandPreviewState.Submitting, ct => operations.ReadReceiptAsync(key, ct), result =>
            {
                if (result == null) throw new InvalidOperationException("Missing receipt lookup.");
                if (result.Found) AcceptReceipt(result.Receipt);
                else SetState(CommandPreviewState.RecoveryRequired, "Receipt not found; retry only the original intent/key.");
            });
        }
        // Explicit recovery action, never automatic and never allocates a new key.
        public Task RetryOriginalAsync()
        {
            Check();
            if (inFlight) return Task.CompletedTask;
            if (!submitted || Receipt != null || State != CommandPreviewState.RecoveryRequired)
                throw new InvalidOperationException("Only an unresolved submitted request may be retried.");
            var intent = Intent;
            return Start(CommandPreviewState.Submitting, ct => operations.SubmitAsync(intent, ct), AcceptReceipt);
        }
        public void ObserveRevision(long currentRevision)
        {
            Check();
            if (currentRevision < revision) throw new ArgumentOutOfRangeException(nameof(currentRevision));
            revision = currentRevision;
            if (!submitted && Preview != null && revision >= Preview.PreviewExpiresAtRevision)
                SetState(CommandPreviewState.Stale, "Preview expired; request a fresh preview.");
        }
        public void Cancel()
        {
            Check(); Invalidate(); Preview = null;
            SetState(submitted && Receipt == null ? CommandPreviewState.RecoveryRequired : CommandPreviewState.Cancelled,
                submitted && Receipt == null ? "Cancellation is not rollback; lookup the original receipt key." : null);
        }
        public void Pump()
        {
            Check();
            while (completions.TryDequeue(out var completion)) completion();
        }
        private void SelectionChanged(SelectionSet snapshot)
        {
            if (disposed || Intent == null || snapshot.Version == selectionVersion) return;
            Invalidate(); Preview = null;
            SetState(submitted && Receipt == null ? CommandPreviewState.RecoveryRequired : CommandPreviewState.Stale,
                submitted ? "Selection changed; submitted effects require receipt lookup, not rollback." : "Selection changed; preview again.");
        }
        private void AcceptReceipt(CommandReceipt receipt)
        {
            if (receipt == null || !Intent.Key.Equals(receipt.Key)) throw new InvalidOperationException("Receipt key mismatch.");
            ValidateTargets(Intent, receipt.TargetResults);
            Receipt = receipt; SetState(CommandPreviewState.Receipt);
        }
        private static void ValidateTargets(CommandIntent intent, IReadOnlyList<TargetResult> results)
        {
            var seen = new HashSet<StableId>();
            foreach (var result in results)
                if (!Contains(intent.TargetIds, result.TargetId) || !seen.Add(result.TargetId))
                    throw new InvalidOperationException("Unexpected/duplicate target result.");
            if (seen.Count != intent.TargetIds.Count) throw new InvalidOperationException("Incomplete target results.");
        }
        private static bool Contains(IReadOnlyList<StableId> ids, StableId id)
        { foreach (var candidate in ids) if (candidate.Equals(id)) return true; return false; }
        private Task Start<T>(CommandPreviewState state, Func<CancellationToken, Task<T>> operation, Action<T> apply)
        {
            cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var requestGeneration = ++generation;
            inFlight = true; SetState(state);
            if (disposed || requestGeneration != generation || token.IsCancellationRequested) return Task.CompletedTask;
            return Receive(operation, apply, token, requestGeneration);
        }
        private async Task Receive<T>(Func<CancellationToken, Task<T>> operation, Action<T> apply, CancellationToken token, long requestGeneration)
        {
            T result = default(T); Exception failure = null;
            try { result = await operation(token).ConfigureAwait(false); }
            catch (Exception ex) { failure = ex; }
            if (disposed) return;
            completions.Enqueue(() =>
            {
                if (disposed || requestGeneration != generation) return;
                inFlight = false;
                try
                {
                    if (failure != null) throw failure;
                    apply(result);
                }
                catch (Exception ex)
                { SetState(submitted ? CommandPreviewState.RecoveryRequired : CommandPreviewState.Error, ex.Message); }
            });
        }
        private void Invalidate()
        {
            ++generation; inFlight = false;
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        }
        private void SetState(CommandPreviewState state, string error = null)
        { State = state; Error = error; Changed?.Invoke(); }
        private void Check()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Call from the creating UI thread; use Pump for completions.");
            if (disposed) throw new ObjectDisposedException(nameof(CommandPreviewController));
        }
        public void Dispose()
        {
            if (disposed) return;
            Check(); disposed = true; selection.Changed -= SelectionChanged; Invalidate();
            while (completions.TryDequeue(out _)) { }
            State = CommandPreviewState.Disposed; Changed = null;
        }
    }
}
