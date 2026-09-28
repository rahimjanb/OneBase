namespace OneBase.Api;

/// <summary>
/// Для локального запуска: подхватывает ближайший .env (вверх по каталогам) в переменные окружения.
/// Уже заданные переменные не перезаписываются. В Docker переменные приходят из docker-compose.
/// </summary>
internal static class DotEnv
{
    public static void Load()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                var eq = line.IndexOf('=');
                if (line.Length == 0 || line.StartsWith('#') || eq <= 0)
                {
                    continue;
                }

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }
    }
}
