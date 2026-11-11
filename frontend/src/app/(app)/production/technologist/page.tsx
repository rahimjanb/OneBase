import { requireProduction } from "@/components/production/guard";
import { TechnologistDashboard } from "@/components/production/TechnologistDashboard";
import { getT } from "@/i18n/server";
import type { ProductionDashboard } from "@/lib/production";
import { apiOutcome } from "@/lib/server-api";
import { qs, todayInTashkent } from "@/lib/tasks";
import { parsePeriod, periodOfRange, periodRange } from "@/lib/technologist-view";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Технолог · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Производство → Технолог»: выпуск, план, брак, расход к норме, выпечка, задания в работе и что требует внимания. Период — ?period=this_month
 * (по умолчанию), last_month, 3_months, year; старые ссылки с from/to работают как прежде. Сбой ответа — плашка с «Повторить» на самой странице.
 */
export default async function TechnologistPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/technologist";
  await requireProduction(path);
  const sp = await searchParams;
  const today = todayInTashkent();
  const from = one(sp, "from");
  const to = one(sp, "to");
  const asked = parsePeriod(one(sp, "period"));
  const custom = !asked && (from || to);
  const range = custom ? { from, to } : periodRange(asked ?? "this_month", today);
  const period = custom ? (from && to ? periodOfRange(from, to, today) : null) : (asked ?? "this_month");
  const outcome = await apiOutcome<ProductionDashboard>(`/api/production/technologist${qs({ from: range.from, to: range.to, department: one(sp, "department") })}`);
  return <TechnologistDashboard data={outcome.state === "ok" ? outcome.data : null} period={period} error={outcome.state !== "ok"} />;
}
