"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Boxes,
  ChevronUp,
  CircleAlert,
  Factory,
  Handshake,
  Landmark,
  Layers,
  ListChecks,
  Loader2,
  Map as MapIcon,
  Megaphone,
  RefreshCw,
  RotateCcw,
  Rotate3d,
  Truck,
  UsersRound,
  Warehouse,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { departmentHref as navDepartmentHref } from "@/lib/navigation";
import { ago, enterpriseKpis, LAYERS, LEGEND, signalOf, TASKS_BAD_SHARE, type LayerKey, type TwinSnapshot } from "@/lib/twin";
import { blocks } from "@/lib/twin-site";
import type { SceneBuilding, SceneInsets } from "./TwinScene";
import { TwinPanel, toneDot, toneText } from "./TwinPanel";

const TwinScene = dynamic(() => import("./TwinScene"), {
  ssr: false,
  loading: () => (
    <div className="absolute inset-0 grid place-items-center">
      <Loader2 className="size-6 animate-spin text-ink-4" aria-hidden />
    </div>
  ),
});

/** Как часто обновлять показатели, пока вкладка видна. */
const REFRESH_MS = 60_000;

const layerIcons: Record<LayerKey, LucideIcon> = {
  all: Layers,
  production: Factory,
  warehouse: Warehouse,
  supply: Truck,
  finance: Landmark,
  hr: UsersRound,
  sales: Handshake,
  marketing: Megaphone,
  tasks: ListChecks,
};

/**
 * Цифровой двойник: 3D-модель территории на весь экран раздела, поверх неё — KPI предприятия, слои, легенда и управление видом, справа —
 * контекстная панель (на телефоне — шторка снизу). Данные — снимок GET /api/twin, обновляется раз в минуту при видимой вкладке и по кнопке;
 * камера и выбор при обновлении не сбрасываются. Слой и здание — в адресе (?layer=…&b=…), чтобы ссылкой можно было поделиться.
 */
export function TwinView({
  initial,
  permissions,
  initialLayer,
  initialKey,
}: {
  initial: TwinSnapshot | null;
  permissions: string[];
  initialLayer: LayerKey;
  initialKey: string | null;
}) {
  const t = useT();
  const [snapshot, setSnapshot] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(!initial);
  const [layer, setLayer] = useState<LayerKey>(initialLayer);
  const [selected, setSelected] = useState<string | null>(initialKey && blocks.some((b) => b.key === initialKey) ? initialKey : null);
  const [mode, setMode] = useState<"3d" | "plan">("3d");
  const [resetKey, setResetKey] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  const [mobile, setMobile] = useState(false);
  const [sheetOpen, setSheetOpen] = useState(!!initialKey);
  const [insets, setInsets] = useState<SceneInsets>({ top: 0, right: 0, bottom: 0 });
  const topRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const lastLoad = useRef(Date.now());

  const refresh = useCallback(async () => {
    setRefreshing(true);
    try {
      setSnapshot(await bff<TwinSnapshot>("twin"));
      setError(null);
      lastLoad.current = Date.now();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setRefreshing(false);
      setNow(Date.now());
    }
  }, []);

  // Автообновление: раз в минуту, пока вкладка видна; вернулись на вкладку после перерыва — сразу.
  useEffect(() => {
    const tick = () => document.visibilityState === "visible" && void refresh();
    const timer = window.setInterval(tick, REFRESH_MS);
    const onVisible = () => document.visibilityState === "visible" && Date.now() - lastLoad.current > REFRESH_MS && void refresh();
    document.addEventListener("visibilitychange", onVisible);
    const clock = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => {
      window.clearInterval(timer);
      window.clearInterval(clock);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [refresh]);

  // Не загрузилось с сервера при открытии — пробуем из браузера.
  useEffect(() => {
    if (!initial) void refresh();
  }, [initial, refresh]);

  useEffect(() => {
    const media = matchMedia("(max-width: 767px)");
    const apply = () => setMobile(media.matches);
    apply();
    media.addEventListener("change", apply);
    return () => media.removeEventListener("change", apply);
  }, []);

  // Слой и здание — в адресе, без перехода.
  useEffect(() => {
    const url = new URL(window.location.href);
    if (layer === "all") url.searchParams.delete("layer");
    else url.searchParams.set("layer", layer);
    if (selected) url.searchParams.set("b", selected);
    else url.searchParams.delete("b");
    window.history.replaceState(window.history.state, "", url);
  }, [layer, selected]);

  // Где свободно от панелей — туда сцена ставит центр вида.
  useEffect(() => {
    const measure = () => {
      const top = topRef.current?.getBoundingClientRect().height ?? 0;
      const panel = panelRef.current?.getBoundingClientRect();
      const controls = bottomRef.current?.getBoundingClientRect().height ?? 0;
      setInsets((prev) => {
        const next = { top: top + 16, right: mobile ? 0 : (panel?.width ?? 0) + 24, bottom: mobile ? (panel?.height ?? 0) + 12 : controls + 16 };
        return prev.top === next.top && prev.right === next.right && prev.bottom === next.bottom ? prev : next;
      });
    };
    measure();
    const observer = new ResizeObserver(measure);
    if (topRef.current) observer.observe(topRef.current);
    if (panelRef.current) observer.observe(panelRef.current);
    if (bottomRef.current) observer.observe(bottomRef.current);
    return () => observer.disconnect();
  }, [mobile]);

  const select = useCallback((key: string | null) => {
    setSelected(key);
    if (key) setSheetOpen(true);
  }, []);

  const departmentHref = useCallback((code: string) => navDepartmentHref(code, permissions), [permissions]);

  const buildings: SceneBuilding[] = useMemo(() => {
    if (!snapshot) return blocks.map((b) => ({ key: b.key, name: t("Корпус {key}", { key: b.key }), tone: "off", value: null }));
    return snapshot.objects.map((o) => {
      const s = signalOf(o, snapshot, layer, t);
      // На модели — только значение слоя; «Не привязано» видно в списке объектов, на модели оно шумит.
      return { key: o.key, name: o.name, tone: s.tone, value: s.tone === "off" ? null : s.value };
    });
  }, [snapshot, layer, t]);

  const kpis = useMemo(() => (snapshot ? enterpriseKpis(snapshot, t) : []), [snapshot, t]);
  const updated = snapshot ? ago(snapshot.generatedAt, now, t) : null;

  return (
    <div data-no-ptr className="relative h-full min-h-[480px] overflow-hidden">
      <TwinScene buildings={buildings} selected={selected} onSelect={select} mode={mode} resetKey={resetKey} insets={insets} />

      {/* Верх: заголовок, KPI предприятия, слои */}
      <div ref={topRef} className={`pointer-events-none absolute left-0 top-0 flex flex-col gap-2.5 p-3 sm:p-4 ${mobile ? "right-0" : "right-[404px]"}`}>
        <div className="pointer-events-auto flex flex-wrap items-center gap-x-3 gap-y-1">
          <h1 className="text-[19px] font-semibold tracking-[-0.02em] text-ink sm:text-[22px]">{t("Цифровой двойник")}</h1>
          <span className="inline-flex items-center gap-1.5 text-[12px] text-ink-3">
            <span className={`size-1.5 rounded-full ${error ? "bg-warn" : "bg-ok"} ${refreshing ? "animate-pulse" : ""}`} aria-hidden />
            {updated ? t("обновлено {when}", { when: updated }) : refreshing ? t("загружаем показатели…") : t("нет данных")}
          </span>
          <button
            type="button"
            onClick={() => void refresh()}
            disabled={refreshing}
            aria-label={t("Обновить")}
            title={t("Обновить")}
            className="grid size-7 cursor-pointer place-items-center rounded-full text-ink-3 transition-colors hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:cursor-default"
          >
            <RefreshCw className={`size-3.5 ${refreshing ? "animate-spin" : ""}`} aria-hidden />
          </button>
        </div>

        {error && (
          <div role="alert" className="pointer-events-auto flex max-w-xl items-start gap-2 rounded-[12px] border border-warn/30 bg-surface-solid/90 px-3 py-2 text-[12.5px] text-ink-2 backdrop-blur-xl">
            <CircleAlert className="mt-0.5 size-3.5 shrink-0 text-warn" aria-hidden />
            <span>{snapshot ? t("Не удалось обновить: {message}. Показаны последние данные.", { message: error }) : error}</span>
          </div>
        )}

        {kpis.length > 0 && (
          <div className="pointer-events-auto -mx-3 flex gap-2 overflow-x-auto px-3 pb-1 [scrollbar-width:none] sm:-mx-4 sm:px-4 [&::-webkit-scrollbar]:hidden">
            {kpis.map((k) => {
              const body = (
                <>
                  <span className="flex items-center gap-1.5 text-[11.5px] font-medium text-ink-3">
                    <span className={`size-1.5 shrink-0 rounded-full ${toneDot[k.tone]}`} aria-hidden />
                    <span className="truncate">{k.label}</span>
                  </span>
                  <span
                    className={`mt-1 truncate tabular-nums ${k.state === "ok" ? `text-[20px] font-semibold tracking-[-0.02em] ${k.tone === "info" || k.tone === "ok" ? "text-ink" : toneText[k.tone]}` : "text-[14px] font-medium leading-[30px] text-ink-4"}`}
                  >
                    {k.value}
                  </span>
                  {k.sub && <span className={`truncate text-[11px] ${k.tone === "bad" || k.tone === "warn" ? toneText[k.tone] : "text-ink-4"}`}>{k.sub}</span>}
                </>
              );
              const cls =
                "flex min-w-[148px] max-w-[230px] shrink-0 flex-col whitespace-nowrap rounded-[14px] border border-line bg-surface-solid/80 px-3 py-2.5 shadow-[0_10px_30px_-18px_rgb(0_0_0/0.6)] backdrop-blur-xl transition-colors";
              return k.href ? (
                <Link key={k.key} href={k.href} className={`${cls} hover:border-line-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent`}>
                  {body}
                </Link>
              ) : (
                <div key={k.key} className={cls}>
                  {body}
                </div>
              );
            })}
          </div>
        )}

        <div
          role="tablist"
          aria-label={t("Информационные слои")}
          className="pointer-events-auto -mx-3 flex gap-1 overflow-x-auto px-3 [scrollbar-width:none] sm:-mx-4 sm:px-4 [&::-webkit-scrollbar]:hidden"
        >
          <div className="flex shrink-0 gap-0.5 rounded-full border border-line bg-surface-solid/80 p-1 shadow-[0_10px_30px_-18px_rgb(0_0_0/0.6)] backdrop-blur-xl">
            {LAYERS.map((l) => {
              const Icon = layerIcons[l.key];
              const on = l.key === layer;
              return (
                <button
                  key={l.key}
                  type="button"
                  role="tab"
                  aria-selected={on}
                  onClick={() => setLayer(l.key)}
                  className={`inline-flex h-8 shrink-0 cursor-pointer items-center gap-1.5 rounded-full px-3 text-[12.5px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                    on ? "bg-accent text-white shadow-sm" : "text-ink-2 hover:bg-muted hover:text-ink"
                  }`}
                >
                  <Icon className="size-3.5" aria-hidden />
                  {t(l.label)}
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* Низ слева: вид и легенда */}
      <div
        ref={bottomRef}
        className={`pointer-events-none absolute left-3 flex flex-col items-start gap-2 sm:left-4 ${mobile ? "" : "bottom-4"}`}
        style={mobile ? { bottom: insets.bottom + 4 } : undefined}
      >
        <div className="pointer-events-auto flex items-center gap-1 rounded-full border border-line bg-surface-solid/80 p-1 shadow-[0_10px_30px_-18px_rgb(0_0_0/0.6)] backdrop-blur-xl">
          {(
            [
              ["3d", Rotate3d, "3D"],
              ["plan", MapIcon, "План сверху"],
            ] as const
          ).map(([m, Icon, label]) => (
            <button
              key={m}
              type="button"
              aria-pressed={mode === m}
              onClick={() => setMode(m)}
              className={`inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-full px-3 text-[12.5px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                mode === m ? "bg-accent text-white shadow-sm" : "text-ink-2 hover:bg-muted hover:text-ink"
              }`}
            >
              <Icon className="size-3.5" aria-hidden />
              {t(label)}
            </button>
          ))}
          <button
            type="button"
            onClick={() => {
              setMode("3d");
              setResetKey((k) => k + 1);
            }}
            aria-label={t("Сбросить вид")}
            title={t("Сбросить вид")}
            className="grid size-8 cursor-pointer place-items-center rounded-full text-ink-2 transition-colors hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <RotateCcw className="size-3.5" aria-hidden />
          </button>
        </div>
        {!mobile && (
          <div className="pointer-events-auto flex max-w-[calc(100vw-480px)] flex-wrap items-center gap-x-3 gap-y-1 rounded-[12px] border border-line bg-surface-solid/80 px-3 py-2 text-[11.5px] text-ink-3 shadow-[0_10px_30px_-18px_rgb(0_0_0/0.6)] backdrop-blur-xl">
            {LEGEND.map((l) => (
              <span key={l.tone} className="inline-flex items-center gap-1.5">
                <span className={`size-2 rounded-full ${toneDot[l.tone]}`} aria-hidden />
                {t(l.label)}
              </span>
            ))}
            <span className="text-ink-4" title={t("Просрочено от {pct}% открытых задач отдела — красный", { pct: TASKS_BAD_SHARE * 100 })}>
              {t("Задачи: красный — просрочено от {pct}%", { pct: TASKS_BAD_SHARE * 100 })}
            </span>
            <span className="hidden text-ink-4 xl:inline">{t("Вращение — левая кнопка · Сдвиг — правая · Масштаб — колесо")}</span>
          </div>
        )}
      </div>

      {/* Контекстная панель / шторка */}
      <aside
        ref={panelRef}
        aria-label={t("Показатели объекта")}
        className={
          mobile
            ? `absolute inset-x-2 bottom-2 flex flex-col overflow-hidden rounded-[20px] border border-line bg-surface-solid/92 shadow-[0_-12px_40px_-16px_rgb(0_0_0/0.55)] backdrop-blur-2xl transition-[max-height] duration-300 ${sheetOpen ? "max-h-[58%]" : "max-h-[64px]"}`
            : "absolute bottom-4 right-4 top-4 flex w-[380px] flex-col overflow-hidden rounded-[20px] border border-line bg-surface-solid/88 shadow-[0_24px_60px_-24px_rgb(0_0_0/0.6)] backdrop-blur-2xl"
        }
      >
        {mobile && (
          <button
            type="button"
            onClick={() => setSheetOpen((v) => !v)}
            aria-expanded={sheetOpen}
            className="flex h-[52px] shrink-0 cursor-pointer items-center gap-2 px-5 text-left focus-visible:outline-none"
          >
            <Boxes className="size-4 text-ink-3" aria-hidden />
            <span className="min-w-0 flex-1 truncate text-[14px] font-semibold text-ink">
              {selected ? (snapshot?.objects.find((o) => o.key === selected)?.name ?? selected) : t("Объекты территории")}
            </span>
            <ChevronUp className={`size-4 text-ink-3 transition-transform ${sheetOpen ? "rotate-180" : ""}`} aria-hidden />
          </button>
        )}
        {(!mobile || sheetOpen) && (
          <TwinPanel
            snapshot={snapshot}
            loading={!snapshot && refreshing}
            layer={layer}
            selected={selected}
            onSelect={select}
            departmentHref={departmentHref}
            onSaved={refresh}
          />
        )}
      </aside>
    </div>
  );
}
