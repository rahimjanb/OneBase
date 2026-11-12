"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Archive, GitCompareArrows, Loader2, Sparkles } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { date, dateTime, num } from "@/lib/analytik";
import { deltaTone, groupLabel, pct, presetLabel, simValue, type ScenarioComparison, type ScenarioRow } from "@/lib/simulator";
import { Card, Empty } from "../ui";
import { Sheet } from "./SimSheet";

/**
 * «Мои сценарии» — панель поверх экрана: открыть сценарий, отметить до maxCompare для сравнения («Сравнить (N)» внизу), архив.
 * Чужие видит только управляющий Аналитиком (решает сервер). actions — действия с текущим сценарием (на телефоне — здесь же).
 */
export function ScenarioListSheet({
  open,
  onClose,
  currentId,
  maxCompare,
  refreshKey,
  compare,
  actions,
}: {
  open: boolean;
  onClose: () => void;
  currentId: string | null;
  maxCompare: number;
  refreshKey: number;
  compare: boolean;
  actions?: React.ReactNode;
}) {
  const t = useT();
  const [archived, setArchived] = useState(false);
  const [rows, setRows] = useState<ScenarioRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>(currentId ? [currentId] : []);
  useEffect(() => {
    if (!open) return;
    let alive = true;
    setRows(null);
    setError(null);
    bff<ScenarioRow[]>(`analytik/simulator/scenarios?archived=${archived}`)
      .then((r) => alive && setRows(r))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [open, archived, refreshKey]);
  const toggle = (id: string) => setSelected((s) => (s.includes(id) ? s.filter((x) => x !== id) : s.length >= maxCompare ? s : [...s, id]));
  return (
    <Sheet
      open={open}
      onClose={onClose}
      label={compare ? "Сравнить сценарии" : "Мои сценарии"}
      side="right"
      width="md:w-[520px]"
      header={
        <div className="flex items-center gap-2">
          <div className="min-w-0 flex-1">
            <h2 className="text-[17px] font-bold text-ink">{t(compare ? "Сравнить сценарии" : "Мои сценарии")}</h2>
            <p className="text-[11px] text-ink-4">{t("Отметьте от 2 до {n} сценариев для сравнения", { n: maxCompare })}</p>
          </div>
          <button type="button" onClick={() => setArchived(!archived)} aria-pressed={archived} className={`inline-flex h-8 shrink-0 items-center gap-1 rounded-lg border px-2.5 text-[12px] ${archived ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-3"}`}>
            <Archive className="size-3.5" aria-hidden />
            {t("Архив")}
          </button>
        </div>
      }
      footer={
        selected.length >= 2 ? (
          <Link href={`/analytik/simulator/compare?ids=${selected.join(",")}`} className="inline-flex h-11 w-full items-center justify-center gap-1.5 rounded-xl bg-accent text-[14px] font-semibold text-white">
            <GitCompareArrows className="size-4" aria-hidden />
            {t("Сравнить")} ({selected.length})
          </Link>
        ) : (
          <p className="text-center text-[12px] text-ink-4">{t("Отметьте ещё {n}, чтобы сравнить", { n: 2 - selected.length })}</p>
        )
      }
    >
      {actions}
      <div className="py-3">
        {error && <p className="rounded-xl bg-bad-soft px-3 py-2 text-[13px] text-bad">{error}</p>}
        {!rows && !error && <Loader2 className="mx-auto my-6 size-5 animate-spin text-ink-4" />}
        {rows && rows.length === 0 && <Empty text={archived ? "В архиве сценариев нет" : "Сохранённых сценариев пока нет"} hint="Задайте параметры и нажмите «Сохранить»." />}
        {rows && rows.length > 0 && (
          <ul className="space-y-2">
            {rows.map((r) => (
              <li key={r.id} className={`flex items-start gap-3 rounded-xl border p-3 ${r.id === currentId ? "border-accent/50 bg-accent-soft" : "border-line bg-muted/60"}`}>
                <input type="checkbox" checked={selected.includes(r.id)} onChange={() => toggle(r.id)} aria-label={`${t("Сравнить")}: ${r.name}`} className="mt-1 size-4 shrink-0 accent-[var(--color-accent)]" />
                <div className="min-w-0 flex-1">
                  <Link href={`/analytik/simulator?id=${r.id}`} onClick={onClose} className="block text-[13.5px] font-semibold text-ink hover:text-accent-strong">
                    {r.name}
                  </Link>
                  <div className="mt-0.5 flex flex-wrap gap-x-2 text-[11px] text-ink-4">
                    {r.preset !== "custom" && <span>{t(presetLabel[r.preset] ?? r.preset)}</span>}
                    <span>
                      {t("база")} {date(r.baseFrom)} — {date(r.baseTo)}
                    </span>
                    <span>
                      {r.horizonDays} {t("дн.")}
                    </span>
                    <span>{dateTime(r.updatedAt ?? r.calculatedAt ?? r.createdAt)}</span>
                    <span className={r.own ? "" : "text-violet"}>{r.own ? t("ваш") : t("другого сотрудника")}</span>
                    {r.hasAi && (
                      <span className="inline-flex items-center gap-0.5 text-violet">
                        <Sparkles className="size-3" aria-hidden />
                        AI
                      </span>
                    )}
                  </div>
                </div>
                <div className="shrink-0 text-right">
                  {r.revenueDeltaPercent != null && (
                    <div className={`rounded-md px-1.5 py-0.5 text-[11px] font-bold tabular-nums ${Math.abs(r.revenueDeltaPercent) < 0.05 ? "bg-muted text-ink-4" : r.revenueDeltaPercent > 0 ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>
                      {t("выручка")} {pct(r.revenueDeltaPercent)}
                    </div>
                  )}
                  {r.critical > 0 && <div className="mt-1 text-[11px] text-bad">{t("ограничений")}: {r.critical}</div>}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Sheet>
  );
}

/** Сравнение сценариев: значения каждого и разница с первым (абсолютная и в %), риски и критичные ограничения. */
export function CompareView({ ids }: { ids: string[] }) {
  const t = useT();
  const [data, setData] = useState<ScenarioComparison | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let alive = true;
    bff<ScenarioComparison>(`analytik/simulator/compare?ids=${ids.join(",")}`)
      .then((d) => alive && setData(d))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [ids]);
  if (error) return <p className="rounded-xl bg-bad-soft px-4 py-3 text-[13px] text-bad">{error}</p>;
  if (!data) return <Loader2 className="size-6 animate-spin text-ink-4" />;
  const groups = [...new Set(data.rows.map((r) => r.group))];
  const revenue = data.rows.find((r) => r.code === "sales.revenue");
  const max = revenue ? Math.max(1, ...revenue.values.map((v) => Math.abs(v ?? 0))) : 1;
  return (
    <div className="space-y-4">
      {data.notes.map((n) => (
        <p key={n} className="rounded-xl border border-warn/30 bg-warn-soft px-4 py-2.5 text-[13px] text-warn">
          {t(n)}
        </p>
      ))}
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {data.columns.map((c, i) => (
          <Card key={c.id} title={c.name} subtitle={`${t(presetLabel[c.preset] ?? c.preset)} · ${c.horizonDays} ${t("дн.")} · ${t("данные на")} ${date(c.asOf)}`} href={`/analytik/simulator?id=${c.id}`}>
            {revenue && (
              <div className="mb-2">
                <div className="flex justify-between text-[12px] text-ink-3">
                  <span>{t("Выручка")}</span>
                  <span className="font-medium tabular-nums text-ink">{simValue(revenue.values[i], "сум")}</span>
                </div>
                <div className="mt-1 h-2 overflow-hidden rounded-full bg-muted">
                  <div className={`h-full rounded-full ${i === 0 ? "bg-ink-4/60" : "bg-accent"}`} style={{ width: `${(Math.abs(revenue.values[i] ?? 0) / max) * 100}%` }} />
                </div>
              </div>
            )}
            {c.headline && <p className="text-[12.5px] text-ink-2">{t(c.headline)}</p>}
          </Card>
        ))}
      </div>
      <Card title="Сравнение показателей" subtitle={`${t("Разница — к первому сценарию")}: ${data.columns[0]?.name ?? ""}`}>
        <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
          <table className="w-full min-w-[720px]">
            <thead>
              <tr className="border-b border-line">
                <th className="px-2 py-2 text-left text-[11px] font-medium uppercase tracking-wide text-ink-4">{t("Показатель")}</th>
                {data.columns.map((c) => (
                  <th key={c.id} className="max-w-40 px-2 py-2 text-right text-[11px] font-medium text-ink-3">
                    {c.name}
                  </th>
                ))}
              </tr>
            </thead>
            {groups.map((g) => (
              <tbody key={g}>
                <tr>
                  <td colSpan={data.columns.length + 1} className="px-2 pb-1 pt-3 text-xs font-semibold text-ink">
                    {t(groupLabel[g] ?? g)}
                  </td>
                </tr>
                {data.rows
                  .filter((r) => r.group === g)
                  .map((r) => (
                    <tr key={r.code} className="border-b border-line/60">
                      <td className="px-2 py-1.5 text-[12.5px] text-ink-2">{t(r.title)}</td>
                      {r.values.map((v, i) => (
                        <td key={i} className="px-2 py-1.5 text-right text-[12.5px] tabular-nums text-ink">
                          {simValue(v, r.unit)}
                          {i > 0 && r.delta[i] != null && r.delta[i] !== 0 && (
                            <div className={`text-[11px] ${deltaTone(r.code, r.delta[i]) === "ok" ? "text-ok" : deltaTone(r.code, r.delta[i]) === "bad" ? "text-bad" : "text-ink-4"}`}>
                              {r.delta[i]! > 0 ? "+" : "−"}
                              {simValue(Math.abs(r.delta[i]!), r.unit)}
                              {r.deltaPercent[i] != null && r.unit !== "%" ? ` (${r.deltaPercent[i]! > 0 ? "+" : ""}${num(r.deltaPercent[i], 1)} %)` : ""}
                            </div>
                          )}
                        </td>
                      ))}
                    </tr>
                  ))}
              </tbody>
            ))}
          </table>
        </div>
      </Card>
    </div>
  );
}
