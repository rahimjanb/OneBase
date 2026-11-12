import Link from "next/link";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { SimulatorView } from "@/components/analytik/simulator/SimulatorView";
import { getT } from "@/i18n/server";
import { apiGet, apiGetOrNull } from "@/lib/server-api";
import { decodeDraft, type ScenarioView, type SimulatorCatalog } from "@/lib/simulator";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Симулятор сценариев · OneBase") };
}

/**
 * «Аналитик → Симулятор» (What-If): ?id — открыть сохранённый сценарий (его снимок и результат), ?draft — параметры из ответа AI-аналитика
 * («Открыть в симуляторе»), без параметров — новый сценарий на последних 3 полных месяцах. Все расчёты и права — на сервере.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const id = one(sp, "id");
  const catalog = await apiGet<SimulatorCatalog>("/api/analytik/simulator/catalog", "/analytik/simulator");
  const scenario = id ? await apiGetOrNull<ScenarioView>(`/api/analytik/simulator/scenarios/${encodeURIComponent(id)}`, "/analytik/simulator") : null;
  const draft = decodeDraft(one(sp, "draft"));
  // Палитра макета (как «Вторичка» и «Технолог»): непрозрачные карточки, свечение сверху справа и снизу слева. На телефоне и планшете
  // общая шапка скрыта (TopbarFrame) — свою рисует SimulatorView.
  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      <div className="max-lg:hidden">
        <PageHeader
          variant="display"
          title={
            <span className="inline-flex flex-wrap items-center gap-2.5">
              {t("Симулятор сценариев")}
              <span className="rounded-[7px] bg-violet-soft px-2 py-0.5 text-[11px] font-bold tracking-normal text-violet">What-If</span>
            </span>
          }
          subtitle={t("Что будет с продажами, производством, сырьём, закупками, деньгами и персоналом, если изменить параметры — по реальным данным OneBase")}
          breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Симулятор") }]}
        />
      </div>
      <PageBody variant="display">
        {id && !scenario ? (
          <div className="rounded-2xl border border-line bg-surface p-6 text-[14px] text-ink-2">
            {t("Сценарий не найден или недоступен вам.")}{" "}
            <Link href="/analytik/simulator" className="font-medium text-accent-strong hover:underline">
              {t("Новый сценарий")}
            </Link>
          </div>
        ) : (
          <SimulatorView key={scenario?.id ?? (draft ? "draft" : "new")} catalog={catalog} initial={scenario} draft={draft} />
        )}
      </PageBody>
    </div>
  );
}
