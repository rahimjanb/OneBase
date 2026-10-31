using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Work;

/// <summary>Вид события календаря: встреча, срок отчёта или важное событие (аттестация, собрание, праздник).</summary>
public enum CalendarEventKind
{
    Meeting,
    ReportDue,
    Event,
}

/// <summary>
/// Событие календаря OneBase. Кто видит: автор, участники, сотрудники отдела (DepartmentId) или все (ForEveryone). Сроки задач в календаре
/// показываются из самих задач — здесь их нет. Время хранится в UTC; весь день (AllDay) — StartsAt в полночь Ташкента.
/// </summary>
public class CalendarEvent : Entity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public CalendarEventKind Kind { get; set; } = CalendarEventKind.Meeting;

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Окончание; null — без длительности (срок отчёта, событие на время).</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public bool AllDay { get; set; }
    public string? Location { get; set; }

    /// <summary>Событие отдела — его видят все сотрудники отдела.</summary>
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Событие всей компании.</summary>
    public bool ForEveryone { get; set; }

    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<CalendarEventParticipant> Participants { get; set; } = [];
}

/// <summary>Участник события.</summary>
public class CalendarEventParticipant
{
    public Guid EventId { get; set; }
    public CalendarEvent? Event { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
}
