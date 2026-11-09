using System.IO.Compression;
using System.Text;

namespace OneBase.Application.Recruitment.Documents;

/// <summary>Что показало содержимое файла: тип (pdf, docx, doc, rtf, odt, txt, jpeg, png, webp) или null, MIME, и почему отклонён.</summary>
public sealed record FileCheck(string? Type, string ContentType, string? Problem, bool HasMacros = false)
{
    public bool Ok => Type is not null && Problem is null;
}

/// <summary>
/// Тип файла по содержимому, а не по расширению: сигнатуры PDF, PNG, JPEG, WEBP, RTF, OLE (DOC), ZIP (DOCX, ODT — по внутренним частям).
/// Исполняемые файлы, HTML и скрипты под видом документа отклоняются. Макросы (vbaProject в DOCX, макросы в DOC) не выполняются никогда —
/// OneBase только читает текст; их наличие помечается.
/// </summary>
public static class FileSignatures
{
    public static readonly IReadOnlyDictionary<string, string> Mime = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pdf"] = "application/pdf",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["doc"] = "application/msword",
        ["rtf"] = "application/rtf",
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["txt"] = "text/plain",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["webp"] = "image/webp",
    };

    /// <summary>Какие типы ожидаются по расширению (содержимое .doc бывает и DOCX, и RTF — так сохраняют Word и почта).</summary>
    private static readonly Dictionary<string, string[]> Expected = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["pdf"],
        [".docx"] = ["docx"],
        [".doc"] = ["doc", "docx", "rtf"],
        [".rtf"] = ["rtf"],
        [".odt"] = ["odt"],
        [".txt"] = ["txt"],
        [".jpg"] = ["jpeg", "png", "webp"],
        [".jpeg"] = ["jpeg", "png", "webp"],
        [".png"] = ["png", "jpeg", "webp"],
        [".webp"] = ["webp", "jpeg", "png"],
    };

    public static FileCheck Check(byte[] bytes, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var type = Detect(bytes, out var macros, out var dangerous);
        if (type == BrokenZip)
        {
            // Архив не открывается, а по расширению — документ Word или ODT: это повреждённый документ (его прочитает разбор и скажет «повреждён»).
            type = extension.ToLowerInvariant() switch { ".docx" or ".doc" => "docx", ".odt" => "odt", _ => null };
        }

        if (dangerous is not null)
        {
            return new FileCheck(null, "application/octet-stream", $"Это не документ: {dangerous}. Файл сохранён, но не открывается и не обрабатывается.");
        }

        if (type is null)
        {
            return new FileCheck(null, "application/octet-stream",
                $"Содержимое не похоже на {extension.TrimStart('.').ToUpperInvariant()} — файл повреждён или это другой формат. Файл сохранён, обработка не выполнялась.");
        }

        if (Expected.TryGetValue(extension, out var allowed) && !allowed.Contains(type))
        {
            return new FileCheck(null, "application/octet-stream",
                $"Расширение {extension.ToLowerInvariant()}, а внутри — {type.ToUpperInvariant()}. Файл сохранён, обработка не выполнялась.");
        }

        return new FileCheck(type, Mime[type], null, macros);
    }

    private static string? Detect(byte[] b, out bool macros, out string? dangerous)
    {
        macros = false;
        dangerous = null;
        if (b.Length < 4)
        {
            return null;
        }

        if (Starts(b, "%PDF-"u8) || IndexOf(b, "%PDF-"u8, 1024) >= 0)
        {
            return "pdf";
        }

        if (Starts(b, [0x89, 0x50, 0x4E, 0x47]))
        {
            return "png";
        }

        if (b is [0xFF, 0xD8, 0xFF, ..])
        {
            return "jpeg";
        }

        if (b.Length >= 12 && Starts(b, "RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        if (Starts(b, "{\\rtf"u8))
        {
            return "rtf";
        }

        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
        {
            // OLE: Word 97–2003. Макросы — поток «Macros»/«_VBA_PROJECT» в каталоге (имена — UTF-16).
            macros = IndexOf(b, Encoding.Unicode.GetBytes("_VBA_PROJECT"), b.Length) >= 0 || IndexOf(b, Encoding.Unicode.GetBytes("Macros"), b.Length) >= 0;
            return "doc";
        }

        if (Starts(b, "PK\x03\x04"u8))
        {
            return Zip(b, ref macros);
        }

        if (Starts(b, "MZ"u8) || Starts(b, [0x7F, 0x45, 0x4C, 0x46]) || b is [0xCF, 0xFA, 0xED, 0xFE, ..] || b is [0xCA, 0xFE, 0xBA, 0xBE, ..])
        {
            dangerous = "исполняемый файл программы";
            return null;
        }

        var head = Encoding.UTF8.GetString(b, 0, Math.Min(b.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t').ToLowerInvariant();
        if (head.StartsWith("<!doctype html", StringComparison.Ordinal) || head.StartsWith("<html", StringComparison.Ordinal) || head.StartsWith("<script", StringComparison.Ordinal)
            || head.StartsWith("<svg", StringComparison.Ordinal) || head.StartsWith("#!", StringComparison.Ordinal))
        {
            dangerous = "веб-страница или скрипт";
            return null;
        }

        return Text(b) ? "txt" : null;
    }

    private static string? Zip(byte[] b, ref bool macros)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(b, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > 5000)
            {
                return null; // не документ
            }

            if (zip.GetEntry("word/document.xml") is not null)
            {
                macros = zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
                return "docx";
            }

            if (zip.GetEntry("mimetype") is { } mime && zip.GetEntry("content.xml") is not null)
            {
                using var reader = new StreamReader(mime.Open());
                var value = reader.ReadToEnd().Trim();
                if (value == "application/vnd.oasis.opendocument.text")
                {
                    macros = zip.Entries.Any(e => e.FullName.StartsWith("Basic/", StringComparison.Ordinal) || e.FullName.StartsWith("Scripts/", StringComparison.Ordinal));
                    return "odt";
                }
            }
        }
        catch (InvalidDataException)
        {
            return BrokenZip;
        }

        return null;
    }

    /// <summary>ZIP с повреждённым каталогом (обрезанный DOCX) — тип решается по расширению.</summary>
    private const string BrokenZip = "zip?";

    /// <summary>Текст: без нулевых байтов и почти без управляющих символов (UTF-8 или Windows-1251).</summary>
    private static bool Text(byte[] b)
    {
        var sample = b.AsSpan(0, Math.Min(b.Length, 8192));
        if (sample.IndexOf((byte)0) >= 0)
        {
            return false;
        }

        var control = 0;
        foreach (var x in sample)
        {
            if (x < 0x20 && x is not (0x09 or 0x0A or 0x0D or 0x0C))
            {
                control++;
            }
        }

        return control <= sample.Length / 100;
    }

    private static bool Starts(byte[] b, ReadOnlySpan<byte> prefix) => b.Length >= prefix.Length && b.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static int IndexOf(byte[] b, ReadOnlySpan<byte> needle, int limit) => b.AsSpan(0, Math.Min(b.Length, limit)).IndexOf(needle);
}
