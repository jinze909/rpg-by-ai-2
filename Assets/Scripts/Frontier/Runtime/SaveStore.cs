using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Starfall
{
    [Serializable] public sealed class SaveEnvelope { public int format = 1; public string payload, checksum; }
    public static class SaveStore
    {
        public static string PathName { get { return PathFor(-1); } }
        public static string PathFor(int slot)
        { if (slot < -1 || slot > 2) throw new ArgumentOutOfRangeException("slot"); return Path.Combine(Application.persistentDataPath, slot < 0 ? "starfall-v1.json" : "starfall-slot-" + (slot + 1) + ".json"); }
        private static string Checksum(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
        // Detect only root-level property names, not words in a journal entry,
        // quest flag, or an unrelated nested object.
        private static bool RootField(string json, string field)
        {
            int nesting = 0, start = -1;
            bool quoted = false, escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c != '"') continue;
                    if (nesting == 1 && i - start == field.Length &&
                        string.CompareOrdinal(json, start, field, 0, field.Length) == 0)
                    {
                        int next = i + 1;
                        while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                        if (next < json.Length && json[next] == ':') return true;
                    }
                    quoted = false;
                }
                else if (c == '"') { quoted = true; start = i + 1; }
                else if (c == '{' || c == '[') nesting++;
                else if (c == '}' || c == ']') nesting--;
            }
            return false;
        }
        private static SaveData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) throw new InvalidDataException("Invalid save JSON.");
            // A broken/truncated envelope must NEVER be interpreted as an old
            // unwrapped SaveData (JsonUtility can populate default fields).
            if (RootField(json, "payload") || RootField(json, "checksum") || RootField(json, "format"))
            {
                SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null || !RootField(json, "payload") ||
                    !RootField(json, "checksum") || envelope.payload == null ||
                    envelope.format != 1 || envelope.checksum != Checksum(envelope.payload))
                    throw new InvalidDataException("Save checksum mismatch.");
                json = envelope.payload;
            }
            // All historical v1/v2 Unity saves serialize these base fields.
            // A *valid JSON fragment* containing only version + inventory
            // previously passed and silently replaced quests, location and
            // money with defaults. Reject it before loading or saving over a
            // recoverable .bak/.tmp/.stage-* candidate. Do not require newer expansion fields.
            string[] required = { "version", "inventory", "flags",
                "room", "x", "y", "health", "coins" };
            foreach (string field in required)
                if (!RootField(json, field))
                    throw new InvalidDataException("Missing save schema field: " + field);
            SaveData value = JsonUtility.FromJson<SaveData>(json);
            if (value == null || (value.version != 1 && value.version != 2) ||
                value.inventory == null || value.inventory.Length != 8 ||
                value.flags == null)
                throw new InvalidDataException("Invalid save schema.");
            return value;
        }
        public static SaveData Read(out string warning) { return Read(-1, out warning); }
        private static DateTime FileTimestamp(string path)
        {
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            // A timestamp centuries ahead is corrupted metadata, not a reason
            // to let one stale sidecar permanently outrank future saves.
            return timestamp > DateTime.UtcNow.AddYears(100) ?
                DateTime.MinValue : timestamp;
        }
        private static DateTime SaveTimestamp(SaveData candidate, string path)
        {
            DateTime savedTime;
            // The serialized timestamp follows the save payload through an
            // atomic rename and is more reliable than the filesystem timestamp
            // copied onto a .bak by platform-specific replace implementations.
            // Reject implausible future metadata, but keep the filesystem-time
            // fallback for old v1 saves and timestamp-corrupted sidecars.
            if (!DateTime.TryParse(candidate.savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out savedTime) ||
                savedTime > DateTime.UtcNow.AddYears(100))
                return FileTimestamp(path);
            return savedTime.ToUniversalTime();
        }
        // Each in-progress write gets its own immutable candidate path.
        // A crashed or failed writer must never truncate the only valid
        // recovery file left by an earlier interrupted transaction.
        private static string[] StagingPaths(string main)
        {
            string directory = Path.GetDirectoryName(main);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return new string[0];
            try
            {
                return Directory.GetFiles(directory,
                    Path.GetFileName(main) + ".stage-*");
            }
            catch { return new string[0]; }
        }
        private static string[] RecoveryPaths(string main)
        {
            var paths = new List<string> {
                main + ".tmp", main + ".bak", main + ".precommit"
            };
            paths.AddRange(StagingPaths(main));
            return paths.ToArray();
        }
        private static void CleanupCommittedRecoveryPaths(string main)
        {
            // Cleanup runs after commit, but another game process could still
            // be writing a different unique stage. Delete only candidates that
            // decode successfully and are no newer than the verified primary.
            SaveData committed;
            DateTime committedAt;
            try
            {
                committed = Decode(File.ReadAllText(main));
                committedAt = SaveTimestamp(committed, main);
            }
            catch { return; }
            var paths = new List<string> { main + ".tmp" };
            paths.AddRange(StagingPaths(main));
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    SaveData candidate = Decode(File.ReadAllText(path));
                    if (SaveTimestamp(candidate, path) > committedAt) continue;
                    File.Delete(path);
                }
                catch
                {
                    // An invalid stage may be an in-flight writer. Preserve it;
                    // Read() ignores invalid candidates without hiding the main.
                }
            }
        }
        private static DateTime NextSaveTimestamp(int slot)
        {
            string main = PathFor(slot);
            DateTime newest = DateTime.MinValue;
            // Wall clocks can move backwards after travel, sleep or manual
            // correction. Advance beyond every valid on-disk transaction
            // candidate so the next successful write remains the newest copy.
            var candidates = new List<string> { main };
            candidates.AddRange(RecoveryPaths(main));
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    SaveData candidate = Decode(File.ReadAllText(path));
                    DateTime timestamp = SaveTimestamp(candidate, path);
                    if (timestamp > newest) newest = timestamp;
                }
                catch
                {
                    // An invalid recovery candidate must not prevent a save.
                }
            }
            DateTime now = DateTime.UtcNow;
            if (newest >= now && newest < DateTime.MaxValue)
                return newest.AddTicks(1);
            return now;
        }
        public static SaveData Read(int slot, out string warning)
        {
            warning = null;
            string main = PathFor(slot);
            // Treat every checksum/schema-verified copy as a candidate.
            // A process may die after flushing a unique .stage-* file but
            // before promoting it; preferring any valid primary unconditionally
            // would silently roll back that newer, recoverable progress.
            SaveData newest = null;
            DateTime newestTime = DateTime.MinValue;
            string recoveredFrom = null;
            if (File.Exists(main))
            {
                try
                {
                    SaveData primary = Decode(File.ReadAllText(main));
                    DateTime primaryTime = SaveTimestamp(primary, main);
                    newest = primary;
                    newestTime = primaryTime;
                    recoveredFrom = main;
                }
                catch (Exception ex) { warning = "Cannot read save: " + ex.Message; }
            }
            // Include all recoverable transaction stages and choose by the
            // payload's own save time, not filename priority. Ties keep the
            // primary (or the first valid recovery copy when no primary exists).
            foreach (string path in RecoveryPaths(main))
            {
                if (!File.Exists(path)) continue;
                try
                {
                    SaveData candidate = Decode(File.ReadAllText(path));
                    DateTime savedTime = SaveTimestamp(candidate, path);
                    if (newest == null || savedTime > newestTime)
                    {
                        newest = candidate;
                        newestTime = savedTime;
                        recoveredFrom = path;
                    }
                }
                catch (Exception ex)
                {
                    // A corrupt stale sidecar must not make a valid primary
                    // look like a recovery event to the player.
                    if (newest == null && warning == null)
                        warning = "Cannot read save: " + ex.Message;
                }
            }
            if (newest != null && !string.Equals(recoveredFrom, main, StringComparison.Ordinal))
            {
                bool interrupted = recoveredFrom.EndsWith(".tmp", StringComparison.Ordinal) ||
                    recoveredFrom.IndexOf(".stage-", StringComparison.Ordinal) >= 0;
                warning = "Recovered " + (interrupted ? "interrupted save" : "backup") + ".";
            }
            return newest;
        }
        public static bool Exists(int slot)
        {
            string path = PathFor(slot);
            return File.Exists(path) || File.Exists(path + ".bak") ||
                File.Exists(path + ".tmp") || File.Exists(path + ".precommit") ||
                StagingPaths(path).Length > 0;
        }
        // Fallback for platforms/filesystems without File.Replace. Never
        // DELETE the only primary save before installing its replacement:
        // keep the former primary at .precommit until the verified temp is
        // promoted. Read() can recover that file even after power loss.
        private static void PortableReplace(string temp, string path, string backup)
        {
            string prior = path + ".precommit";
            // When an earlier attempt died after displacement, restore its
            // original before starting a new transaction. Do not overwrite
            // the last recoverable primary with a stale temp or empty slot.
            if (File.Exists(prior))
            {
                if (!File.Exists(path)) File.Move(prior, path);
                else File.Delete(prior);
            }
            if (backup != null) File.Copy(path, backup, true);
            File.Move(path, prior);
            try { File.Move(temp, path); }
            catch
            {
                if (!File.Exists(path) && File.Exists(prior))
                    File.Move(prior, path);
                throw;
            }
            try { File.Delete(prior); }
            catch (IOException error)
            {
                // Current save is verified and committed. The stale prior
                // can be cleaned later; failure to remove it is not failure
                // to save the new data.
                Debug.LogWarning("Starfall old save cleanup delayed: " + error.Message);
            }
        }
        public static bool Write(SaveData value) { return Write(value, -1); }
        public static bool Write(SaveData value, int slot)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string path = PathFor(slot);
                // Never reuse .tmp or a previous stage for a new write: it may
                // be the only checksum-valid copy left after an earlier crash.
                string temp = path + ".stage-" + Guid.NewGuid().ToString("N");
                value.savedAtUtc = NextSaveTimestamp(slot).ToString("o", CultureInfo.InvariantCulture);
                string payload = JsonUtility.ToJson(value);
                string json = JsonUtility.ToJson(new SaveEnvelope { payload = payload, checksum = Checksum(payload) }, true);
                // Validate before touching disk. The unique stage path then
                // preserves all older recovery candidates until the new bytes
                // are flushed and decoded from disk.
                Decode(json);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { byte[] bytes = Encoding.UTF8.GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                Decode(File.ReadAllText(temp));
                if (!File.Exists(path)) File.Move(temp, path);
                else
                {
                    bool validOld = true; try { Decode(File.ReadAllText(path)); } catch { validOld = false; }
                    string backup = validOld ? path + ".bak" : null;
                    try { File.Replace(temp, path, backup); }
                    catch (PlatformNotSupportedException)
                    { PortableReplace(temp, path, backup); }
                }
                CleanupCommittedRecoveryPaths(path);
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("Starfall save failed: " + ex.Message); return false; }
        }
    }
}
