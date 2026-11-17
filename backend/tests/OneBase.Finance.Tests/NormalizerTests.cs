using System.Text.Json;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.Normalization;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Tests;

/// <summary>Проверка и приведение данных 1С: неверные записи отклоняются по одной и сохраняются целиком, суммы и курсы не пересчитываются.</summary>
public class NormalizerTests
{
    private static readonly OneCNormalizer Normalizer = new();
    private static readonly DateTimeOffset Date = new(2026, 10, 15, 14, 30, 0, TimeSpan.FromHours(5));

    [Fact]
    public void Valid_document_keeps_amounts_rate_and_lines_as_in_1C()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCDocumentDto
        {
            Id = "d1", Version = "v3", Type = "sale", SourceType = "РеализацияТоваровУслуг", Number = "0000-001234", Date = Date, OrganizationId = "o1", Posted = true,
            Currency = "usd", Amount = 1120m, VatAmount = 120m, AmountAccounting = 14_392_168m, Rate = new OneCRateRefDto(12_850.15m, 1, new DateOnly(2026, 10, 15), "ЦБ РУз"),
            Lines = [new OneCDocumentLineDto { LineNumber = 1, ItemId = "i1", Quantity = 100, Amount = 1120m, CostAmount = 9_700_000m }],
        });

        var d = Assert.Single(b.Documents);
        Assert.Empty(b.Rejections);
        Assert.Equal(("USD", 1120m, 14_392_168m, 12_850.15m, "ЦБ РУз"), (d.Currency, d.Amount, d.AmountAccounting, d.Rate!.Value, d.Rate.Source));
        Assert.Equal(9_700_000m, Assert.Single(d.Lines).CostAmount);
        Assert.Equal(new DateOnly(2026, 10, 15), b.EarliestByOrganization["o1"]);
    }

    [Fact]
    public void Missing_accounting_amount_is_taken_only_from_the_documents_own_rate()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCDocumentDto { Id = "d1", Type = "sale", Number = "1", Date = Date, OrganizationId = "o1", Currency = "USD", Amount = 100m, Rate = new OneCRateRefDto(12_500m, 1) });
        Normalizer.Add(b, new OneCDocumentDto { Id = "d2", Type = "sale", Number = "2", Date = Date, OrganizationId = "o1", Currency = "USD", Amount = 100m });
        Normalizer.Add(b, new OneCDocumentDto { Id = "d3", Type = "sale", Number = "3", Date = Date, OrganizationId = "o1", Currency = "UZS", Amount = 5_000m });

        Assert.Equal([1_250_000m, 5_000m], b.Documents.Select(d => d.AmountAccounting));
        var rejected = Assert.Single(b.Rejections);
        Assert.Equal(("document", "d2"), (rejected.EntityType, rejected.SourceId));
        Assert.Contains("без суммы в валюте учёта и без курса", rejected.Reason);
        Assert.Equal("d2", rejected.Payload.GetProperty("id").GetString());
    }

    [Fact]
    public void Broken_records_are_rejected_one_by_one_with_payload()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCAccountDto { Id = "a1", Code = "4010", Name = "Покупатели", Type = "Активный" });
        Normalizer.Add(b, new OneCAccountDto { Id = "a2", Code = "9999", Name = "?", Type = "странный" });
        Normalizer.Add(b, new OneCAccountDto { Code = "5110", Name = "Без id", Type = "active" });
        Normalizer.Add(b, new OneCCurrencyDto { Id = "c1", Code = "DOLLAR", Name = "Доллар" });
        Normalizer.Add(b, new OneCRateDto { Currency = "USD", Date = new DateOnly(2026, 10, 1), Rate = 0 });
        Normalizer.Add(b, new OneCEntrySetDto { Registrar = new OneCRegistrarDto("r1", "sale", null, "1", Date), OrganizationId = "o1",
            Entries = [new OneCEntryDto { LineNumber = 1, Amount = 10, Debit = new OneCEntrySideDto { Account = "4010" }, Credit = new OneCEntrySideDto() }] });

        Assert.Equal("active", Assert.Single(b.Accounts).Type);
        Assert.Equal(5, b.Rejections.Count);
        Assert.Contains(b.Rejections, r => r.SourceId == "a2" && r.Reason.Contains("неизвестный вид счёта"));
        Assert.Contains(b.Rejections, r => r.Reason.Contains("нет идентификатора"));
        Assert.Contains(b.Rejections, r => r.EntityType == "entry" && r.Reason.Contains("нет счёта дебета или кредита"));
        Assert.All(b.Rejections, r => Assert.Equal(JsonValueKind.Object, r.Payload.ValueKind));
    }

    [Fact]
    public void Personal_identifier_is_not_transferred()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCCounterpartyDto { Id = "p1", Name = "Иванов И. И.", Pinfl = "31234567890123", LegalForm = "individual" });
        var request = b.ToRequest(Guid.NewGuid(), "k", "ref:counterparty", "");
        Assert.DoesNotContain("31234567890123", JsonSerializer.Serialize(request));
    }

    [Fact]
    public void Changes_carry_documents_with_entries_cancellations_and_deletions()
    {
        var b = new NormalizedBatch();
        var entry = new OneCEntryDto { LineNumber = 1, Period = Date, Amount = 500, Debit = new OneCEntrySideDto { Account = "4010" }, Credit = new OneCEntrySideDto { Account = "9010" } };
        var posted = new OneCDocumentDto { Id = "d1", Type = "sale", SourceType = "Реализация", Number = "1", Date = Date, OrganizationId = "o1", Posted = true, Amount = 500, Entries = [entry] };
        var unposted = posted with { Id = "d2", Posted = false };
        Normalizer.Add(b, new OneCChangeDto { Seq = 1, EntityType = "document", Id = "d1", Data = JsonSerializer.SerializeToElement(posted, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        Normalizer.Add(b, new OneCChangeDto { Seq = 2, EntityType = "document", Id = "d2", Data = JsonSerializer.SerializeToElement(unposted, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        Normalizer.Add(b, new OneCChangeDto { Seq = 3, EntityType = "document", Id = "d3", OrganizationId = "o1", Deleted = true });
        Normalizer.Add(b, new OneCChangeDto { Seq = 4, EntityType = "spaceship", Id = "x", Data = JsonSerializer.SerializeToElement(new { id = "x" }) });

        Assert.Equal(["d1", "d2"], b.Documents.Select(d => d.SourceId));
        Assert.Equal([1, 0], b.EntrySets.Select(s => s.Entries.Count));
        Assert.Equal(("document", "d3", "o1"), (b.Deletions[0].EntityType, b.Deletions[0].SourceId, b.Deletions[0].OrganizationId));
        Assert.Contains("неизвестный тип объекта", Assert.Single(b.Rejections).Reason);
    }

    [Fact]
    public void Entry_without_period_takes_the_registrar_date_and_keeps_all_subconto()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCEntrySetDto
        {
            Registrar = new OneCRegistrarDto("r1", "sale", "Реализация", "7", Date), OrganizationId = "o1", Version = "v1",
            Entries =
            [
                new OneCEntryDto
                {
                    LineNumber = 1, Amount = 12_850_150m,
                    Debit = new OneCEntrySideDto { Account = "4010", Currency = "usd", CurrencyAmount = 1000m, Subconto = [new(1, "Контрагенты", "c1", "001", "ООО «Покупатель»"), new(2, "Договоры", "k1", null, "Договор 15"), new(3, "ДокументыРасчетов", "r1", null, "Реализация 7")] },
                    Credit = new OneCEntrySideDto { Account = "9010", Subconto = [new(1, "Номенклатура", "i1", "00-1", "Конфеты")] },
                },
            ],
        });

        var e = Assert.Single(Assert.Single(b.EntrySets).Entries);
        Assert.Equal(Date, e.Period);
        Assert.Equal(3, e.Debit.Subconto.Count);
        Assert.Equal(("USD", 1000m), (e.Debit.Currency, e.Debit.CurrencyAmount));
    }

    [Fact]
    public void Balance_needs_month_organization_and_account()
    {
        var b = new NormalizedBatch();
        Normalizer.Add(b, new OneCBalanceDto { Period = "2026-10", OrganizationId = "o1", Account = "4010", Closing = new OneCBalanceAmountsDto { Debit = 5 } });
        Normalizer.Add(b, new OneCBalanceDto { Period = "октябрь", OrganizationId = "o1", Account = "4010" });

        Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single(b.Balances).Period);
        Assert.Single(b.Rejections);
    }
}
