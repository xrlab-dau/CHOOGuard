using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Where the JEV key of this machine comes from.</summary>
    public enum JevKeySource
    {
        /// <summary>No key on this machine: the title asks for one before a shift.</summary>
        None,
        /// <summary>TYPESAFE_API_KEY in the environment (wins over the file).</summary>
        Environment,
        /// <summary>~/.chooguard/typesafe.key, written by the title screen.</summary>
        File,
        /// <summary>TYPESAFE_API_KEY=off: JEV deliberately switched off (automated runs); nothing is composed.</summary>
        Off,
    }

    /// <summary>Result of asking the JEV server whether a key is accepted.</summary>
    public enum JevKeyCheck { Unchecked, Checking, Accepted, Rejected, Unreachable }

    /// <summary>
    /// The JEV (TypeSafe System One) API key of this machine. Every user brings their own key: it is read from
    /// TYPESAFE_API_KEY or from ~/.chooguard/typesafe.key, which the title screen writes (owner-only permissions on macOS
    /// and Linux; the user profile folder on Windows). The key never enters the repository, a build, a log or the shift
    /// records. A GitHub Actions secret cannot stand in: secrets reach workflow runs only, never a clone or a local Editor.
    /// </summary>
    public static class JevKey
    {
        public const string Variable = "TYPESAFE_API_KEY";
        /// <summary>Lists the models; answers 200 for an accepted key and 401/403 otherwise, without running the model.</summary>
        public const string ModelsEndpoint = "https://api.typesafe.ai/v1/models";
        public const int TimeoutSeconds = 8;

        private static readonly char[] Blank = { ' ', '\t', '\r', '\n' };
        // 이번 실행에서 서버가 사용 중인 키에 대해 답한 것. 키 자체는 남기지 않고 지문만 둔다.
        private static string checkedFingerprint;
        private static JevKeyCheck lastCheck;

        public static string FilePath => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".chooguard", "typesafe.key");

        /// <summary>Shape check only (length, no blanks); the server decides whether it is accepted.</summary>
        public static bool Plausible(string value) => value != null && value.Length >= 20 && value.Length <= 512 && value.IndexOfAny(Blank) < 0;

        /// <summary>The key to use now, or null. <paramref name="source"/> tells where it came from (or why there is none).</summary>
        public static string Load(out JevKeySource source)
        {
            var variable = System.Environment.GetEnvironmentVariable(Variable);
            if (!string.IsNullOrWhiteSpace(variable))
            {
                variable = variable.Trim();
                if (string.Equals(variable, "off", StringComparison.OrdinalIgnoreCase)) { source = JevKeySource.Off; return null; }
                source = Plausible(variable) ? JevKeySource.Environment : JevKeySource.None;
                return source == JevKeySource.Environment ? variable : null;
            }
            source = JevKeySource.None;
            try
            {
                if (!File.Exists(FilePath)) return null;
                var stored = File.ReadAllText(FilePath).Trim();
                if (!Plausible(stored)) return null;
                source = JevKeySource.File;
                return stored;
            }
            catch (Exception) { return null; }
        }

        /// <summary>True when TYPESAFE_API_KEY is set to something that is not a usable key (and not "off").</summary>
        public static bool VariableUnusable
        {
            get
            {
                var variable = System.Environment.GetEnvironmentVariable(Variable);
                return !string.IsNullOrWhiteSpace(variable) && !string.Equals(variable.Trim(), "off", StringComparison.OrdinalIgnoreCase) && !Plausible(variable.Trim());
            }
        }

        public static bool FileExists
        {
            get { try { return File.Exists(FilePath); } catch (Exception) { return false; } }
        }

        /// <summary>
        /// Stores <paramref name="key"/> for this machine's user, remembering what the server said about it
        /// (<paramref name="known"/>). On macOS and Linux the folder is made 0700 and the file is made 0600 before the key
        /// is written into it.
        /// </summary>
        public static void Save(string key, JevKeyCheck known)
        {
            key = key?.Trim();
            if (!Plausible(key)) throw new ArgumentException("JEV 키 형식이 아닙니다.", nameof(key));
            var path = FilePath;
            var folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            OwnerOnly(folder, 0x1C0);
            File.WriteAllText(path, "");
            OwnerOnly(path, 0x180);
            File.WriteAllText(path, key + "\n", new UTF8Encoding(false));
            Observed(key, known);
        }

        /// <summary>Removes the stored key of this machine (the environment variable, if any, is not touched).</summary>
        public static bool Delete()
        {
            try
            {
                if (!File.Exists(FilePath)) return false;
                File.Delete(FilePath);
                Forget();
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Asks the server whether <paramref name="key"/> is accepted (GET /v1/models, no model run). The callback runs on
        /// the main thread. Checking changes nothing: callers record the answer for the key in use with
        /// <see cref="Observed"/> (or <see cref="Save"/>), so a rejected candidate never marks the stored key as bad.
        /// </summary>
        public static IEnumerator Check(string key, Action<JevKeyCheck> done)
        {
            var known = KnownFor(key);
            if (known == JevKeyCheck.Accepted || known == JevKeyCheck.Rejected) { done(known); yield break; }
            using (var request = UnityWebRequest.Get(ModelsEndpoint))
            {
                request.SetRequestHeader("Authorization", "Bearer " + key);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = TimeoutSeconds;
                yield return request.SendWebRequest();
                done(request.result == UnityWebRequest.Result.Success ? JevKeyCheck.Accepted :
                    request.responseCode == 401 || request.responseCode == 403 ? JevKeyCheck.Rejected : JevKeyCheck.Unreachable);
            }
        }

        /// <summary>What the server last said about <paramref name="key"/> during this run (Unchecked for any other key).</summary>
        public static JevKeyCheck KnownFor(string key) => key != null && Fingerprint(key) == checkedFingerprint ? lastCheck : JevKeyCheck.Unchecked;

        /// <summary>Records what the server said about the key in use (the title's check, or a 401 mid-shift when the key was revoked).</summary>
        public static void Observed(string key, JevKeyCheck result)
        {
            checkedFingerprint = Fingerprint(key);
            lastCheck = result;
        }

        private static void Forget()
        {
            checkedFingerprint = null;
            lastCheck = JevKeyCheck.Unchecked;
        }

        /// <summary>A one-way fingerprint so the key itself is never kept beyond the request that needs it.</summary>
        private static string Fingerprint(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
        }

        private static void OwnerOnly(string path, int mode)
        {
            if (UnityEngine.Application.platform == RuntimePlatform.WindowsPlayer || UnityEngine.Application.platform == RuntimePlatform.WindowsEditor) return;
            try { Chmod(path, mode); }
            catch (Exception) { }
        }

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string path, int mode);
    }
}
