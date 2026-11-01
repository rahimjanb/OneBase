using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Application.Procurement;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Номера документов снабжения одной командой PostgreSQL (INSERT … ON CONFLICT DO UPDATE … RETURNING): строка счётчика блокируется на время
/// команды, поэтому одновременные заявки получают разные номера. Номер, выданный заявке, которая не сохранилась, пропускается — повторов нет.
/// </summary>
internal sealed class ProcurementNumbers(OneBaseDbContext db) : IProcurementNumbers
{
    public async Task<string> NextAsync(string prefix, CancellationToken ct)
    {
        var year = FieldClock.Today.Year;
        var rows = await db.Database.SqlQuery<long>($"""
            INSERT INTO procurement."Counters" ("Prefix", "Year", "Last") VALUES ({prefix}, {year}, 1)
            ON CONFLICT ("Prefix", "Year") DO UPDATE SET "Last" = procurement."Counters"."Last" + 1
            RETURNING "Last" AS "Value"
            """).ToListAsync(ct);
        return ProcurementRules.Number(prefix, year, rows.Single());
    }
}
