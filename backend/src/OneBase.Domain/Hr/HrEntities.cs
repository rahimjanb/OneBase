namespace OneBase.Domain.Hr;

/// <summary>Статус сотрудника в Verifix (employee$list.status): работает, уволен, ещё не принят.</summary>
public static class HrEmployeeStatus
{
    public const string Working = "W";
    public const string Dismissed = "D";
    public const string NotHired = "U";
}

/// <summary>
/// Сотрудник из Verifix — источник истины по полям Verifix (имя, телефон, статус, даты приёма и увольнения, подразделение, должность,
/// график). Сопоставляется только по ID Verifix (employee_id, уникален). Локальные поля OneBase (Note) синхронизация не трогает.
/// Пропал из полной выгрузки Verifix — запись не удаляется: ставится MissingSince («нет в Verifix с …»), вернулся — отметка снимается.
/// Увольнение (статус D) — это деактивация, не удаление: история посещений остаётся.
/// Персональные данные сверх нужного для учёта времени (ПИНФЛ, ИНН, паспорт, фото, биометрия, номер карты доступа) не загружаются.
/// </summary>
public class HrEmployee
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>employee_id в Verifix.</summary>
    public long VerifixId { get; set; }

    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }

    /// <summary>«Фамилия Имя Отчество» — для поиска и сортировки; собирается при загрузке.</summary>
    public required string FullName { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>HrEmployeeStatus: W — работает, D — уволен, U — не принят.</summary>
    public required string Status { get; set; }

    /// <summary>A — активен, P — пассивен (state в Verifix).</summary>
    public string? State { get; set; }

    public DateOnly? HiringDate { get; set; }
    public DateOnly? DismissalDate { get; set; }
    public long? DivisionId { get; set; }
    public long? OrgUnitId { get; set; }
    public long? JobId { get; set; }
    public long? ScheduleId { get; set; }

    /// <summary>Последний известный ID рабочего цикла (staff_id из отметок и табеля): при повторном приёме он новый.</summary>
    public long? StaffId { get; set; }

    /// <summary>Отпечаток полей Verifix — чтобы отличить изменённую строку от неизменной (счётчики журнала).</summary>
    public string? SourceHash { get; set; }

    // ---------- Локальные поля OneBase: синхронизация их не меняет ----------

    /// <summary>Заметка HR в OneBase.</summary>
    public string? Note { get; set; }

    public DateTimeOffset? NoteUpdatedAt { get; set; }
    public Guid? NoteUpdatedById { get; set; }

    // ---------- Синхронизация ----------

    public DateTimeOffset FirstSyncedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Когда сотрудник последний раз пришёл в выгрузке Verifix.</summary>
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Не пришёл в последней полной выгрузке (удалён в Verifix или временно недоступен); null — есть в Verifix.</summary>
    public DateTimeOffset? MissingSince { get; set; }
}

/// <summary>Вид справочника Verifix.</summary>
public static class HrReferenceKinds
{
    public const string Division = "division";
    public const string Job = "job";
    public const string Schedule = "schedule";
    public const string Location = "location";
    public const string TimeKind = "time_kind";

    public static readonly IReadOnlyList<string> All = [Division, Job, Schedule, Location, TimeKind];
}

/// <summary>
/// Справочник Verifix: подразделения, должности, графики, локации (филиалы и проходные), виды времени. Ключ — (вид, ID Verifix).
/// DepartmentCode — локальная привязка подразделения к отделу OneBase (по ней руководитель отдела видит своих сотрудников); синхронизация
/// её не меняет.
/// </summary>
public class HrReference
{
    public required string Kind { get; set; }
    public long VerifixId { get; set; }
    public required string Name { get; set; }

    /// <summary>Код: буквенный код вида времени (Я, ОП, ОТС…), вид графика (C, F, H, M, A), код должности.</summary>
    public string? Code { get; set; }

    /// <summary>Родитель подразделения.</summary>
    public long? ParentId { get; set; }

    /// <summary>A — активен, P — пассивен.</summary>
    public string? State { get; set; }

    /// <summary>Адрес локации.</summary>
    public string? Address { get; set; }

    /// <summary>Подразделение закрыто (closed_date).</summary>
    public DateOnly? ClosedDate { get; set; }

    /// <summary>Локально: отдел OneBase, к которому относится подразделение.</summary>
    public string? DepartmentCode { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MissingSince { get; set; }
}

/// <summary>
/// Отметка Verifix (track$list): одна строка на track_id — повторная загрузка обновляет её, а не создаёт дубликат.
/// Время хранится трижды: как пришло (RawTime, «dd.mm.yyyy hh24:mi:ss» во временной зоне организации), локальное время организации
/// (LocalTime, без зоны) и момент в UTC (OccurredAt). Тип отметки — как прислал Verifix (track_type I/O/C/M/T/N/P): одна отметка —
/// ещё не рабочий день, приход и уход считаются только по типам I и O. Отметка, пропавшая из Verifix за полностью перезагруженный
/// период, не удаляется: MissingSince — и в расчётах она не участвует.
/// </summary>
public class HrAttendanceEvent
{
    public long Id { get; set; }

    /// <summary>track_id в Verifix.</summary>
    public long VerifixTrackId { get; set; }

    public long VerifixEmployeeId { get; set; }

    /// <summary>Сотрудник OneBase; null — сотрудник ещё не загружен (свяжется следующей синхронизацией).</summary>
    public Guid? EmployeeId { get; set; }

    public long? StaffId { get; set; }
    public long? DivisionId { get; set; }
    public long? LocationId { get; set; }
    public long? ScheduleId { get; set; }

    /// <summary>track_type: I — приход, O — уход, C — отметка присутствия, M — продолжение рабочего дня, T — уход на перерыв, N — приход с перерыва, P — не описан в документации.</summary>
    public required string TrackType { get; set; }

    /// <summary>mark_type: способ отметки (F — лицо, R — карта, T — отпечаток, M — веб…).</summary>
    public string? MarkType { get; set; }

    public required string RawTime { get; set; }
    public DateTime LocalTime { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ChangedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MissingSince { get; set; }
}

/// <summary>
/// День табеля Verifix (timesheet$export): график дня (вид дня, начало и конец, перерыв, план), первый приход и последний уход
/// по действительным интервалам, факты по видам времени. Ключ — (staff_id, дата). Дни без графика Verifix не присылает.
/// Время — локальное время организации, как в ответе.
/// </summary>
public class HrTimesheetDay
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public DateOnly Date { get; set; }
    public long VerifixEmployeeId { get; set; }
    public Guid? EmployeeId { get; set; }
    public long? DivisionId { get; set; }
    public long? JobId { get; set; }
    public long? ScheduleId { get; set; }

    /// <summary>W — рабочий, R — выходной, A — доп. выходной, H — праздник, N — нерабочий.</summary>
    public required string DayKind { get; set; }

    public DateTime? PlanBegin { get; set; }
    public DateTime? PlanEnd { get; set; }
    public DateTime? BreakBegin { get; set; }
    public DateTime? BreakEnd { get; set; }
    public int? PlanMinutes { get; set; }
    public DateTime? FirstIn { get; set; }
    public DateTime? LastOut { get; set; }
    public long? InputLocationId { get; set; }
    public long? OutputLocationId { get; set; }
    public int? DoneMarks { get; set; }
    public int? PlannedMarks { get; set; }

    /// <summary>Факты как прислал Verifix: JSON [{"k": time_kind_id, "v": fact_value}]. Единица fact_value в документации не согласована с примером.</summary>
    public string FactsJson { get; set; } = "[]";

    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MissingSince { get; set; }
}

/// <summary>Статус запуска синхронизации.</summary>
public static class HrSyncStatus
{
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";

    /// <summary>Часть шагов не прошла — загруженное сохранено.</summary>
    public const string Partial = "Partial";

    public const string Failed = "Failed";
}

/// <summary>Кто запустил: расписание, кнопка, повтор неудачного запуска.</summary>
public static class HrSyncTrigger
{
    public const string Auto = "auto";
    public const string Manual = "manual";
    public const string Retry = "retry";
}

/// <summary>
/// Журнал синхронизации Verifix: начало, конец, загружено (новых и изменённых), пропущено (без изменений и непригодных строк), ошибки,
/// шаги (StepsJson). Одновременно идёт не больше одного запуска — уникальный индекс по строкам со статусом Running.
/// </summary>
public class HrSyncRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Trigger { get; set; }
    public Guid? StartedById { get; set; }
    public Guid? RetryOfId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset HeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = HrSyncStatus.Running;
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }

    /// <summary>Период отметок и табеля этого запуска (локальные даты организации).</summary>
    public DateOnly? PeriodFrom { get; set; }

    public DateOnly? PeriodTo { get; set; }

    /// <summary>Итог или первая ошибка — без секретов.</summary>
    public string? Message { get; set; }

    /// <summary>Шаги: JSON [{step, status, created, updated, skipped, errors, pages, message}].</summary>
    public string StepsJson { get; set; } = "[]";
}

/// <summary>
/// Состояние загрузки по данным Verifix: последний успех, ошибка и до какого момента данные полные (CoveredThrough — момент, до которого
/// отметки загружены целиком; по нему отсутствие отличается от «данные ещё не пришли»).
/// </summary>
public class HrSyncState
{
    public required string Entity { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? CoveredThrough { get; set; }

    /// <summary>С какой даты данные загружены (начало истории).</summary>
    public DateOnly? CoveredFrom { get; set; }

    public int LastRows { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
}
