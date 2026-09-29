namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Обход списка Linko по limit/offset — отдельно от HTTP, чтобы правило «ничего не потерять и не задвоить» проверялось тестами.
/// </summary>
public static class LinkoPaging
{
    /// <summary>
    /// Читает все страницы. Конец набора — страница короче лимита. Если уже первая страница короче лимита,
    /// это может быть не конец, а предел сервера (у возвратов он бывает меньше 1000): тогда запрашивается следующая —
    /// пустая значит конец, непустая — дальше идём шагом, который реально отдаёт сервер (иначе строки между пропали бы).
    /// Страницы запрашиваются пачками по parallel, а передаются в onPage строго по порядку.
    /// </summary>
    public static async Task<int> ReadAllAsync<T>(
        Func<int, int, Task<IReadOnlyList<T>>> page,
        int limit,
        int parallel,
        Func<IReadOnlyList<T>, Task> onPage,
        Action<int>? onServerLimit = null)
    {
        limit = Math.Max(1, limit);
        parallel = Math.Max(1, parallel);

        async Task<int> Emit(IReadOnlyList<T> items)
        {
            if (items.Count > 0)
            {
                await onPage(items);
            }

            return items.Count;
        }

        var first = await Emit(await page(0, limit));
        if (first == 0)
        {
            return 0;
        }

        var total = first;
        var offset = first;
        if (first < limit)
        {
            var second = await Emit(await page(offset, limit));
            if (second == 0)
            {
                return total;
            }

            limit = first;
            onServerLimit?.Invoke(limit);
            total += second;
            offset += second;
            if (second < limit)
            {
                return total;
            }
        }

        while (true)
        {
            var batch = await Task.WhenAll(Enumerable.Range(0, parallel).Select(i => page(offset + i * limit, limit)));
            foreach (var items in batch)
            {
                var count = await Emit(items);
                total += count;
                offset += count;
                if (count < limit)
                {
                    return total;
                }
            }
        }
    }
}
