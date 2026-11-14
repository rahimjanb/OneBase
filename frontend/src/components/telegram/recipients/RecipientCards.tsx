"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronDown, Lightbulb, MapPin, MessageCircle, MessageCircleOff, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { hasChat, matrix, nameOf, scopeProblem, type Group } from "@/lib/telegram-recipients";
import type { ReportJob, ReportRecipient } from "@/lib/telegramReports";
import { Avatar, LastSendView, ReportSquares, RolePill, Toggle } from "./bits";
import { subtitleOf, type RowAction } from "./RecipientsTable";

const FIRST = 6;
const HINT_KEY = "onebase:tg-reports:hint-select";

/** Долгое нажатие (0,5 с, без сдвига пальца) — выбор карточки; после него «клик» не открывает карточку. */
function useLongPress(onLong: () => void) {
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const start = useRef<{ x: number; y: number } | null>(null);
  const fired = useRef(false);
  const cancel = () => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
  };
  return {
    fired,
    handlers: {
      onPointerDown: (e: React.PointerEvent) => {
        fired.current = false;
        start.current = { x: e.clientX, y: e.clientY };
        cancel();
        timer.current = setTimeout(() => {
          fired.current = true;
          navigator.vibrate?.(15);
          onLong();
        }, 500);
      },
      onPointerMove: (e: React.PointerEvent) => {
        if (start.current && Math.hypot(e.clientX - start.current.x, e.clientY - start.current.y) > 10) cancel();
      },
      onPointerUp: cancel,
      onPointerLeave: cancel,
      onPointerCancel: cancel,
      onContextMenu: (e: React.MouseEvent) => {
        if (fired.current) e.preventDefault();
      },
    },
  };
}

function Card({
  r,
  jobs,
  regionNames,
  selecting,
  selected,
  busy,
  onOpen,
  onSelect,
  onToggle,
  onAction,
}: {
  r: ReportRecipient;
  jobs: ReportJob[];
  regionNames: string[];
  selecting: boolean;
  selected: boolean;
  busy: boolean;
  onOpen: () => void;
  onSelect: (on: boolean) => void;
  onToggle: (on: boolean) => void;
  onAction: (action: RowAction) => void;
}) {
  const t = useT();
  const press = useLongPress(() => onSelect(!selected));
  const chat = hasChat(r);
  const first = r.chats.find((c) => c.canSend);
  return (
    <article
      {...press.handlers}
      onClick={() => {
        if (press.fired.current) return;
        if (selecting) onSelect(!selected);
        else onOpen();
      }}
      aria-label={nameOf(r)}
      className={`cursor-pointer select-none rounded-[16px] border p-3 transition-colors ${
        selected ? "border-accent bg-accent/[0.07]" : chat ? "border-line bg-surface" : "border-warn/25 bg-surface"
      } ${r.enabled ? "" : "opacity-60"}`}
    >
      <div className="flex items-center gap-3">
        {selecting ? (
          <span
            aria-hidden
            className={`grid size-[38px] shrink-0 place-items-center rounded-full border-2 ${selected ? "border-accent bg-accent text-white" : "border-line-strong"}`}
          >
            {selected && "✓"}
          </span>
        ) : (
          <Avatar name={nameOf(r)} role={r.role} size={38} />
        )}
        <span className="min-w-0 flex-1">
          <span className="flex min-w-0 items-center gap-1.5">
            <span className="truncate text-[14px] font-semibold text-ink">{nameOf(r)}</span>
            <RolePill role={r.role} />
          </span>
          <span className="block truncate text-[11px] text-ink-4">{subtitleOf(r, regionNames, t)}</span>
          {scopeProblem(r) && (
            <span className="block truncate text-[11px] font-semibold text-warn" title={t(scopeProblem(r) as string)}>
              {t(scopeProblem(r) as string)}
            </span>
          )}
        </span>
        <Toggle checked={r.enabled} disabled={busy} size="lg" onChange={onToggle} label={r.enabled ? t("Выключить {name}", { name: nameOf(r) }) : t("Включить {name}", { name: nameOf(r) })} />
      </div>
      {chat ? (
        <div className="mt-2.5 flex items-center gap-2 rounded-[10px] bg-muted px-3 py-2 text-[12px]">
          <MessageCircle aria-hidden className="size-4 shrink-0 text-ink-3" />
          <span className="min-w-0 flex-1 truncate text-ink-2">
            {first?.title}
            {r.chats.length > 1 && <span className="text-ink-4"> +{r.chats.length - 1}</span>}
          </span>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onAction("chats");
            }}
            className="shrink-0 font-semibold text-accent-strong"
          >
            {t("изменить")}
          </button>
        </div>
      ) : (
        <div className="mt-2.5 flex items-center gap-2 rounded-[10px] bg-warn-soft px-3 py-2 text-[12px] font-semibold text-warn">
          <MessageCircleOff aria-hidden className="size-4 shrink-0" />
          <span className="min-w-0 flex-1">{t("Нет чата — отчёты пропускаются")}</span>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onAction("chats");
            }}
            className="shrink-0 text-accent-strong"
          >
            {t("привязать")}
          </button>
        </div>
      )}
      <div className="mt-2.5 flex items-center justify-between gap-2">
        <ReportSquares cells={matrix(r, jobs)} size="lg" />
        <span className="min-w-0 max-w-[45%]">
          <LastSendView r={r} jobs={jobs} compact />
        </span>
      </div>
    </article>
  );
}

/**
 * Телефон и планшет: группы-регионы карточками (планшет — в две колонки). Тап — правка, долгое нажатие — выбор нескольких (внизу — полоса
 * действий). Подсказка про выбор — пока его не попробовали.
 */
export function RecipientCards({
  groups,
  jobs,
  regionNamesOf,
  selected,
  onSelect,
  onOpen,
  onToggle,
  onAction,
  busy,
}: {
  groups: Group[];
  jobs: ReportJob[];
  regionNamesOf: (r: ReportRecipient) => string[];
  selected: Set<string>;
  onSelect: (ids: string[], on: boolean) => void;
  onOpen: (r: ReportRecipient) => void;
  onToggle: (r: ReportRecipient, on: boolean) => void;
  onAction: (r: ReportRecipient, action: RowAction) => void;
  busy: Set<string>;
}) {
  const t = useT();
  const [expanded, setExpanded] = useState<string[]>([]);
  const [hint, setHint] = useState(false);
  useEffect(() => {
    try {
      setHint(localStorage.getItem(HINT_KEY) !== "1");
    } catch {
      setHint(false);
    }
  }, []);
  const hideHint = () => {
    setHint(false);
    try {
      localStorage.setItem(HINT_KEY, "1");
    } catch {
      // без хранилища — подсказка вернётся при следующем открытии
    }
  };
  const selecting = selected.size > 0;
  useEffect(() => {
    if (selecting && hint) hideHint();
  });

  return (
    <div className="space-y-4">
      {groups.map((g) => {
        const more = expanded.includes(g.key) ? 0 : Math.max(0, g.rows.length - FIRST);
        const rows = more > 0 ? g.rows.slice(0, FIRST) : g.rows;
        const problems = [g.noChat > 0 && t("{n} без чата", { n: g.noChat }), g.disabled > 0 && t("{n} откл.", { n: g.disabled })].filter(Boolean).join(" · ");
        const counts = [g.counts.rm && `${g.counts.rm} ${t("РМ")}`, g.counts.sv && `${g.counts.sv} ${t("СВ")}`, g.counts.tp && `${g.counts.tp} ${t("ТП")}`].filter(Boolean).join(" · ");
        return (
          <section key={g.key} aria-label={t(g.name)}>
            <h3 className="mb-2 flex items-center gap-2 px-1">
              <MapPin aria-hidden className="size-4 text-ink-3" />
              <span className="text-[15px] font-bold text-ink">{t(g.name)}</span>
              <span className="truncate text-[12px] text-ink-4">{problems || counts}</span>
            </h3>
            <div className="grid gap-2.5 md:grid-cols-2">
              {rows.map((r) => (
                <Card
                  key={r.id}
                  r={r}
                  jobs={jobs}
                  regionNames={regionNamesOf(r)}
                  selecting={selecting}
                  selected={selected.has(r.id)}
                  busy={busy.has(r.id)}
                  onOpen={() => onOpen(r)}
                  onSelect={(on) => onSelect([r.id], on)}
                  onToggle={(on) => onToggle(r, on)}
                  onAction={(action) => onAction(r, action)}
                />
              ))}
            </div>
            {more > 0 && (
              <button type="button" onClick={() => setExpanded((l) => [...l, g.key])} className="mt-2 flex items-center gap-1 px-1 text-[13px] font-semibold text-accent-strong">
                <ChevronDown aria-hidden className="size-4" />
                {t("Ещё {n} — {region}", { n: more, region: t(g.name) })}
              </button>
            )}
          </section>
        );
      })}
      {hint && groups.length > 0 && (
        <div className="flex items-start gap-3 rounded-[16px] bg-accent-soft px-4 py-3 text-[13px] text-ink-2">
          <Lightbulb aria-hidden className="mt-0.5 size-5 shrink-0 text-accent-strong" />
          <span className="min-w-0 flex-1">{t("Выберите несколько карточек долгим нажатием — привяжите чат или отчёт сразу всем")}</span>
          <button type="button" onClick={hideHint} aria-label={t("Скрыть подсказку")} className="grid size-6 shrink-0 place-items-center rounded-md text-ink-3">
            <X className="size-4" />
          </button>
        </div>
      )}
    </div>
  );
}
