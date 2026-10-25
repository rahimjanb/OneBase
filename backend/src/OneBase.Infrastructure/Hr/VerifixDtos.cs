namespace OneBase.Infrastructure.Hr;

// Ответы Verifix Public API v1.0 (документация Postman, коллекция 23097602/2s83ziN3VR). Поля — только те, что нужны учёту времени:
// ПИНФЛ, ИНН, паспорт, адреса, фото и признаки биометрии, номер карты доступа, семья, образование и медосмотры в DTO не описаны —
// System.Text.Json их пропускает, в память и в базу OneBase они не попадают.

public sealed record VerifixToken(string? AccessToken, string? TokenType, int? ExpiresIn);

public sealed record VerifixMeta(int? Count, long? NextCursor);

public sealed record VerifixPage<T>(List<T>? Data, VerifixMeta? Meta);

public sealed record VerifixEmployeeDto(
    long EmployeeId,
    string? FirstName,
    string? LastName,
    string? MiddleName,
    string? PhoneNumber,
    string? Email,
    string? State,
    string? Status,
    string? HiringDate,
    string? DismissalDate,
    long? JobId,
    long? DivisionId,
    long? ScheduleId,
    long? OrgUnitId);

public sealed record VerifixTrackDto(
    long TrackId,
    long EmployeeId,
    long? StaffId,
    long? ScheduleId,
    long? DivisionId,
    long? LocationId,
    string? TrackType,
    string? MarkType,
    string? TrackDatetime);

public sealed record VerifixTimesheetFactDto(long TimeKindId, long? FactValue);

public sealed record VerifixTimesheetDayDto(
    string? Date,
    string? DayKind,
    string? BeginTime,
    string? EndTime,
    string? BreakBeginTime,
    string? BreakEndTime,
    string? InputTime,
    string? OutputTime,
    long? InputLocationId,
    long? OutputLocationId,
    int? PlanTime,
    int? DoneMarks,
    int? PlannedMarks,
    List<VerifixTimesheetFactDto>? Facts);

public sealed record VerifixTimesheetDto(
    long EmployeeId,
    long StaffId,
    string? EmployeeName,
    long? JobId,
    long? DivisionId,
    long? ScheduleId,
    List<VerifixTimesheetDayDto>? Days);

public sealed record VerifixDivisionDto(long DivisionId, string? Name, long? ParentId, string? ClosedDate, string? State);

public sealed record VerifixJobDto(long JobId, string? Name, string? Code, string? State);

public sealed record VerifixScheduleDto(long ScheduleId, string? Name, string? Kind, string? State);

public sealed record VerifixLocationDto(long LocationId, string? Name, string? Address, string? State);

public sealed record VerifixTimeKindDto(long TimeKindId, string? Name, string? LetterCode);

public sealed record VerifixFilialDto(string? FilialName, string? RegionName);
