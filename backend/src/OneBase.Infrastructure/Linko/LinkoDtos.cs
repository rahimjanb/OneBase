using System.Text.Json;

namespace OneBase.Infrastructure.Linko;

// Формат ответов Linko External API (snake_case, см. https://sfademo.linko.uz/docs/).

public sealed record LinkoPage<T>(List<T>? Results, List<LinkoItemError>? Errors);

public sealed record LinkoItemError(long? Id, string? ServiceId, JsonElement? Error);

public sealed record LinkoCount(long Count);

public sealed record LinkoRef(long Id, string? Name);

public sealed record LinkoLocation(double? Lat, double? Lon);

public sealed record LinkoMeasurement(long? Id, string? Name, bool? IsWeighted);

public sealed record LinkoUserDto(
    long Id,
    string? Username,
    string? FirstName,
    string? SecondName,
    bool? IsActive,
    LinkoRef? Job,
    LinkoRef? Position);

public sealed record LinkoMarketDto(
    long Id,
    string? Name,
    LinkoRef? MarketType,
    LinkoRef? ResponsibleAgent,
    LinkoRef? Branch,
    string? Address,
    LinkoLocation? Location,
    decimal? Tm);

public sealed record LinkoProductDto(
    long Id,
    string? Name,
    string? Code,
    LinkoRef? Type,
    LinkoMeasurement? Measurement,
    decimal? Tm);

public sealed record LinkoProductTypeDto(long Id, string? Name, LinkoRef? Parent, decimal? Tm);

public sealed record LinkoBorderDto(long Id, string? Name, long? ParentId, bool? IsDelete, decimal? Tm);

public sealed record LinkoMarketUserDto(long Id, long UserId, long MarketId, bool? IsDelete);

public sealed record LinkoOrderLineDto(
    long Id,
    LinkoRef? Product,
    decimal? Amount,
    decimal? ReturnAmount,
    decimal? Price,
    decimal? TotalPrice,
    decimal? TotalWeight);

public sealed record LinkoOrderDto(
    long Id,
    DateTime CreatedDate,
    DateTime? DateDelivery,
    string? Status,
    LinkoRef? Market,
    LinkoRef? Branch,
    LinkoRef? Agent,
    decimal? TotalPrice,
    decimal? TotalWeight,
    decimal? DiscountPrice,
    bool? IsFullReturn,
    LinkoRef? Currency,
    List<LinkoOrderLineDto>? Products,
    decimal? Tm,
    DateTime? AcceptedTime = null);

public sealed record LinkoReturnLineDto(
    long Id,
    decimal? Price,
    LinkoRef? Product,
    long? OrderProductId,
    decimal? Amount,
    decimal? TotalWeight,
    long? OrderId);

public sealed record LinkoReturnDto(
    long Id,
    DateTime CreatedDate,
    string? Status,
    LinkoRef? Market,
    LinkoRef? Agent,
    LinkoRef? Branch,
    decimal? TotalPrice,
    decimal? TotalWeight,
    List<LinkoReturnLineDto>? ReturnedProducts,
    decimal? Tm);

public sealed record LinkoVisitDto(
    long Id,
    LinkoRef? Market,
    LinkoRef? User,
    string? Status,
    bool? IsInPlan,
    DateTime Date);

// ---------- API планов (staff_balance) ----------
// Читаются только нужные поля: зарплата, бонусы и контакты сотрудников не загружаются.

public sealed record LinkoStaffLastDate(DateTime? LastDate);

public sealed record LinkoStaffUserRef(long Id);

public sealed record LinkoStaffIndicator(long Id, string? Name, string? PlanType);

public sealed record LinkoStaffBalanceDto(
    LinkoStaffUserRef? User,
    int? Level,
    decimal? PlanAmount,
    decimal? SalesAmount,
    decimal? ReturnAmount,
    decimal? FactAmount,
    decimal? PlanForecast,
    decimal? FactPercent,
    decimal? ForecastPercent,
    LinkoStaffIndicator? PerformanceIndicator);

public sealed record LinkoKpiPlanDto(
    long Id,
    LinkoRef? User,
    int Year,
    int Month,
    decimal? Plan,
    LinkoRef? PerformanceIndicator);
