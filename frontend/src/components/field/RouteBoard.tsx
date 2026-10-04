"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, GripVertical, ListChecks, Map as MapIcon, Navigation, Plus, RefreshCw, Route as RouteIcon, Shuffle, SkipForward, Trash2 } from "lucide-react";
import { pointStatusLabel, priorityLabel, routeSourceLabel } from "@/lib/field/labels";
import type { FieldCustomerRow, FieldRoute, FieldRoutePoint, FieldVisitRow, Page } from "@/lib/field/types";
import { Chip, Empty, buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { fieldApi, getPosition, useAction } from "./hooks";
import { VisitPanel } from "./VisitPanel";

const statusDot: Record<string, string> = {
  Planned: "bg-surface text-ink ring-2 ring-line",
  InProgress: "bg-accent text-white",
  Visited: "bg-ok text-white",
  Skipped: "bg-bad text-white",
  Cancelled: "bg-muted text-ink-3",
};

const navUrl = (p: FieldRoutePoint) => (p.lat && p.lon ? `https://yandex.ru/maps/?rtext=~${p.lat},${p.lon}&rtt=auto` : null);

/**
 * Маршрут дня. Агент: следующая точка, визит, пропуск, навигатор. Супервайзер/РМ: сформировать, оптимизировать,
 * переставить (перетаскиванием или стрелками), добавить и убрать точки.
 */
export function RouteBoard({
  route,
  agentId,
  agentName,
  date,
  isOwn,
  canPlan,
  active,
  geoRadiusM,
}: {
  route: FieldRoute | null;
  agentId: string;
  agentName: string;
  date: string;
  isOwn: boolean;
  canPlan: boolean;
  active: FieldVisitRow | null;
  geoRadiusM: number;
}) {
  const { busy, error, run } = useAction();
  const [edit, setEdit] = useState(false);
  const [order, setOrder] = useState<FieldRoutePoint[]>([]);
  const [drag, setDrag] = useState<number | null>(null);
  const [skip, setSkip] = useState<FieldRoutePoint | null>(null);
  const [skipNote, setSkipNote] = useState("");
  const [visitPoint, setVisitPoint] = useState<FieldRoutePoint | null>(null);
  const [adding, setAdding] = useState(false);

  const locked = useMemo(() => (route?.points ?? []).filter((p) => p.status !== "Planned"), [route]);

  const build = () =>
    run("build", async () => {
      const pos = isOwn ? await getPosition(6000) : null;
      await fieldApi.post("routes/build", { agentId, date, latitude: pos?.latitude ?? null, longitude: pos?.longitude ?? null });
    });

  if (!route) {
    return (
      <Empty
        action={
          (canPlan || isOwn) && (
            <button type="button" onClick={build} disabled={busy !== null} className={buttonClass.primary}>
              <RouteIcon className="size-4" /> {busy === "build" ? "Строим…" : "Сформировать маршрут"}
            </button>
          )
        }
      >
        Маршрута на этот день нет.
        {(canPlan || isOwn) && " Его можно собрать из задач, плана визитов Linko и точек, которые пора посетить."}
        {error && <p className="mt-3 text-bad">{error}</p>}
      </Empty>
    );
  }

  const startEdit = () => {
    setOrder(route.points.filter((p) => p.status === "Planned"));
    setEdit(true);
  };

  const move = (from: number, to: number) => {
    if (to < 0 || to >= order.length || from === to) return;
    const next = [...order];
    const [item] = next.splice(from, 1);
    next.splice(to, 0, item);
    setOrder(next);
  };

  const save = () =>
    run("save", () => fieldApi.put(`routes/${route.id}/points`, { marketIds: order.map((p) => p.marketId) }), { onDone: () => setEdit(false) });

  const share = route.planned > 0 ? route.visited / route.planned : 0;
  const next = route.points.find((p) => p.id === route.nextPointId);

  return (
    <div className="space-y-3">
      <div className="rounded-xl border border-line bg-surface p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="text-sm text-ink-2">
              {agentName} · {routeSourceLabel[route.source]}
            </div>
            <div className="mt-1 text-2xl font-semibold tabular-nums text-ink">
              {route.visited}
              <span className="text-base font-normal text-ink-3"> / {route.planned} точек</span>
            </div>
          </div>
          <div className="text-right text-sm text-ink-2">
            <div>{route.distanceKm.toLocaleString("ru-RU")} км</div>
            <div>
              ≈ {Math.floor(route.estimatedMinutes / 60)} ч {route.estimatedMinutes % 60} мин
            </div>
          </div>
        </div>
        <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted">
          <div className="h-full rounded-full bg-ok" style={{ width: `${Math.round(share * 100)}%` }} />
        </div>
        <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
          <span>осталось {route.remaining}</span>
          {route.skipped > 0 && <span className="text-bad">пропущено {route.skipped}</span>}
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          <Link href={`/field/map?agent=${agentId}&date=${date}`} className={buttonClass.outline}>
            <MapIcon className="size-4" /> На карте
          </Link>
          {(canPlan || isOwn) && !edit && (
            <button
              type="button"
              disabled={busy !== null}
              onClick={() =>
                run("opt", async () => {
                  const pos = isOwn ? await getPosition(6000) : null;
                  await fieldApi.post(`routes/${route.id}/optimize`, { latitude: pos?.latitude ?? null, longitude: pos?.longitude ?? null });
                })
              }
              className={buttonClass.outline}
            >
              <Shuffle className="size-4" /> {busy === "opt" ? "Считаем…" : "Оптимизировать"}
            </button>
          )}
          {canPlan && !edit && (
            <>
              <button type="button" onClick={startEdit} className={buttonClass.outline}>
                <ListChecks className="size-4" /> Изменить
              </button>
              <button
                type="button"
                disabled={busy !== null}
                onClick={() => {
                  if (confirm("Пересобрать маршрут? Непосещённые точки заменятся новым подбором.")) build();
                }}
                className={buttonClass.ghost}
              >
                <RefreshCw className="size-4" /> Пересобрать
              </button>
            </>
          )}
        </div>
        {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      </div>

      {isOwn && next && !edit && (
        <div className="rounded-xl border-2 border-accent/40 bg-surface p-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-accent-strong">Следующая точка</div>
          <div className="mt-1 text-lg font-semibold text-ink">{next.name}</div>
          {next.address && <div className="text-sm text-ink-2">{next.address}</div>}
          <div className="mt-3 grid grid-cols-2 gap-2">
            <button type="button" onClick={() => setVisitPoint(next)} className={`${buttonClass.primary} h-12`}>
              Начать визит
            </button>
            {navUrl(next) ? (
              <a href={navUrl(next)!} target="_blank" rel="noreferrer" className={`${buttonClass.outline} h-12`}>
                <Navigation className="size-4" /> Навигатор
              </a>
            ) : (
              <Link href={`/field/customers/${next.marketId}`} className={`${buttonClass.outline} h-12`}>
                Карточка
              </Link>
            )}
          </div>
        </div>
      )}

      {edit ? (
        <div className="rounded-xl border border-line bg-surface">
          <div className="flex items-center justify-between border-b border-line px-4 py-3">
            <div className="text-sm font-medium text-ink">Порядок непосещённых точек · перетащите или стрелками</div>
            <button type="button" onClick={() => setAdding(true)} className={buttonClass.outline}>
              <Plus className="size-4" /> Точка
            </button>
          </div>
          {locked.length > 0 && <div className="border-b border-line px-4 py-2 text-xs text-ink-3">Посещённые и начатые ({locked.length}) остаются на своих местах.</div>}
          <ol>
            {order.map((p, i) => (
              <li
                key={p.marketId}
                draggable
                onDragStart={() => setDrag(i)}
                onDragOver={(e) => e.preventDefault()}
                onDrop={() => {
                  if (drag !== null) move(drag, i);
                  setDrag(null);
                }}
                className={`flex items-center gap-2 border-b border-line px-3 py-2 last:border-0 ${drag === i ? "opacity-50" : ""}`}
              >
                <GripVertical className="size-4 shrink-0 cursor-grab text-ink-3 max-lg:hidden" />
                <span className="w-6 shrink-0 text-center text-xs tabular-nums text-ink-3">{locked.length + i + 1}</span>
                <span className="min-w-0 flex-1 truncate text-sm text-ink">{p.name}</span>
                <button type="button" aria-label="Выше" onClick={() => move(i, i - 1)} className="grid size-9 place-items-center rounded-lg text-ink-2 hover:bg-muted">
                  <ArrowUp className="size-4" />
                </button>
                <button type="button" aria-label="Ниже" onClick={() => move(i, i + 1)} className="grid size-9 place-items-center rounded-lg text-ink-2 hover:bg-muted">
                  <ArrowDown className="size-4" />
                </button>
                <button type="button" aria-label="Убрать" onClick={() => setOrder(order.filter((_, j) => j !== i))} className="grid size-9 place-items-center rounded-lg text-bad hover:bg-bad-soft">
                  <Trash2 className="size-4" />
                </button>
              </li>
            ))}
          </ol>
          <div className="flex gap-2 border-t border-line p-3">
            <button type="button" onClick={save} disabled={busy !== null} className={`${buttonClass.primary} flex-1`}>
              {busy === "save" ? "Сохраняем…" : "Сохранить маршрут"}
            </button>
            <button type="button" onClick={() => setEdit(false)} className={buttonClass.outline}>
              Отмена
            </button>
          </div>
        </div>
      ) : (
        <ol className="overflow-hidden rounded-xl border border-line bg-surface">
          {route.points.map((p) => {
            const [label, tone] = pointStatusLabel[p.status];
            return (
              <li key={p.id} className="flex items-start gap-3 border-b border-line px-4 py-3 last:border-0">
                <span className={`mt-0.5 grid size-7 shrink-0 place-items-center rounded-full text-xs font-semibold tabular-nums ${statusDot[p.status]}`}>{p.sequence}</span>
                <div className="min-w-0 flex-1">
                  <Link href={`/field/customers/${p.marketId}`} prefetch={false} className="block truncate text-sm font-medium text-ink hover:underline">
                    {p.name}
                  </Link>
                  <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-3">
                    {p.plannedTime && p.status === "Planned" && <span>≈ {p.plannedTime.slice(0, 5)}</span>}
                    {p.arrival && <span>прибыл {new Date(p.arrival).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })}</span>}
                    {p.address && <span className="truncate">{p.address}</span>}
                  </div>
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    <Chip tone={tone}>{p.linkoDone && p.status === "Visited" && !p.visitId ? "Посещена (Linko)" : label}</Chip>
                    {p.priority !== "Medium" && <Chip tone={priorityLabel[p.priority][1]}>{priorityLabel[p.priority][0]}</Chip>}
                    {p.openTasks > 0 && <Chip tone="accent">задач: {p.openTasks}</Chip>}
                    {p.note && <span className="text-xs text-ink-3">«{p.note}»</span>}
                  </div>
                </div>
                {isOwn && (p.status === "Planned" || p.status === "InProgress") && (
                  <div className="flex shrink-0 flex-col gap-1.5">
                    <button type="button" onClick={() => setVisitPoint(p)} className="rounded-lg bg-accent px-3 py-2 text-xs font-semibold text-white">
                      {p.status === "InProgress" ? "Визит" : "Начать"}
                    </button>
                    {p.status === "Planned" && (
                      <button type="button" onClick={() => setSkip(p)} className="rounded-lg border border-line px-3 py-2 text-xs text-ink-2" aria-label="Пропустить точку">
                        <SkipForward className="mx-auto size-3.5" />
                      </button>
                    )}
                  </div>
                )}
                {!isOwn && canPlan && p.status === "Planned" && (
                  <button type="button" onClick={() => setSkip(p)} className="shrink-0 rounded-lg border border-line px-2.5 py-1.5 text-xs text-ink-2 hover:bg-muted">
                    Пропустить
                  </button>
                )}
              </li>
            );
          })}
        </ol>
      )}

      <Sheet open={visitPoint !== null} onClose={() => setVisitPoint(null)} title={visitPoint?.name ?? "Визит"}>
        {visitPoint && (
          <div className="space-y-3">
            {visitPoint.address && <p className="text-sm text-ink-2">{visitPoint.address}</p>}
            <VisitPanel marketId={visitPoint.marketId} marketName={visitPoint.name} routePointId={visitPoint.id} active={active} canStart geoRadiusM={geoRadiusM} onFinished={() => setVisitPoint(null)} />
            <Link href={`/field/customers/${visitPoint.marketId}`} className="block text-center text-sm text-accent-strong underline">
              Карточка точки, история и задачи
            </Link>
          </div>
        )}
      </Sheet>

      <Sheet
        open={skip !== null}
        onClose={() => setSkip(null)}
        title="Пропустить точку"
        footer={
          <button
            type="button"
            disabled={busy !== null}
            onClick={() => skip && run("skip", () => fieldApi.post(`routes/points/${skip.id}/skip`, { note: skipNote || null }), { onDone: () => setSkip(null) })}
            className={`${buttonClass.danger} w-full`}
          >
            {busy === "skip" ? "Сохраняем…" : "Пропустить"}
          </button>
        }
      >
        <p className="text-sm text-ink-2">«{skip?.name}» — точка останется в маршруте отмеченной, AI предложит повторный визит.</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {["Закрыто", "Нет ЛПР", "Не успел", "Нет товара", "Отказались принимать"].map((r) => (
            <button key={r} type="button" onClick={() => setSkipNote(r)} className={`rounded-full px-3 py-2 text-sm ${skipNote === r ? "bg-ink text-surface" : "bg-muted text-ink-2"}`}>
              {r}
            </button>
          ))}
        </div>
        <input value={skipNote} onChange={(e) => setSkipNote(e.target.value)} placeholder="или своя причина" className={`${inputClass} mt-3`} />
      </Sheet>

      <AddPointSheet
        open={adding}
        agentId={agentId}
        exclude={new Set([...order.map((p) => p.marketId), ...locked.map((p) => p.marketId)])}
        onClose={() => setAdding(false)}
        onPick={(c) => {
          setOrder([...order, { id: `new-${c.marketId}`, sequence: 0, marketId: c.marketId, name: c.name, address: c.address, lat: c.lat, lon: c.lon, plannedTime: null, status: "Planned", linkoDone: false, visitId: null, arrival: null, departure: null, note: null, priority: c.priority, openTasks: 0 }]);
          setAdding(false);
        }}
      />
    </div>
  );
}

/** Поиск точки агента для добавления в маршрут. */
function AddPointSheet({ open, agentId, exclude, onClose, onPick }: { open: boolean; agentId: string; exclude: Set<number>; onClose: () => void; onPick: (c: FieldCustomerRow) => void }) {
  const [text, setText] = useState("");
  const [items, setItems] = useState<FieldCustomerRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const search = async (q: string) => {
    setError(null);
    try {
      const page = await fieldApi.get<Page<FieldCustomerRow>>(`customers?agentId=${agentId}&pageSize=30&search=${encodeURIComponent(q)}`);
      setItems(page.items);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };
  return (
    <Sheet open={open} onClose={onClose} title="Добавить точку">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          search(text);
        }}
        className="flex gap-2"
      >
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Название, адрес или id" className={inputClass} enterKeyHint="search" autoFocus />
        <button type="submit" className={buttonClass.outline}>
          Найти
        </button>
      </form>
      {error && <p className="mt-2 text-sm text-bad">{error}</p>}
      <ul className="mt-3 divide-y divide-line">
        {(items ?? []).filter((c) => !exclude.has(c.marketId)).map((c) => (
          <li key={c.marketId}>
            <button type="button" onClick={() => onPick(c)} className="flex w-full items-center gap-3 py-3 text-left">
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium text-ink">{c.name}</span>
                <span className="block truncate text-xs text-ink-3">{c.address ?? `#${c.marketId}`}</span>
              </span>
              <Plus className="size-4 text-accent-strong" />
            </button>
          </li>
        ))}
        {items && items.length === 0 && <li className="py-6 text-center text-sm text-ink-3">Ничего не найдено среди точек агента.</li>}
      </ul>
    </Sheet>
  );
}
