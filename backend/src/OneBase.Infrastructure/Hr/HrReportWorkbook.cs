using ClosedXML.Excel;
using OneBase.Application.Hr;

namespace OneBase.Infrastructure.Hr;

/// <summary>Отчёт HR в Excel: «Подразделения» (итоги), «Табель» (сотрудник × день буквами), «Отклонения».</summary>
public static class HrReportWorkbook
{
    public static byte[] Build(HrReport report, HrTimesheet timesheet, IReadOnlyList<HrDeviation> deviations)
    {
        using var workbook = new XLWorkbook();

        var groups = workbook.Worksheets.Add("Подразделения");
        groups.Cell(1, 1).Value = $"Посещаемость {report.From:dd.MM.yyyy} — {report.To:dd.MM.yyyy} (данные Verifix)";
        groups.Cell(1, 1).Style.Font.Bold = true;
        string[] head = ["Подразделение", "Сотрудников", "Рабочих дней по графику", "Дней с приходом", "Опозданий", "Минут опозданий", "Ранних уходов",
            "Отсутствий", "Дней по документам", "Не ясно", "Отработано, ч", "План, ч", "Явка, %", "Без опозданий, %"];
        Header(groups, 3, head);
        var row = 4;
        foreach (var g in report.Divisions)
        {
            Summary(groups, row++, g.Division, g.Employees, g.Summary, g.AttendanceRate, g.Punctuality);
        }

        Summary(groups, row, "Итого", report.Employees, report.Total, report.AttendanceRate, report.Punctuality);
        groups.Row(row).Style.Font.Bold = true;
        groups.Columns().AdjustToContents(1, 60);

        var sheet = workbook.Worksheets.Add("Табель");
        string[] people = ["Сотрудник", "Подразделение", "Должность"];
        Header(sheet, 1, [.. people, .. timesheet.Dates.Select(d => d.ToString("dd.MM")), "Явка", "Опоздания", "Отсутствия", "Отработано, ч"]);
        row = 2;
        foreach (var r in timesheet.Rows)
        {
            sheet.Cell(row, 1).Value = r.Employee;
            sheet.Cell(row, 2).Value = r.Division ?? "";
            sheet.Cell(row, 3).Value = r.Job ?? "";
            for (var i = 0; i < r.Codes.Count; i++)
            {
                var cell = sheet.Cell(row, people.Length + 1 + i);
                cell.Value = r.Codes[i];
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (r.Codes[i] is "ОТС")
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE2E1");
                }
                else if (r.Codes[i] is "ОП" or "РУ")
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                }
            }

            var c = people.Length + r.Codes.Count + 1;
            sheet.Cell(row, c).Value = r.Summary.PresentDays;
            sheet.Cell(row, c + 1).Value = r.Summary.LateDays;
            sheet.Cell(row, c + 2).Value = r.Summary.AbsentDays;
            sheet.Cell(row, c + 3).Value = Math.Round(r.Summary.WorkedMinutes / 60.0, 1);
            row++;
        }

        sheet.SheetView.FreezeColumns(1);
        sheet.Column(1).AdjustToContents(1, 60);
        sheet.Column(2).AdjustToContents(1, 60);
        sheet.Cell(row + 1, 1).Value = "Я — явка, ОП — опоздание, РУ — ранний уход, РВ — работа в нерабочий день, ОТС — отсутствие, ОТ/Б/К/О/НО — по документу, "
                                       + "В — выходной, П — праздник, · — день ещё не начался, — — нет графика, ? — данных недостаточно.";

        var list = workbook.Worksheets.Add("Отклонения");
        Header(list, 1, ["Дата", "Сотрудник", "Подразделение", "Отклонение", "Минут", "Начало по графику", "Конец по графику", "Приход", "Уход", "Пояснение"]);
        row = 2;
        foreach (var d in deviations)
        {
            list.Cell(row, 1).Value = d.Date.ToString("dd.MM.yyyy");
            list.Cell(row, 2).Value = d.Employee;
            list.Cell(row, 3).Value = d.Division ?? "";
            list.Cell(row, 4).Value = d.Kind switch { "late" => "Опоздание", "early" => "Ранний уход", "absent" => "Отсутствие", _ => "Нет ухода" };
            if (d.Minutes is { } m)
            {
                list.Cell(row, 5).Value = m;
            }

            list.Cell(row, 6).Value = d.PlanBegin?.ToString("HH:mm") ?? "";
            list.Cell(row, 7).Value = d.PlanEnd?.ToString("HH:mm") ?? "";
            list.Cell(row, 8).Value = d.FirstIn?.ToString("HH:mm") ?? "";
            list.Cell(row, 9).Value = d.LastOut?.ToString("HH:mm") ?? "";
            list.Cell(row, 10).Value = d.Reason ?? "";
            row++;
        }

        list.Columns().AdjustToContents(1, 60);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Header(IXLWorksheet sheet, int row, IReadOnlyList<string> titles)
    {
        for (var i = 0; i < titles.Count; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = titles[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        }
    }

    private static void Summary(IXLWorksheet sheet, int row, string name, int employees, HrSummary s, double? attendance, double? punctuality)
    {
        sheet.Cell(row, 1).Value = name;
        sheet.Cell(row, 2).Value = employees;
        sheet.Cell(row, 3).Value = s.PlanDays;
        sheet.Cell(row, 4).Value = s.PresentDays;
        sheet.Cell(row, 5).Value = s.LateDays;
        sheet.Cell(row, 6).Value = s.LateMinutes;
        sheet.Cell(row, 7).Value = s.EarlyDays;
        sheet.Cell(row, 8).Value = s.AbsentDays;
        sheet.Cell(row, 9).Value = s.LeaveDays;
        sheet.Cell(row, 10).Value = s.UnknownDays;
        sheet.Cell(row, 11).Value = Math.Round(s.WorkedMinutes / 60.0, 1);
        sheet.Cell(row, 12).Value = Math.Round(s.PlanMinutes / 60.0, 1);
        if (attendance is { } a)
        {
            sheet.Cell(row, 13).Value = a;
        }

        if (punctuality is { } p)
        {
            sheet.Cell(row, 14).Value = p;
        }
    }
}
