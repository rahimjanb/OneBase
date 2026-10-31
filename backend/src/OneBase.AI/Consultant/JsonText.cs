using System.Text.Json;

namespace OneBase.AI.Consultant;

/// <summary>Разбор JSON из ответа модели: объект может быть обёрнут в текст или ```json.</summary>
internal static class JsonText
{
    public static JsonElement? ExtractObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// JSON, обрезанный лимитом длины ответа модели («…,"findings":["Выручка 13,4 млрд сум","Визиты в сентяб»): обрезается до последнего
    /// целого значения, открытые массивы и объекты закрываются. Целые поля (summary, уже дописанные находки) сохраняются, недописанное —
    /// отбрасывается. null — объекта в тексте нет или восстановить нечего.
    /// </summary>
    public static string? CloseTruncated(string? text)
    {
        var start = text?.IndexOf('{') ?? -1;
        if (text is null || start < 0)
        {
            return null;
        }

        // Стек контейнеров: '{' или '['; у объекта — ждём ли ключ (после «{» и «,») или значение (после «:»).
        var stack = new List<(char Type, bool ExpectKey)>();
        var cut = -1;
        char[]? closers = null;

        void Safe(int at)
        {
            cut = at;
            closers = stack.Select(f => f.Type == '{' ? '}' : ']').Reverse().ToArray();
        }

        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '{' or '[':
                    stack.Add((c, c == '{'));
                    i++;
                    Safe(i);
                    continue;
                case '}' or ']':
                    if (stack.Count == 0)
                    {
                        return null;
                    }

                    stack.RemoveAt(stack.Count - 1);
                    i++;
                    if (stack.Count == 0)
                    {
                        // Объект целый — обрезки нет.
                        return text[start..i];
                    }

                    Safe(i);
                    continue;
                case ':':
                    if (stack.Count > 0 && stack[^1].Type == '{')
                    {
                        stack[^1] = ('{', false);
                    }

                    i++;
                    continue;
                case ',':
                    if (stack.Count > 0 && stack[^1].Type == '{')
                    {
                        stack[^1] = ('{', true);
                    }

                    i++;
                    continue;
                case '"':
                {
                    var key = stack.Count > 0 && stack[^1].Type == '{' && stack[^1].ExpectKey;
                    var j = i + 1;
                    var closed = false;
                    while (j < text.Length)
                    {
                        if (text[j] == '\\')
                        {
                            j += 2;
                            continue;
                        }

                        if (text[j] == '"')
                        {
                            closed = true;
                            break;
                        }

                        j++;
                    }

                    if (!closed)
                    {
                        i = text.Length;
                        continue;
                    }

                    i = j + 1;
                    if (!key)
                    {
                        Safe(i);
                    }

                    continue;
                }
                default:
                {
                    // Число, true, false, null — до разделителя; оборванное в конце текста — не целое.
                    var j = i;
                    while (j < text.Length && !char.IsWhiteSpace(text[j]) && text[j] is not (',' or '}' or ']' or ':'))
                    {
                        j++;
                    }

                    if (j < text.Length)
                    {
                        Safe(j);
                    }

                    i = j;
                    continue;
                }
            }
        }

        if (cut < 0 || closers is null)
        {
            return null;
        }

        var body = text[start..cut].TrimEnd();
        if (body.EndsWith(','))
        {
            body = body[..^1];
        }

        var repaired = body + new string(closers);
        try
        {
            using var doc = JsonDocument.Parse(repaired);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? repaired : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];
}
