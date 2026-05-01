using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MindAtlas.Desktop;

// Provides localized strings for Desktop-specific UI (tray menu, QuickNote window)
public static class DesktopLocalizer
{
    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Strings =
        new Dictionary<string, FrozenDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["tray.open"] = "Open MindAtlas",
                ["tray.quick_note"] = "Quick Note",
                ["tray.settings"] = "Settings",
                ["tray.exit"] = "Exit",
                ["quick_note.title"] = "MindAtlas — Quick Note",
                ["quick_note.placeholder"] = "Type your note here... (Ctrl+Enter to save)",
                ["quick_note.save"] = "Save",
                ["quick_note.saving"] = "Saving...",
                ["quick_note.ingesting"] = "Ingesting...",
                ["quick_note.done"] = "Done!",
                ["quick_note.saved_pending"] = "Saved (ingest pending)"
            }.ToFrozenDictionary(),
            ["ko"] = new Dictionary<string, string>
            {
                ["tray.open"] = "MindAtlas 열기",
                ["tray.quick_note"] = "빠른 메모",
                ["tray.settings"] = "설정",
                ["tray.exit"] = "종료",
                ["quick_note.title"] = "MindAtlas — 빠른 메모",
                ["quick_note.placeholder"] = "메모를 입력하세요... (Ctrl+Enter로 저장)",
                ["quick_note.save"] = "저장",
                ["quick_note.saving"] = "저장 중...",
                ["quick_note.ingesting"] = "수집 중...",
                ["quick_note.done"] = "완료!",
                ["quick_note.saved_pending"] = "저장됨 (수집 대기)"
            }.ToFrozenDictionary(),
            ["ja"] = new Dictionary<string, string>
            {
                ["tray.open"] = "MindAtlas を開く",
                ["tray.quick_note"] = "クイックノート",
                ["tray.settings"] = "設定",
                ["tray.exit"] = "終了",
                ["quick_note.title"] = "MindAtlas — クイックノート",
                ["quick_note.placeholder"] = "ここにノートを入力... (Ctrl+Enterで保存)",
                ["quick_note.save"] = "保存",
                ["quick_note.saving"] = "保存中...",
                ["quick_note.ingesting"] = "取り込み中...",
                ["quick_note.done"] = "完了！",
                ["quick_note.saved_pending"] = "保存済み (取り込み待ち)"
            }.ToFrozenDictionary()
        }.ToFrozenDictionary();

    public static string CurrentLanguage { get; private set; } = "en";

    // Read UI language from appsettings.json. Tolerant of concurrent writes
    // (the file may momentarily be empty/locked while the server saves it).
    public static void LoadLanguage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return;

        byte[] bytes;
        try
        {
            // Open with FileShare.ReadWrite|Delete to coexist with an in-flight writer.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            bytes = ms.ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (bytes.Length == 0) return;

        try
        {
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("MindAtlas", out var ma) &&
                ma.TryGetProperty("UiLanguage", out var lang) &&
                lang.ValueKind == JsonValueKind.String)
            {
                var value = lang.GetString();
                // Empty string in appsettings.json means "auto-detect" — fall
                // back to the OS UI culture, limited to supported locales.
                CurrentLanguage = string.IsNullOrWhiteSpace(value)
                    ? DetectOsLanguage()
                    : value!;
            }
            else
            {
                CurrentLanguage = DetectOsLanguage();
            }
        }
        catch (JsonException)
        {
            // Partial/torn write during reload — ignore; watcher will re-fire.
        }
    }

    private static string DetectOsLanguage()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Strings.ContainsKey(code) ? code : "en";
    }

    public static string Get(string key) =>
        Strings.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var value)
            ? value
            : Strings["en"].GetValueOrDefault(key, key);
}
