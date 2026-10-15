using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ChooGuard.Contracts;

namespace ChooGuard.App.Mvp
{
    public static class MvpBackendProbe
    {
        /// <summary>Focused real-file probe. Retains its unique directory as evidence; never touches application save data.</summary>
        public static Dictionary<string, string> Run()
        {
            var result = new Dictionary<string, string>();
            string directory = Path.Combine(Path.GetTempPath(), "chooguard-mvp-probe-" + Guid.NewGuid().ToString("N"));
            result["storageDirectory"] = directory;
            try
            {
                CommandIntent intent;
                string commit;
                var ct = CancellationToken.None;
                using (var port = new MvpOperationsPort(directory))
                {
                    intent = port.CreateIntent("ops-1", "move", "platform");
                    var stale = port.CreateIntent("fire-1", "move", "exit");
                    var preview = port.PreviewAsync(intent, ct).GetAwaiter().GetResult();
                    Check(preview.TargetResults[0].Status == TargetStatus.ACCEPTED && port.Revision == 0 && port.Teams[0].LocationId == "concourse" && !File.Exists(Path.Combine(directory, "synthetic-mvp.snapshot")), "preview-no-effects", result);
                    Check(port.Revision < preview.PreviewExpiresAtRevision && preview.PreviewExpiresAtRevision == port.Revision + 1, "preview-confirmation-window", result);
                    var receipt = port.SubmitAsync(intent, ct).GetAwaiter().GetResult();
                    commit = receipt.CommitId;
                    Check(receipt.Status == ReceiptStatus.ACCEPTED && port.Revision == 1 && port.Teams[0].LocationId == "platform", "submit", result);
                    var repeat = port.SubmitAsync(intent, ct).GetAwaiter().GetResult();
                    Check(repeat.CommitId == commit && port.Revision == 1, "repeat-key", result);
                    var changed = new CommandIntent(intent.Key, intent.ActingAgencyId, intent.ActingTeamIds, new StableId("hold"), intent.TargetIds, intent.ReadSet, intent.PayloadRef, intent.AuthoredAt);
                    Check(port.SubmitAsync(changed, ct).GetAwaiter().GetResult().Status == ReceiptStatus.CONFLICT && port.Revision == 1, "same-key-conflict", result);
                    Check(port.SubmitAsync(stale, ct).GetAwaiter().GetResult().Status == ReceiptStatus.REJECTED && port.Revision == 1, "stale-reject", result);
                }
                using (var reopened = new MvpOperationsPort(directory))
                {
                    var saved = reopened.ReadReceiptAsync(intent.Key, ct).GetAwaiter().GetResult();
                    Check(reopened.Revision == 1 && reopened.Teams[0].LocationId == "platform" && saved.Found && saved.Receipt.CommitId == commit && reopened.SubmitAsync(intent, ct).GetAwaiter().GetResult().CommitId == commit, "reopen", result);
                }
                result["overall"] = "PASS: focused local synthetic MVP probe";
            }
            catch (Exception e) { result["overall"] = "FAIL"; result["error"] = e.ToString(); }
            return result;
        }
        static void Check(bool condition, string name, Dictionary<string, string> result)
        { result[name] = condition ? "PASS" : "FAIL"; if (!condition) throw new InvalidOperationException(name); }
    }
}
