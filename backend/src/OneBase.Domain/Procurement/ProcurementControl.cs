using OneBase.Domain.Common;

namespace OneBase.Domain.Procurement;

/// <summary>
/// Ежедневная сводка снабжения директору и руководителю снабжения — одна на день (Day уникален). Хранится журналом: заголовок, короткий
/// итог и разделы (JSON: название, сколько, строки со ссылками) — снимок на момент формирования, чтобы прошлые сводки читались как были.
/// </summary>
public class ProcurementDigest : Entity
{
    public DateOnly Day { get; set; }

    public required string Title { get; set; }

    /// <summary>Итог одной строкой: «Ждут согласования: 2 · новых заявок: 1 · ждут закупки: 3 · …».</summary>
    public required string Summary { get; set; }

    /// <summary>Разделы (JSON): [{ "title", "count", "items": [{ "text", "link" }] }].</summary>
    public required string Sections { get; set; }

    /// <summary>Скольким руководителям ушло уведомление.</summary>
    public int Recipients { get; set; }
}

/// <summary>
/// Эскалация просроченной задачи снабжения: уровень 1 — руководителю снабжения, 2 — директору. Одна запись на задачу, уровень и срок
/// (уникально): повторные проверки не дублируют, перенесли срок — эскалация начнётся заново.
/// </summary>
public class ProcurementEscalation : Entity
{
    public Guid TaskId { get; set; }

    public int Level { get; set; }

    public DateOnly DueDate { get; set; }
}
