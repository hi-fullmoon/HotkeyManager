using System.Text.Json.Nodes;
using HotkeyManager.Settings;
using HotkeyManager.Core;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HotkeyManager-SettingsChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.jsonc");
        try
        {
            var original = "{ // comment\n \"future\": 42, \"hotkeys\": [{\"key\":\"\",\"processName\":\"notepad\",\"exePath\":\"notepad.exe\",\"windowClass\":\"KeepMe\",\"unknown\":true,}],}";
            File.WriteAllText(path, original);
            var recording = false;
            var store = new SettingsConfigStore(path, value => recording = value);
            var entries = store.Load();
            Check(entries.Count == 1, "JSONC comments and trailing comma load");
            entries[0]!["name"] = "记事本";
            store.Save(entries);
            var saved = JsonNode.Parse(File.ReadAllText(path))!;
            Check(saved["future"]!.GetValue<int>() == 42 && saved["hotkeys"]![0]!["windowClass"]!.GetValue<string>() == "KeepMe" && saved["hotkeys"]![0]!["unknown"]!.GetValue<bool>(), "unknown fields and window class preserved");
            Check(File.ReadAllText(path + ".bak") == original, "original JSONC backed up byte-for-byte");
            entries = store.Load(); File.AppendAllText(path, " ");
            Reject(() => store.Save(entries), "external edit conflict");
            Check(!recording, "hotkeys restored after failed save");
            store.Load(); store.Save(new JsonArray());
            Check(store.Load().Count == 0, "delete last item and reload empty list");
            var bad = JsonNode.Parse("[{\"key\":\"Ctrl+Alt+Bogus\",\"processName\":\"app\",\"exePath\":\"app.exe\"}]")!.AsArray();
            Reject(() => store.Save(bad), "invalid key rejected");
            bad[0]!["key"] = "A"; Reject(() => store.Save(bad), "unmodified letter rejected");
            Check(store.Load().Count == 0 && !recording, "validation failure preserves file and restores hotkeys");
            using var occupied = new HotkeyService();
            var shortcut = HotkeyParser.Parse("Ctrl+Alt+Shift+F23");
            if (!occupied.Register(shortcut.Modifiers, shortcut.VirtualKey, () => { }, out var error)) throw new Exception(error);
            bad[0]!["key"] = "Ctrl+Alt+Shift+F23";
            Reject(() => store.Save(bad), "system occupied key rejected");
            occupied.UnregisterAll();
            bad.Add(bad[0]!.DeepClone()); bad[1]!["key"] = "shift+control+alt+f23";
            Reject(() => store.Save(bad), "semantic duplicate keys rejected");
            bad.RemoveAt(1); store.Save(bad);
            Check(store.Load()[0]!["key"]!.GetValue<string>() == "Ctrl+Alt+Shift+F23", "valid system key saves successfully");
            File.WriteAllText(path, "{broken");
            Reject(() => store.Load(), "malformed JSON does not become empty configuration");
            Console.WriteLine("All settings checks passed.");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (Exception ex) when (ex is InvalidDataException or FormatException or System.Text.Json.JsonException) { Console.WriteLine("PASS " + name); return; }
        throw new Exception("Expected failure: " + name);
    }
}
