"use client";

import { useMemo, useState } from "react";
import { Loader2, Plus, Save, Search, Trash2 } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { kg, num } from "@/lib/sales/format";
import { regionSourceLabel, type TajikSetupView } from "@/lib/sales/tajik";

const field = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none disabled:opacity-60";
const select = "h-9 w-full rounded-lg border border-line bg-surface px-2 text-sm text-ink focus:border-accent focus:outline-none disabled:opacity-60";
const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

type Draft = { id: string | null; key: string; name: string; keywords: string };

const toDraft = (data: TajikSetupView): Draft[] => data.regions.map((r) => ({ id: r.id, key: r.id, name: r.name, keywords: r.keywords.join(", ") }));

/**
 * «Регионы и точки» Таджикистана: регионы со словами из названий точек, регион ТП по умолчанию, точки вручную. Порядок определения региона точки:
 * закреплена вручную → слово в названии → регион ТП → «не определён». Данные 1С не меняются. Без права «Продажи: управление» — только просмотр.
 */
export function TajikSetup({ initial, canManage }: { initial: TajikSetupView; canManage: boolean }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [drafts, setDrafts] = useState<Draft[]>(() => toDraft(initial));
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);
  const [search, setSearch] = useState("");
  const [onlyUnresolved, setOnlyUnresolved] = useState(initial.stores.some((s) => s.source === "none"));

  const dirty = JSON.stringify(drafts.map(({ id, name, keywords }) => ({ id, name, keywords }))) !== JSON.stringify(toDraft(data).map(({ id, name, keywords }) => ({ id, name, keywords })));

  const run = async (key: string, action: () => Promise<TajikSetupView>, ok: string) => {
    setBusy(key);
    setNotice(null);
    try {
      const next = await action();
      setData(next);
      setDrafts(toDraft(next));
      setNotice({ ok: true, text: ok });
    } catch (e) {
      setNotice({ ok: false, text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const saveRegions = () =>
    run(
      "regions",
      () =>
        bff<TajikSetupView>("sales/tajik/setup/regions", {
          method: "PUT",
          body: JSON.stringify(
            drafts.map((d) => ({
              id: d.id,
              name: d.name.trim(),
              keywords: d.keywords
                .split(/[,;\n]/)
                .map((k) => k.trim())
                .filter(Boolean),
            })),
          ),
        }),
      t("Регионы сохранены — отчёт пересчитан."),
    );

  const assign = (kind: "agent" | "store", target: { name?: string; storeId?: string }, regionId: string) =>
    run(
      `${kind}:${target.name ?? target.storeId}`,
      () => bff<TajikSetupView>("sales/tajik/setup/assignment", { method: "PUT", body: JSON.stringify({ kind, ...target, regionId: regionId || null }) }),
      t("Сохранено."),
    );

  const stores = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.stores.filter((s) => (!onlyUnresolved || s.source === "none") && (!q || s.name.toLowerCase().includes(q) || (s.agent ?? "").toLowerCase().includes(q)));
  }, [data.stores, search, onlyUnresolved]);

  const unresolved = data.stores.filter((s) => s.source === "none").length;
  const disabled = !canManage || busy !== null;

  return (
    <div className="space-y-6">
      {notice && (
        <div className={`rounded-lg px-4 py-2.5 text-sm ${notice.ok ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`} role="status">
          {notice.text}
        </div>
      )}
      {!canManage && <p className="rounded-lg bg-muted px-4 py-2.5 text-sm text-ink-2">{t("Только просмотр: менять регионы может пользователь с правом «Продажи: управление».")}</p>}

      <Section
        title={t("Регионы")}
        hint={t("{n} регионов · точек без региона: {m}", { n: num(data.regions.length), m: num(unresolved) })}
        actions={
          canManage && (
            <div className="flex gap-2">
              <button
                type="button"
                className={button}
                disabled={disabled}
                onClick={() => setDrafts((d) => [...d, { id: null, key: `new-${Date.now()}`, name: "", keywords: "" }])}
              >
                <Plus className="size-4" aria-hidden />
                {t("Регион")}
              </button>
              <button type="button" className={primary} disabled={disabled || !dirty} onClick={saveRegions}>
                {busy === "regions" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />}
                {t("Сохранить")}
              </button>
            </div>
          )
        }
      >
        <div className="divide-y divide-line">
          {drafts.map((d, i) => {
            const stats = data.regions.find((r) => r.id === d.id);
            return (
              <div key={d.key} className="grid gap-2 py-2.5 sm:grid-cols-[minmax(140px,220px)_1fr_auto] sm:items-center">
                <input
                  className={field}
                  value={d.name}
                  disabled={disabled}
                  aria-label={t("Название региона")}
                  placeholder={t("Название")}
                  onChange={(e) => setDrafts((all) => all.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))}
                />
                <input
                  className={field}
                  value={d.keywords}
                  disabled={disabled}
                  aria-label={t("Слова в названии точки")}
                  placeholder={t("Слова в названии точки через запятую: кулоб, куляб")}
                  onChange={(e) => setDrafts((all) => all.map((x, j) => (j === i ? { ...x, keywords: e.target.value } : x)))}
                />
                <div className="flex items-center justify-between gap-3 sm:justify-end">
                  <span className="whitespace-nowrap text-xs tabular-nums text-ink-3">{stats ? t("{n} точек", { n: num(stats.stores) }) : t("новый")}</span>
                  {canManage && (
                    <button
                      type="button"
                      className="rounded-md p-1.5 text-ink-3 hover:bg-bad-soft hover:text-bad disabled:opacity-50"
                      disabled={disabled}
                      aria-label={t("Удалить регион")}
                      title={t("Удалить регион")}
                      onClick={() => setDrafts((all) => all.filter((_, j) => j !== i))}
                    >
                      <Trash2 className="size-4" aria-hidden />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
        <Note>
          {t(
            "Слово ищется в названии точки целиком, без учёта регистра и таджикских букв (ҷ = ч, ӯ = у, ҳ = х, қ = к, ё = е). Если подходят несколько регионов — побеждает слово ближе к концу названия. Удалённый регион снимает и закрепления за ним.",
          )}
        </Note>
      </Section>

      <Section title={t("Регион ТП по умолчанию")} hint={t("для точек ТП, в названии которых нет слова региона")}>
        <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
          <table className="w-full min-w-max text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("ТП")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Точек")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Без региона")}</th>
                <th className="py-2 pl-3 font-semibold">{t("Регион по умолчанию")}</th>
              </tr>
            </thead>
            <tbody>
              {data.agents.map((a) => (
                <tr key={a.name} className="border-b border-line/60">
                  <td className="py-2 pr-3 font-medium text-ink">{a.name}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{num(a.stores)}</td>
                  <td className={`px-3 py-2 text-right tabular-nums ${a.unresolved > 0 ? "font-semibold text-warn" : "text-ink-3"}`}>{num(a.unresolved)}</td>
                  <td className="w-64 py-2 pl-3">
                    <select
                      className={select}
                      value={a.regionId ?? ""}
                      disabled={disabled}
                      aria-label={t("Регион ТП {name}", { name: a.name })}
                      onChange={(e) => assign("agent", { name: a.name }, e.target.value)}
                    >
                      <option value="">{t("— не задан —")}</option>
                      {data.regions.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={t("Точки")} hint={t("закреплённая вручную точка главнее слов в названии и региона ТП")}>
        <div className="flex flex-wrap items-center gap-3">
          <label className="relative min-w-[220px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 size-4 text-ink-3" aria-hidden />
            <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Поиск по точке или ТП")} aria-label={t("Поиск по точке или ТП")} />
          </label>
          <label className="inline-flex items-center gap-2 text-sm text-ink-2">
            <input type="checkbox" checked={onlyUnresolved} onChange={(e) => setOnlyUnresolved(e.target.checked)} />
            {t("Только без региона ({n})", { n: num(unresolved) })}
          </label>
        </div>
        <div className="-mx-4 mt-3 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
          <table className="w-full min-w-max text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Точка")}</th>
                <th className="px-3 py-2 font-semibold">{t("ТП")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Факт, кг")}</th>
                <th className="px-3 py-2 font-semibold">{t("Регион сейчас")}</th>
                <th className="py-2 pl-3 font-semibold">{t("Закрепить")}</th>
              </tr>
            </thead>
            <tbody>
              {stores.slice(0, 300).map((s) => (
                <tr key={s.id} className="border-b border-line/60">
                  <td className="max-w-[320px] py-2 pr-3 text-ink">{s.name}</td>
                  <td className="px-3 py-2 text-ink-2">{s.agent ?? "—"}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{kg(s.kg)}</td>
                  <td className="px-3 py-2">
                    <span className={s.source === "none" ? "text-warn" : "text-ink"}>{s.source === "none" ? t("не определён") : s.regionName}</span>
                    <span className="block text-xs text-ink-3">{t(regionSourceLabel[s.source])}</span>
                  </td>
                  <td className="w-56 py-2 pl-3">
                    <select
                      className={select}
                      value={s.manualRegionId ?? ""}
                      disabled={disabled}
                      aria-label={t("Регион точки {name}", { name: s.name })}
                      onChange={(e) => assign("store", { storeId: s.id }, e.target.value)}
                    >
                      <option value="">{t("— по правилам —")}</option>
                      {data.regions.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {stores.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{onlyUnresolved ? t("Все точки в регионах.") : t("Ничего не найдено.")}</p>}
          {stores.length > 300 && <p className="py-3 text-xs text-ink-3">{t("Показаны первые 300 из {n} — уточните поиск.", { n: num(stores.length) })}</p>}
        </div>
      </Section>
    </div>
  );
}
