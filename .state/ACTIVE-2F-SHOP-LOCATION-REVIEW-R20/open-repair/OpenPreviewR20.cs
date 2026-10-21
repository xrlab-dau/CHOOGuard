using System;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

public static class OpenPreviewR20
{
    const string Root = "/Users/um-yunsang/CHOOGuard";
    const string Thread = "01a13cb4-b738-7823-bf16-30a39ea3d8c6";
    const string Scope = "C180_ROOT_OPEN_PREVIEW_R20";
    static void Guard()
    {
        var a = JObject.Parse(File.ReadAllText(Root + "/.state/WRITER-AUTHORITY.json"));
        if ((string)a["status"] != "ACTIVE" || (bool?)a["Native_inflight"] != true ||
            (string)a["sole_writer_thread"] != Thread || (string)a["scope"] != Scope)
            throw new Exception("Exact GUI-only C180 lease required.");
        var g = JObject.Parse(File.ReadAllText(Root + "/" + (string)a["delegated_author_lease"]));
        if ((string)g["grant_nonce"] != (string)a["scene_worker_grant_nonce"])
            throw new Exception("Exact C180 nonce required.");
    }
    public static string Open()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/2F-SHOP-LOCATION-REVIEW-R20-NO-TEXT.pdf"
        }, "PREVIEW-OPEN-GUI-RELAY-RESULT.json");
    }
    public static string OpenSources()
    {
        return OpenDocuments(new[] {
            Root + "/workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083313-store-plan-page-1-embedded-2.jpeg",
            Root + "/.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/OFFICIAL-2F-SPECIALIST-PLAN-20261007-ORIGINAL-PAGE.pdf"
        }, "PREVIEW-OPEN-SOURCES-GUI-RELAY-RESULT.json");
    }
    public static string OpenR21()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-SHOP-STRUCTURE-REVIEW-R21/2F-SHOP-STRUCTURAL-WORK-OVERLAY-R21-NO-TEXT.pdf"
        }, "PREVIEW-OPEN-STRUCTURE-R21-GUI-RELAY-RESULT.json");
    }
    public static string OpenSpecialistR21()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/OFFICIAL-2F-SPECIALIST-PLAN-20261007-ORIGINAL-PAGE.pdf"
        }, "PREVIEW-OPEN-SPECIALIST-R21-GUI-RELAY-RESULT.json");
    }
    public static string OpenR22()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/2F-SHOP-CAD-LINE-EXTENSION-REVIEW-R22-NO-TEXT.pdf"
        }, "PREVIEW-OPEN-CAD-LINE-R22-GUI-RELAY-RESULT.json");
    }
    public static string OpenUserOnlyR23()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/user-only-r23/USER-MARKED-REGIONS-ONLY-2F-CAD-R23-NO-TEXT.pdf"
        }, "PREVIEW-OPEN-USER-ONLY-R23-GUI-RELAY-RESULT.json");
    }
    public static string OpenBlueOnlyR24()
    {
        return OpenDocuments(new[] {
            Root + "/.state/ACTIVE-2F-BLUE-ONLY-VECTOR-CONSTRUCTION-R24/BLUE-ONLY-CAD-LINE-CONSTRUCTION-R24-NO-TEXT.pdf"
        }, "PREVIEW-OPEN-BLUE-ONLY-R24-GUI-RELAY-RESULT.json");
    }
    static string OpenDocuments(string[] documents, string receipt)
    {
        Guard();
        string arguments = "-a \"/System/Applications/Preview.app\"";
        foreach (string file in documents) {
            if (!File.Exists(file)) throw new FileNotFoundException("Requested document missing.", file);
            arguments += " \"" + file + "\"";
        }
        var start = new ProcessStartInfo {
            FileName = "/usr/bin/open",
            Arguments = arguments,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        var p = Process.Start(start);
        if (p == null) throw new Exception("Document-open process was not created.");
        bool exited = p.WaitForExit(8000);
        var result = new JObject {
            ["status"] = exited && p.ExitCode == 0 ? "PREVIEW_OPEN_REQUEST_DELIVERED" : "PREVIEW_OPEN_REQUEST_FAILED",
            ["utc"] = DateTime.UtcNow.ToString("o"),
            ["documents"] = new JArray(documents),
            ["relay"] = "existing local GUI process; exact /usr/bin/open -a Preview",
            ["open_process_pid"] = p.Id,
            ["exited"] = exited,
            ["exit_code"] = exited ? new JValue(p.ExitCode) : JValue.CreateNull(),
            ["stderr"] = exited ? p.StandardError.ReadToEnd() : "Open request still in flight; process preserved.",
            ["Scene_Assets_changed"] = false,
            ["Scene_read_or_save_or_capture"] = false
        };
        Guard();
        File.WriteAllText(Root + "/.state/ACTIVE-2F-SHOP-LOCATION-REVIEW-R20/open-repair/" + receipt, result.ToString());
        return result.ToString();
    }
}
