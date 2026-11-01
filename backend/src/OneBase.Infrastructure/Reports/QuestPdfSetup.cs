using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace OneBase.Infrastructure.Reports;

/// <summary>QuestPDF один раз на процесс: лицензия Community (решение 31.10.2026) и шрифт Inter из сборки — кириллица и узбекская латиница.</summary>
internal static class QuestPdfSetup
{
    private static readonly object Init = new();
    private static bool _ready;

    public static void EnsureReady()
    {
        lock (Init)
        {
            if (_ready)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = typeof(QuestPdfSetup).Assembly;
            foreach (var name in new[] { "Regular", "Medium", "SemiBold", "Bold" })
            {
                using var stream = assembly.GetManifestResourceStream($"OneBase.Fonts.Inter-{name}.ttf")
                                   ?? throw new InvalidOperationException($"Нет шрифта Inter-{name} в сборке");
                FontManager.RegisterFontFromStream(stream);
            }

            _ready = true;
        }
    }
}
