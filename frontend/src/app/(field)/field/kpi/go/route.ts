import { NextResponse, type NextRequest } from "next/server";

/** Форма выбора месяца KPI: ym=2026-9 → /field/kpi?year=2026&month=9 (относительный адрес — за nginx домен подставит браузер). */
export async function GET(request: NextRequest) {
  const ym = request.nextUrl.searchParams.get("ym") ?? "";
  const [year, month] = ym.split("-").map(Number);
  const location = year && month ? `/field/kpi?year=${year}&month=${month}` : "/field/kpi";
  return new NextResponse(null, { status: 303, headers: { Location: location } });
}
