"use client";

import { AlertTriangle } from "lucide-react";
import { useT } from "@/i18n/client";
import { quantity } from "@/lib/procurement";
import { stageFlow, type CalcResult, type LineBasis, type RequirementLine } from "@/lib/production";

const th = "py-2 pr-3 font-semibold";
const num = "whitespace-nowrap py-2 pr-3 text-right tabular-nums";

/** Подпись базы нормы в расчёте: сколько замесов, штук, упаковок, коробок. */
const baseUnit: Record<Exclude<LineBasis, "Output">, string> = { Batch: "замесов", Piece: "шт.", Pack: "упаковок", Box: "коробок" };

/**
 * Результат расчёта по цепочке: замесы (стандартные и частичные, излишек теста), этапы (обработано, вход, выход, потери, брак, дальше),
 * компоненты по этапам (база, чистый расход, нормативные потери, норма) и — если есть — потребность по позициям со свободным годным на складе.
 * Ошибки карты и неполные данные показываются, а не заменяются нулями.
 */
export function RecipeCalcResult({ calc, need, warehouseName }: { calc: CalcResult; need?: RequirementLine[]; warehouseName?: string }) {
  const t = useT();
  const unit = calc.outputUnit;
  if (calc.problems.length > 0) {
    return (
      <div className="rounded-xl border border-bad/30 bg-bad-soft px-4 py-3 text-sm text-ink">
        <p className="mb-1 flex items-center gap-2 font-semibold">
          <AlertTriangle className="size-4 text-bad" aria-hidden /> {t("Карта не считается")}
        </p>
        <ul className="list-disc space-y-0.5 pl-5">
          {calc.problems.map((p) => (
            <li key={p}>{t(p)}</li>
          ))}
        </ul>
      </div>
    );
  }

  const stageName = (no: number | null) => calc.stages.find((s) => s.no === no)?.name ?? null;
  const groups = calc.stages.length > 0 ? calc.stages.map((s) => ({ no: s.no as number | null, name: s.name })) : [{ no: null, name: "" }];
  const b = calc.batches;
  return (
    <div className="space-y-4">
      {calc.missing.length > 0 && (
        <ul className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-2.5 text-sm text-ink">
          {calc.missing.map((m) => (
            <li key={m}>{t(m)}</li>
          ))}
        </ul>
      )}

      {b && (
        <div className="rounded-xl bg-muted/60 p-3 text-sm">
          <p className="text-ink">
            {t("Теста нужно {need} кг → замесов {n}: всего {total} кг", { need: quantity(b.needKg), n: b.batches.length, total: quantity(b.totalKg) })}
            {b.surplusKg > 0 && <span className="text-warn"> · {t("излишек {kg} кг", { kg: quantity(b.surplusKg) })}</span>}
          </p>
          <p className="mt-0.5 text-xs text-ink-3">
            {t("Стандартный замес {s} кг; допустимо {min}–{max} кг.", { s: quantity(b.standardKg), min: quantity(b.minKg), max: quantity(b.maxKg) })}
          </p>
          {b.batches.length > 0 && (
            <ul className="mt-2 flex flex-wrap gap-1.5">
              {b.batches.map((x) => (
                <li key={x.no} className={`rounded-md px-2 py-0.5 text-xs tabular-nums ${x.partial ? "bg-warn-soft text-warn" : "bg-surface text-ink-2"}`}>
                  {x.no}: {quantity(x.massKg)} {t("кг")}
                  {x.partial && ` · ${t("частичный")}`}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {calc.stages.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-sm">
            <caption className="mb-1 text-left text-xs font-semibold text-ink-2">{t("По этапам")}</caption>
            <thead>
              <tr className="whitespace-nowrap border-b border-line text-left text-[11px] text-ink-3">
                <th className={th}>{t("Этап")}</th>
                <th className={`${th} text-right`}>{t("Обработано, {unit}", { unit })}</th>
                <th className={`${th} text-right`}>{t("Вход, кг")}</th>
                <th className={`${th} text-right`}>{t("Потери, кг")}</th>
                <th className={`${th} text-right`}>{t("Брак, кг")}</th>
                <th className="py-2 text-right font-semibold">{t("Дальше, кг")}</th>
              </tr>
            </thead>
            <tbody>
              {calc.stages.map((s) => {
                const f = stageFlow(s);
                return (
                  <tr key={s.no} className="border-b border-line last:border-0">
                    <td className="py-2 pr-3 text-ink">
                      <span className="tabular-nums text-ink-3">{s.no}.</span> {s.name}
                      {s.defectPercent > 0 && <span className="block text-[11px] text-ink-3">{t("брак по нормативу {p} %", { p: quantity(s.defectPercent) })}</span>}
                    </td>
                    <td className={num}>{quantity(s.processed)}</td>
                    <td className={num}>{f ? quantity(f.inKg) : "—"}</td>
                    <td className={`${num} text-ink-2`}>{f ? quantity(f.lossKg) : "—"}</td>
                    <td className={`${num} ${f && f.defectKg > 0 ? "text-bad" : "text-ink-2"}`}>{f ? quantity(f.defectKg) : quantity(s.rejected)}</td>
                    <td className="whitespace-nowrap py-2 text-right tabular-nums text-ink">{f ? quantity(f.nextKg) : "—"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-sm">
          <caption className="mb-1 text-left text-xs font-semibold text-ink-2">{t("Компоненты")}</caption>
          <thead>
            <tr className="whitespace-nowrap border-b border-line text-left text-[11px] text-ink-3">
              <th className={th}>{t("Компонент")}</th>
              <th className={`${th} text-right`}>{t("База")}</th>
              <th className={`${th} text-right`}>{t("Чистый расход")}</th>
              <th className={`${th} text-right`}>{t("Потери")}</th>
              <th className="py-2 text-right font-semibold">{t("Норма")}</th>
            </tr>
          </thead>
          {groups.map((g) => {
            const rows = calc.lines.filter((l) => l.stageNo === g.no);
            if (rows.length === 0) return null;
            return (
              <tbody key={g.no ?? "all"}>
                {g.name && (
                  <tr>
                    <th colSpan={5} scope="rowgroup" className="pb-1 pt-3 text-left text-xs font-semibold text-ink-2">
                      {g.no}. {g.name}
                    </th>
                  </tr>
                )}
                {rows.map((l) => (
                  <tr key={l.key} className="border-b border-line last:border-0">
                    <td className="py-2 pr-3 text-ink">{l.name}</td>
                    <td className={`${num} text-ink-2`}>
                      {quantity(l.base)} <span className="text-[11px] text-ink-3">{l.basis === "Output" ? unit : t(baseUnit[l.basis as Exclude<LineBasis, "Output">])}</span>
                    </td>
                    <td className={num}>
                      {quantity(l.net)} {l.unit}
                    </td>
                    <td className={`${num} text-ink-2`}>{l.loss > 0 ? `${quantity(l.loss)} ${l.unit}` : "—"}</td>
                    <td className="whitespace-nowrap py-2 text-right font-semibold tabular-nums text-ink">
                      {quantity(l.gross)} {l.unit}
                      {l.unit !== l.itemUnit && (
                        <span className="block text-[11px] font-normal text-ink-3">
                          = {quantity(l.itemGross)} {l.itemUnit}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            );
          })}
        </table>
      </div>

      {need && need.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] text-sm">
            <caption className="mb-1 text-left text-xs font-semibold text-ink-2">{t("Потребность по позициям склада")}</caption>
            <thead>
              <tr className="whitespace-nowrap border-b border-line text-left text-[11px] text-ink-3">
                <th className={th}>{t("Позиция")}</th>
                <th className={`${th} text-right`}>{t("Нужно")}</th>
                <th className={`${th} text-right`}>{t("Годного свободно")}</th>
                <th className="py-2 text-right font-semibold">{t("Не хватает")}</th>
              </tr>
            </thead>
            <tbody>
              {need.map((l) => (
                <tr key={l.itemId} className="border-b border-line last:border-0">
                  <td className="py-2 pr-3 text-ink">
                    {l.name}
                    {calc.lines.filter((x) => x.itemId === l.itemId).length > 1 && (
                      <span className="block text-[11px] text-ink-3">
                        {calc.lines
                          .filter((x) => x.itemId === l.itemId)
                          .map((x) => stageName(x.stageNo))
                          .filter(Boolean)
                          .join(" + ")}
                      </span>
                    )}
                  </td>
                  <td className={num}>
                    {quantity(l.required)} {l.unitCode}
                  </td>
                  <td className={`${num} text-ink-2`}>{quantity(l.available)}</td>
                  <td className={`whitespace-nowrap py-2 text-right tabular-nums ${l.shortage > 0 ? "font-semibold text-bad" : "text-ok"}`}>
                    {l.shortage > 0 ? quantity(l.shortage) : t("хватает")}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {warehouseName && (
            <p className="mt-2 text-xs text-ink-3">{t("Склад: {name}. Годное — допущенные по качеству партии в сроке, за вычетом резерва заявок.", { name: warehouseName })}</p>
          )}
        </div>
      )}
    </div>
  );
}
