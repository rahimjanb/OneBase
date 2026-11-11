"use client";

import { useEffect, useMemo, useState } from "react";
import { MapPinned } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";

/** Роли раздела «Продажи» по своим регионам (как SystemRoles.SalesRm / SalesSupervisor на сервере). */
export const SALES_REGION_ROLES = { rm: "РМ", supervisor: "Супервайзер продаж" } as const;
export const isSalesRegionRole = (roleName: string | null | undefined) => roleName === SALES_REGION_ROLES.rm || roleName === SALES_REGION_ROLES.supervisor;

type Option = { id: string; name: string; hint: string | null };

/**
 * Регионы РМ / супервайзера продаж в окне пользователя (обязательно): в разделе «Продажи» он видит только их данные — регионы, ТП,
 * магазины, продажи, планы, KPI. Регионы — филиалы Linko из «Настройки → Продажи: оргструктура» (подсказка — направление). Сохраняются вместе
 * с пользователем; у существующего подставляются текущие.
 */
export function SalesRegionsPicker({ roleName, userId, value, onChange }: { roleName: string; userId: string | null; value: string[] | null; onChange: (ids: string[]) => void }) {
  const t = useT();
  const [regions, setRegions] = useState<Option[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");

  useEffect(() => {
    let live = true;
    bff<{ regions: Option[] }>("users/sales-scope/options")
      .then((o) => live && setRegions(o.regions))
      .catch((e: Error) => live && setError(e.message));
    return () => {
      live = false;
    };
  }, []);

  // Текущие регионы пользователя — один раз при открытии (value = null — ещё не загружены).
  useEffect(() => {
    if (value !== null) return;
    if (!userId) {
      onChange([]);
      return;
    }
    let live = true;
    bff<{ level: string | null; regionIds: string[] }>(`users/${userId}/sales-scope`)
      .then((v) => live && onChange(v.level === "Rm" || v.level === "Supervisor" ? v.regionIds : []))
      .catch(() => live && onChange([]));
    return () => {
      live = false;
    };
  }, [userId, value, onChange]);

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return (regions ?? []).filter((r) => !q || r.name.toLowerCase().includes(q) || (r.hint ?? "").toLowerCase().includes(q));
  }, [regions, query]);

  if (error) return <p className="text-sm text-bad">{t(error)}</p>;
  const chosen = value ?? [];
  const rm = roleName === SALES_REGION_ROLES.rm;

  return (
    <fieldset className="space-y-2 rounded-xl border border-line p-3">
      <legend className="flex items-center gap-1.5 px-1 text-sm font-medium text-ink">
        <MapPinned className="size-4 text-ink-3" aria-hidden />
        {t(rm ? "Регионы РМ" : "Регионы супервайзера")} <span className="text-bad">*</span>
      </legend>
      <p className="text-xs text-ink-3">
        {t(rm
          ? "В разделе «Продажи» РМ видит только эти регионы: их СВР, ТП, магазины, продажи, планы и KPI. Страница направления открыта, если у РМ все его регионы."
          : "В разделе «Продажи» супервайзер видит только эти регионы: их ТП, магазины, продажи, планы и KPI.")}
      </p>
      {regions === null || value === null ? (
        <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
      ) : regions.length === 0 ? (
        <p className="text-sm text-warn">{t("Регионов нет — они появляются после синхронизации с Linko («Настройки → Продажи: оргструктура»).")}</p>
      ) : (
        <>
          {regions.length > 8 && (
            <input
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t("Найти регион")}
              aria-label={t("Найти регион")}
              className="h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink outline-none focus:border-accent"
            />
          )}
          <div className="grid max-h-60 gap-1.5 overflow-y-auto sm:grid-cols-2">
            {shown.map((r) => (
              <label key={r.id} className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={chosen.includes(r.id)} onChange={(e) => onChange(e.target.checked ? [...chosen, r.id] : chosen.filter((x) => x !== r.id))} />
                <span className="truncate">{r.name}</span>
                {r.hint && <span className="truncate text-xs text-ink-3">· {r.hint}</span>}
              </label>
            ))}
          </div>
          <p className="text-xs text-ink-3">{t("Выбрано: {n}", { n: chosen.length })}</p>
        </>
      )}
    </fieldset>
  );
}
