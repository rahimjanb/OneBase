"use client";

import { Fragment } from "react";
import { ArrowDown, ArrowRight, PackageCheck, Wheat } from "lucide-react";
import { useT } from "@/i18n/client";
import { quantity } from "@/lib/procurement";
import { stageFlow, stageKindLabel, type CalcResult, type RecipeDetail } from "@/lib/production";

function Row({ label, value, note, tone = "text-ink" }: { label: string; value: string; note?: string; tone?: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2 tabular-nums">
      <dt className="shrink-0 text-ink-3">{label}</dt>
      <dd className={`text-right ${tone}`}>
        {value}
        {note && <span className="block text-[10px] text-ink-3">{note}</span>}
      </dd>
    </div>
  );
}

/**
 * Схема технологической цепочки: что поступает на этап (поток предыдущего и компоненты этапа), выход и потери массы, брак (и можно ли
 * перерабатывать), что уходит дальше — по расчёту на выход рецептуры (например, на 1 000 кг). Сырьё → этапы → готовая продукция.
 */
export function RecipeChain({ recipe, calc }: { recipe: RecipeDetail; calc: CalcResult | null }) {
  const t = useT();
  const kg = (n: number) => `${quantity(n)} ${t("кг")}`;
  const raw = new Set(recipe.lines.map((l) => l.itemId)).size;
  const Arrow = () => (
    <span className="grid shrink-0 place-items-center text-ink-4 max-lg:py-1 lg:px-0.5" aria-hidden>
      <ArrowRight className="size-4 max-lg:hidden" />
      <ArrowDown className="size-4 lg:hidden" />
    </span>
  );
  return (
    <div className="overflow-x-auto pb-1">
      <ol className="flex flex-col items-stretch lg:flex-row lg:items-stretch" aria-label={t("Схема цепочки")}>
        <li className="flex shrink-0 flex-col justify-center rounded-xl bg-muted px-3 py-3 text-sm lg:w-20 lg:px-2.5">
          <Wheat className="mb-1 size-4 text-ink-3" aria-hidden />
          <span className="font-medium text-ink">{t("Сырьё")}</span>
          <span className="text-xs text-ink-3">{t("позиций: {n}", { n: raw })}</span>
        </li>
        {recipe.stages.map((s) => {
          const c = calc?.stages.find((x) => x.no === s.stageNo);
          const flow = c ? stageFlow(c) : null;
          const parts = recipe.lines.filter((l) => l.stageId === s.id);
          const batch = s.kind === "Mixing" ? calc?.batches : null;
          return (
            <Fragment key={s.id}>
              <Arrow />
              <li className="flex flex-col rounded-xl border border-line bg-surface p-3 text-sm lg:min-w-[150px] lg:flex-1 lg:p-2.5">
                <div className="flex items-start gap-2">
                  <span className="grid size-6 shrink-0 place-items-center rounded-md bg-accent-soft text-xs font-semibold tabular-nums text-accent-strong">{s.stageNo}</span>
                  <span className="min-w-0">
                    <span className="block font-semibold leading-tight text-ink">{s.name}</span>
                    <span className="block text-[11px] text-ink-3">
                      {[t(stageKindLabel[s.kind]) !== s.name ? t(stageKindLabel[s.kind]) : null, s.area].filter(Boolean).join(" · ")}
                    </span>
                  </span>
                </div>
                <dl className="mt-2 space-y-0.5 text-[11px]">
                  {parts.length > 0 && (
                    <div className="mb-1">
                      <dt className="text-ink-3">{s.kind === "Mixing" ? t("Рецептура замеса") : t("Добавляется")}</dt>
                      <dd className="line-clamp-2 text-ink-2" title={parts.map((p) => p.name).join(", ")}>
                        {parts.map((p) => p.name).join(", ")}
                      </dd>
                    </div>
                  )}
                  {batch ? (
                    <Row label={t("Замесов")} value={`${batch.batches.length} · ${kg(batch.totalKg)}`} note={batch.surplusKg > 0 ? t("излишек {kg}", { kg: kg(batch.surplusKg) }) : undefined} />
                  ) : (
                    flow && <Row label={t("Вход")} value={kg(flow.inKg)} note={flow.addedKg > 0 ? t("в т. ч. компоненты {kg}", { kg: kg(flow.addedKg) }) : undefined} />
                  )}
                  <Row label={t("Выход")} value={`${quantity(s.yieldPercent)} %`} />
                  {flow && flow.lossKg > 0 && <Row label={t("Потери")} value={kg(flow.lossKg)} tone="text-warn" />}
                  <Row
                    label={t("Брак")}
                    value={s.defectPercent > 0 ? `${quantity(s.defectPercent)} %${flow && flow.defectKg > 0 ? ` · ${kg(flow.defectKg)}` : ""}` : t("нет")}
                    tone={s.defectPercent > 0 ? "text-bad" : "text-ink-3"}
                    note={s.reworkable && s.defectPercent > 0 ? t("в переработку") : undefined}
                  />
                </dl>
                {flow && (
                  <p className="mt-auto border-t border-line pt-2 text-xs tabular-nums text-ink-2">
                    {t("Дальше")}: <span className="font-semibold text-ink">{kg(flow.nextKg)}</span>
                  </p>
                )}
              </li>
            </Fragment>
          );
        })}
        <Arrow />
        <li className="flex shrink-0 flex-col justify-center rounded-xl bg-ok-soft px-3 py-3 text-sm lg:w-24 lg:px-2.5">
          <PackageCheck className="mb-1 size-4 text-ok" aria-hidden />
          <span className="font-medium text-ink">{t("Готовая продукция")}</span>
          <span className="text-xs tabular-nums text-ink-2">
            {quantity(recipe.outputQuantity)} {recipe.outputUnit}
          </span>
        </li>
      </ol>
      {calc === null && recipe.stages.length > 0 && <p className="mt-2 text-xs text-ink-3">{t("Массы по этапам появятся, когда карта будет считаться без ошибок.")}</p>}
    </div>
  );
}
