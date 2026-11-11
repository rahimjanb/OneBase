"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import {
  Briefcase,
  Building,
  ChevronDown,
  ChevronRight,
  CircleCheckBig,
  CirclePause,
  CirclePlay,
  Ellipsis,
  FilePen,
  Layers,
  Plus,
  RotateCw,
  Search,
  SlidersHorizontal,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { refreshRecruitingNav } from "@/components/recruitment/RecruitingNav";
import { emptyInput, VacancyForm } from "@/components/recruitment/Vacancies";
import { useT } from "@/i18n/client";
import { recruitmentApi, vacancyStatusLabel, type RecruitmentContext, type VacancyRow, type VacancyStatus } from "@/lib/recruitment";
import {
  avatarTone,
  hiredProgress,
  initials,
  matchesQuery,
  matchesStatus,
  parseStatus,
  salaryFull,
  salaryShort,
  shortName,
  sortVacancies,
  statusCounts,
  type AvatarTone,
  type StatusFilter,
} from "@/lib/recruitment-view";

/*
 * HR → «Подбор» → «Вакансии» (ТЗ prompt-hr-podbor-vakansii.md, разделы 4–8; макеты AItTc и xH9vJ). Данные — прежний список вакансий;
 * поиск, статус и отдел применяются на месте и пишутся в адрес (history.replaceState — без перезагрузки страницы). Таблица — когда
 * области содержимого хватает ширины (от 1000 px; от 1100 — рекрутер с именем), уже — карточки (по две в ряд от 600 px). Цвета — токены
 * палитры макета.
 */

const NEW_VACANCY = "recruitment:new-vacancy";

const STATUS_PILL: Record<VacancyStatus, string> = {
  Open: "bg-ok-soft text-ok",
  Paused: "bg-warn-soft text-warn",
  Closed: "bg-muted text-ink-3",
  Draft: "bg-muted text-ink-4",
};

const AVATAR: Record<AvatarTone, string> = {
  violet: "bg-violet-soft text-violet",
  ok: "bg-ok-soft text-ok",
  accent: "bg-accent-soft text-accent-strong",
  warn: "bg-warn-soft text-warn",
  pink: "bg-pink/15 text-pink",
  cyan: "bg-cyan/15 text-cyan",
};

type FilterItem = { key: StatusFilter; label: string; icon: LucideIcon; tone: string; dot: string };

const FILTERS: FilterItem[] = [
  { key: "all", label: "Все", icon: Layers, tone: "text-accent-strong", dot: "" },
  { key: "active", label: "Активные", icon: CirclePlay, tone: "text-ok", dot: "bg-ok" },
  { key: "pause", label: "Пауза", icon: CirclePause, tone: "text-warn", dot: "bg-warn" },
  { key: "closed", label: "Закрытые", icon: CircleCheckBig, tone: "text-ink-3", dot: "bg-ink-3" },
  { key: "draft", label: "Черновики", icon: FilePen, tone: "text-ink-4", dot: "bg-ink-4" },
];

const candidatesOf = (id: string) => `/hr/recruitment/candidates?vacancyId=${id}`;
const vacancyHref = (id: string) => `/hr/recruitment/vacancies/${id}`;

/** «Новая вакансия»: на компьютере — кнопка в строке фильтров, на телефоне — круглая «+» в шапке. Открывает форму в шторке. */
export function NewVacancyButton({ round = false }: { round?: boolean }) {
  const t = useT();
  const open = () => window.dispatchEvent(new Event(NEW_VACANCY));
  if (round) {
    return (
      <button
        type="button"
        onClick={open}
        aria-label={t("Новая вакансия")}
        className="grid size-10 shrink-0 place-items-center rounded-full bg-accent outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-page active:brightness-90"
      >
        <Plus aria-hidden className="size-5 text-white" />
      </button>
    );
  }
  return (
    <button
      type="button"
      onClick={open}
      className="flex h-10 shrink-0 items-center gap-2 rounded-[10px] bg-accent px-4 shadow-[0_4px_14px_#3D7BFF55] outline-none hover:brightness-110 focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-page"
    >
      <Plus aria-hidden className="size-4 text-white" />
      <span className="text-[13px] font-semibold text-white">{t("Новая вакансия")}</span>
    </button>
  );
}

function StatusPill({ status }: { status: VacancyStatus }) {
  const t = useT();
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[7px] px-2 py-[3px] text-[11px] font-semibold ${STATUS_PILL[status]}`}>
      <span aria-hidden className="size-1.5 rounded-full bg-current" />
      {t(vacancyStatusLabel[status])}
    </span>
  );
}

function Avatar({ name, size }: { name: string; size: 22 | 26 }) {
  return (
    <span
      aria-hidden
      className={`grid shrink-0 place-items-center rounded-full font-bold ${AVATAR[avatarTone(name)]} ${size === 26 ? "size-[26px] text-[10px]" : "size-[22px] text-[9px]"}`}
    >
      {initials(name)}
    </span>
  );
}

function Progress({ hired, planned }: { hired: number; planned: number }) {
  const { pct, done } = hiredProgress(hired, planned);
  return (
    <span aria-hidden className="mt-1.5 block h-1 overflow-hidden rounded-full bg-muted">
      <span className={`block h-full rounded-full ${done ? "bg-ok" : "bg-accent"}`} style={{ width: `${pct}%` }} />
    </span>
  );
}

/** Строка под названием: «V4 · Ташкент, Чиланзар · hh.uz · Telegram». */
const metaLine = (v: VacancyRow) => [v.code, v.city, v.onHh ? "hh.uz" : null, v.inTelegram ? "Telegram" : null].filter(Boolean).join(" · ");

// ---------- Фильтр статуса ----------

/** Кнопки статуса (radiogroup «Статус вакансии»): стрелки ← → двигают выбор. «Черновики» — только когда они есть. */
function StatusFilters({ value, counts, onChange, mobile = false }: { value: StatusFilter; counts: Record<StatusFilter, number>; onChange: (s: StatusFilter) => void; mobile?: boolean }) {
  const t = useT();
  const items = FILTERS.filter((f) => f.key !== "draft" || counts.draft > 0 || value === "draft");
  const onKey = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
    e.preventDefault();
    const i = Math.max(0, items.findIndex((f) => f.key === value));
    const next = items[(i + (e.key === "ArrowRight" ? 1 : items.length - 1)) % items.length];
    onChange(next.key);
    e.currentTarget.querySelector<HTMLElement>(`[data-status="${next.key}"]`)?.focus();
  };
  return (
    <div
      role="radiogroup"
      aria-label={t("Статус вакансии")}
      onKeyDown={onKey}
      className={mobile ? "no-scrollbar -mx-4 flex gap-[5px] overflow-x-auto px-4" : "flex shrink-0 gap-1 rounded-xl border border-line bg-surface p-1"}
    >
      {items.map((f) => {
        const on = f.key === value;
        const Icon = f.icon;
        return (
          <button
            key={f.key}
            type="button"
            role="radio"
            aria-checked={on}
            data-status={f.key}
            tabIndex={on ? 0 : -1}
            onClick={() => onChange(f.key)}
            className={`flex shrink-0 items-center whitespace-nowrap outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              mobile
                ? `gap-1.5 rounded-[10px] border px-2 py-[7px] text-[12px] font-semibold ${on ? "border-transparent bg-accent" : "border-line bg-surface"}`
                : `gap-1.5 rounded-[9px] px-[11px] py-[7px] text-[13px] ${on ? "bg-accent font-semibold" : "font-medium hover:bg-muted"}`
            }`}
          >
            {mobile
              ? f.dot && <span aria-hidden className={`size-1.5 rounded-full ${on ? "bg-white" : f.dot}`} />
              : <Icon aria-hidden className={`size-3.5 ${on ? "text-white" : f.tone}`} />}
            <span className={on ? "text-white" : mobile ? "text-ink" : "text-ink-2"}>{t(f.label)}</span>
            <span className={`text-[11px] font-semibold tabular-nums ${on ? "text-white/70" : "text-ink-4"}`}>{counts[f.key]}</span>
          </button>
        );
      })}
    </div>
  );
}

// ---------- Меню строки ----------

type MenuAt = { id: string; x: number; y: number; up: boolean };

function RowMenu({ at, row, onClose, onStatus }: { at: MenuAt; row: VacancyRow; onClose: () => void; onStatus: (v: VacancyRow, s: VacancyStatus) => void }) {
  const t = useT();
  const router = useRouter();
  const panel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    panel.current?.querySelector<HTMLElement>("[role=menuitem]")?.focus();
    const away = (e: Event) => !panel.current?.contains(e.target as Node) && onClose();
    const key = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("pointerdown", away, true);
    document.addEventListener("keydown", key);
    document.addEventListener("scroll", onClose, true);
    window.addEventListener("resize", onClose);
    return () => {
      document.removeEventListener("pointerdown", away, true);
      document.removeEventListener("keydown", key);
      document.removeEventListener("scroll", onClose, true);
      window.removeEventListener("resize", onClose);
    };
  }, [onClose]);

  const items: { label: string; run: () => void; tone?: string }[] = [
    { label: "Открыть", run: () => router.push(vacancyHref(row.id)) },
    { label: "Редактировать", run: () => router.push(`${vacancyHref(row.id)}?edit=1`) },
    ...(row.status === "Open" ? [{ label: "Поставить на паузу", run: () => onStatus(row, "Paused") }] : []),
    ...(row.status === "Paused" ? [{ label: "Возобновить", run: () => onStatus(row, "Open") }] : []),
    ...(row.status === "Draft" ? [{ label: "Сделать активной", run: () => onStatus(row, "Open") }] : []),
    ...(row.status === "Closed" ? [{ label: "Открыть снова", run: () => onStatus(row, "Open") }] : []),
    ...(row.status !== "Closed" ? [{ label: "Закрыть", run: () => onStatus(row, "Closed"), tone: "text-bad" }] : []),
    ...(!row.onHh && row.status !== "Closed" ? [{ label: "Опубликовать на hh.uz", run: () => router.push("/hr/recruitment/hh") }] : []),
  ];
  const onKey = (e: React.KeyboardEvent) => {
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") return;
    e.preventDefault();
    const list = [...(panel.current?.querySelectorAll<HTMLElement>("[role=menuitem]") ?? [])];
    const i = list.indexOf(document.activeElement as HTMLElement);
    list[(i + (e.key === "ArrowDown" ? 1 : list.length - 1)) % list.length]?.focus();
  };
  return (
    <div
      ref={panel}
      role="menu"
      aria-label={t("Действия с вакансией")}
      onKeyDown={onKey}
      style={{ right: Math.max(8, at.x), ...(at.up ? { bottom: at.y } : { top: at.y }) }}
      className="fixed z-50 w-60 rounded-xl border border-line-strong bg-surface-solid p-1 shadow-lg"
    >
      {items.map((item) => (
        <button
          key={item.label}
          type="button"
          role="menuitem"
          onClick={() => {
            onClose();
            item.run();
          }}
          className={`block w-full rounded-lg px-3 py-2 text-left text-[13px] font-medium outline-none hover:bg-muted focus-visible:bg-muted ${item.tone ?? "text-ink"}`}
        >
          {t(item.label)}
        </button>
      ))}
    </div>
  );
}

/** Место меню: под точкой (кнопкой «…» или пальцем), у нижнего края окна — над ней. */
function menuAt(id: string, x: number, top: number, bottom: number): MenuAt {
  const up = bottom + 300 > window.innerHeight && top > 300;
  return { id, x: window.innerWidth - x, y: up ? window.innerHeight - top + 4 : bottom + 4, up };
}

// ---------- Таблица (компьютер) ----------

function VacancyTable({ rows, canManage, onMenu }: { rows: VacancyRow[]; canManage: boolean; onMenu: (m: MenuAt) => void }) {
  const t = useT();
  const router = useRouter();
  const th = "px-2 py-2.5 text-left text-[11px] font-semibold uppercase tracking-[0.3px] text-ink-4";
  return (
    <div className="overflow-hidden rounded-2xl border border-line bg-surface">
      <table className="w-full table-fixed border-collapse">
        <colgroup>
          <col />
          <col className="w-[166px] @max-[1239px]:w-[124px]" />
          <col className="w-[136px] @max-[1239px]:w-[116px]" />
          <col className="w-[206px] @max-[1239px]:w-[188px]" />
          <col className="w-[106px] @max-[1239px]:w-[84px]" />
          <col className="w-[166px] @max-[1239px]:w-[124px]" />
          <col className="w-[186px] @max-[1239px]:w-[172px] @max-[1099px]:w-[56px]" />
          <col className="w-[58px] @max-[1239px]:w-[50px]" />
        </colgroup>
        <thead className="bg-muted">
          <tr>
            <th scope="col" className={`${th} pl-[18px]`}>{t("Вакансия")}</th>
            <th scope="col" className={th}>{t("Отдел")}</th>
            <th scope="col" className={th}>{t("Статус")}</th>
            <th scope="col" className={th}>{t("Зарплата, сум")}</th>
            <th scope="col" className={`${th} text-right`}>{t("В работе")}</th>
            <th scope="col" className={th}>{t("Принято")}</th>
            <th scope="col" className={th}>
              <span className="@max-[1099px]:sr-only">{t("Рекрутер")}</span>
            </th>
            <th scope="col" className={`${th} pr-[18px]`}>
              <span className="sr-only">{t("Действия")}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((v) => {
            const closed = v.status === "Closed";
            const salary = salaryFull(v.salaryFrom, v.salaryTo);
            return (
              <tr
                key={v.id}
                tabIndex={0}
                aria-label={`${v.title}, ${t(vacancyStatusLabel[v.status])}`}
                onClick={(e) => !(e.target as HTMLElement).closest("a,button") && router.push(vacancyHref(v.id))}
                onKeyDown={(e) => e.key === "Enter" && e.target === e.currentTarget && router.push(vacancyHref(v.id))}
                className="cursor-pointer border-t border-line outline-none hover:bg-muted focus-visible:bg-muted focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent"
              >
                <td className="py-3 pl-[18px] pr-2">
                  <div className="flex min-w-0 items-center gap-3">
                    <span aria-hidden className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-muted">
                      <Briefcase className={`size-4 ${closed ? "text-ink-4" : "text-ink-2"}`} />
                    </span>
                    <div className="min-w-0">
                      <Link href={vacancyHref(v.id)} tabIndex={-1} className={`block truncate text-[14px] font-semibold hover:underline ${closed ? "text-ink-2" : "text-ink"}`}>
                        {v.title}
                      </Link>
                      <p className="truncate text-[11px] text-ink-4">{metaLine(v)}</p>
                    </div>
                  </div>
                </td>
                <td className="truncate px-2 py-3 text-[13px] text-ink-2">{t(v.department)}</td>
                <td className="px-2 py-3">
                  <StatusPill status={v.status} />
                </td>
                <td className="truncate px-2 py-3 text-[13px] font-medium tabular-nums text-ink-2">{t(salary.key, salary.vars)}</td>
                <td className="px-2 py-3 text-right">
                  <Link
                    href={candidatesOf(v.id)}
                    tabIndex={-1}
                    title={t("Кандидаты вакансии")}
                    className={`text-[13px] font-semibold tabular-nums hover:underline ${v.active > 0 ? "text-ink" : "text-ink-4"}`}
                  >
                    {v.active}
                  </Link>
                </td>
                <td className="px-2 py-3">
                  <span className="text-[12px] font-semibold tabular-nums text-ink-2">
                    {v.hired} / {v.headcount}
                  </span>
                  <Progress hired={v.hired} planned={v.headcount} />
                </td>
                <td className="px-2 py-3">
                  {v.recruiter ? (
                    <span className="flex min-w-0 items-center gap-2" title={v.recruiter}>
                      <Avatar name={v.recruiter} size={26} />
                      <span className="truncate text-[13px] text-ink-2 @max-[1099px]:sr-only">{v.recruiter}</span>
                    </span>
                  ) : (
                    <span className="text-[13px] text-ink-4">—</span>
                  )}
                </td>
                <td className="py-3 pl-2 pr-[18px] text-right">
                  {canManage && (
                    <button
                      type="button"
                      aria-label={t("Действия: {name}", { name: v.title })}
                      aria-haspopup="menu"
                      onClick={(e) => {
                        const r = e.currentTarget.getBoundingClientRect();
                        onMenu(menuAt(v.id, r.right, r.top, r.bottom));
                      }}
                      className="grid size-8 place-items-center rounded-lg text-ink-3 outline-none hover:bg-surface hover:text-ink focus-visible:ring-2 focus-visible:ring-accent"
                    >
                      <Ellipsis aria-hidden className="size-4" />
                    </button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

// ---------- Карточки (телефон и узкая область) ----------

function VacancyCard({ v, canManage, onMenu }: { v: VacancyRow; canManage: boolean; onMenu: (m: MenuAt) => void }) {
  const t = useT();
  const router = useRouter();
  const press = useRef<{ timer: number; x: number; y: number; fired: boolean } | null>(null);
  const salary = salaryShort(v.salaryFrom, v.salaryTo);
  const closed = v.status === "Closed";
  const meta = [t(v.department), v.city].filter(Boolean).join(" · ");

  // Долгое нажатие (или правая кнопка мыши) — меню действий; обычное нажатие — открыть вакансию.
  const cancel = () => {
    if (press.current) window.clearTimeout(press.current.timer);
  };
  const openMenu = (x: number, y: number) => canManage && onMenu(menuAt(v.id, Math.min(x + 120, window.innerWidth - 8), y, y));

  return (
    <article
      onPointerDown={(e) => {
        if (!canManage || e.pointerType === "mouse") return;
        const at = { x: e.clientX, y: e.clientY };
        press.current = { ...at, fired: false, timer: window.setTimeout(() => press.current && ((press.current.fired = true), openMenu(at.x, at.y)), 500) };
      }}
      onPointerMove={(e) => press.current && Math.hypot(e.clientX - press.current.x, e.clientY - press.current.y) > 10 && cancel()}
      onPointerUp={cancel}
      onPointerCancel={cancel}
      onContextMenu={(e) => {
        if (!canManage) return;
        e.preventDefault();
        if (!press.current?.fired) openMenu(e.clientX, e.clientY);
      }}
      onClick={(e) => {
        if (press.current?.fired) {
          press.current = null;
          return;
        }
        if (!(e.target as HTMLElement).closest("a,button")) router.push(vacancyHref(v.id));
      }}
      className="flex cursor-pointer select-none flex-col gap-3 rounded-2xl border border-line bg-surface p-3.5 [-webkit-touch-callout:none] active:bg-muted"
    >
      <div className="flex items-start gap-3">
        <span aria-hidden className="grid size-[38px] shrink-0 place-items-center rounded-[10px] bg-muted">
          <Briefcase className={`size-[17px] ${closed ? "text-ink-4" : "text-ink-2"}`} />
        </span>
        <div className="min-w-0 flex-1">
          <h3 className="text-[15px] font-semibold leading-snug">
            <Link href={vacancyHref(v.id)} className={`outline-none focus-visible:underline ${closed ? "text-ink-2" : "text-ink"}`}>
              {v.title}
            </Link>
          </h3>
          {meta && <p className="mt-0.5 truncate text-[12px] text-ink-3">{meta}</p>}
        </div>
        <StatusPill status={v.status} />
      </div>
      <dl className="grid grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)_minmax(0,1fr)] gap-2">
        <div className="min-w-0 rounded-[10px] bg-muted px-2.5 py-2">
          <dt className="text-[10px] font-semibold uppercase tracking-[0.3px] text-ink-4">{t("Зарплата")}</dt>
          <dd className="mt-0.5 truncate text-[14px] font-bold tabular-nums text-ink">{t(salary.key, salary.vars)}</dd>
        </div>
        <div className="min-w-0 rounded-[10px] bg-muted px-2.5 py-2">
          <dt className="text-[10px] font-semibold uppercase tracking-[0.3px] text-ink-4">{t("В работе")}</dt>
          <dd className={`mt-0.5 text-[14px] font-bold tabular-nums ${v.active > 0 ? "text-ink" : "text-ink-4"}`}>{v.active}</dd>
        </div>
        <div className="min-w-0 rounded-[10px] bg-muted px-2.5 py-2">
          <dt className="text-[10px] font-semibold uppercase tracking-[0.3px] text-ink-4">{t("Принято")}</dt>
          <dd className="mt-0.5 text-[14px] font-bold tabular-nums text-ink">
            {v.hired} / {v.headcount}
            <Progress hired={v.hired} planned={v.headcount} />
          </dd>
        </div>
      </dl>
      <div className="flex items-center gap-2">
        {v.recruiter ? <Avatar name={v.recruiter} size={22} /> : null}
        <span className="min-w-0 flex-1 truncate text-[12px] text-ink-2">
          {v.recruiter ? t("Рекрутер: {name}", { name: shortName(v.recruiter) }) : t("Рекрутер не назначен")}
        </span>
        <Link href={candidatesOf(v.id)} className="flex shrink-0 items-center gap-0.5 text-[12px] font-semibold text-accent-strong outline-none focus-visible:underline">
          {t("Кандидаты")}
          <ChevronRight aria-hidden className="size-3.5" />
        </Link>
      </div>
    </article>
  );
}

// ---------- Состояния ----------

function Skel({ className }: { className: string }) {
  return <span aria-hidden className={`block animate-pulse rounded-md bg-muted ${className}`} />;
}

export function LoadingList() {
  const t = useT();
  return (
    <div aria-busy role="status" aria-label={t("Загрузка…")}>
      <div className="overflow-hidden rounded-2xl border border-line bg-surface @max-[999px]:hidden">
        <div className="h-[37px] bg-muted" />
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="flex items-center gap-4 border-t border-line px-[18px] py-3">
            <Skel className="size-9 rounded-[10px]" />
            <div className="flex-1 space-y-1.5">
              <Skel className="h-3.5 w-48" />
              <Skel className="h-2.5 w-32" />
            </div>
            <Skel className="h-3 w-24" />
            <Skel className="h-5 w-16 rounded-[7px]" />
            <Skel className="h-3 w-40" />
            <Skel className="h-3 w-28" />
          </div>
        ))}
      </div>
      <div className="grid gap-2.5 @min-[600px]:grid-cols-2 @min-[1000px]:hidden">
        {Array.from({ length: 3 }, (_, i) => (
          <div key={i} className="space-y-3 rounded-2xl border border-line bg-surface p-3.5">
            <div className="flex gap-3">
              <Skel className="size-[38px] rounded-[10px]" />
              <div className="flex-1 space-y-1.5">
                <Skel className="h-4 w-40" />
                <Skel className="h-3 w-28" />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-2">
              <Skel className="h-12 rounded-[10px]" />
              <Skel className="h-12 rounded-[10px]" />
              <Skel className="h-12 rounded-[10px]" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function Empty({ filtered, canManage, onReset }: { filtered: boolean; canManage: boolean; onReset: () => void }) {
  const t = useT();
  return (
    <div className="flex flex-col items-center gap-2 rounded-2xl border border-line bg-surface px-6 py-12 text-center">
      <span aria-hidden className="grid size-12 place-items-center rounded-[14px] bg-accent-soft text-accent-strong">
        {filtered ? <Search className="size-5" /> : <Briefcase className="size-5" />}
      </span>
      <p className="mt-1 text-[15px] font-semibold text-ink">{t(filtered ? "Ничего не найдено" : "Вакансий пока нет")}</p>
      <p className="max-w-md text-[13px] text-ink-3">
        {t(filtered ? "Измените поиск или фильтры." : "Создайте первую вакансию — она появится здесь и может быть опубликована на hh.uz и в Telegram")}
      </p>
      <div className="mt-2">
        {filtered ? (
          <button type="button" onClick={onReset} className="h-9 rounded-[10px] border border-line-strong bg-surface px-3.5 text-[13px] font-semibold text-ink hover:bg-muted">
            {t("Сбросить фильтры")}
          </button>
        ) : (
          canManage && <NewVacancyButton />
        )}
      </div>
    </div>
  );
}

// ---------- Экран ----------

/** Список вакансий: поиск, статус кнопками, отдел; таблица или карточки; HR создаёт новую (кнопка, «+» в шапке телефона, ?new=1). */
export function VacanciesView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const status = parseStatus(params.get("status"));
  const department = params.get("department") ?? "";
  const [q, setQ] = useState(params.get("q") ?? "");
  const [rows, setRows] = useState<VacancyRow[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(params.get("new") === "1" && ctx.canManage);
  const [departmentsOpen, setDepartmentsOpen] = useState(false);
  const [menu, setMenu] = useState<MenuAt | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await recruitmentApi.vacancies());
      setLoadError(null);
    } catch (e) {
      setLoadError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => void load(), [load]);

  // Адрес: поиск — через 300 мс после ввода, статус и отдел — сразу; без перезагрузки страницы.
  const setParam = useCallback(
    (patch: Record<string, string | null>) => {
      const next = new URLSearchParams(window.location.search);
      for (const [k, v] of Object.entries(patch)) {
        if (v) next.set(k, v);
        else next.delete(k);
      }
      const s = next.toString();
      window.history.replaceState(null, "", s ? `${pathname}?${s}` : pathname);
    },
    [pathname],
  );
  useEffect(() => {
    const timer = window.setTimeout(() => (params.get("q") ?? "") !== q.trim() && setParam({ q: q.trim() || null }), 300);
    return () => window.clearTimeout(timer);
  }, [q, params, setParam]);

  // «+» в шапке телефона и кнопка пустого списка — событием; ?new=1 — ссылкой. Параметр убираем, чтобы обновление страницы не открывало форму.
  useEffect(() => {
    const open = () => ctx.canManage && setCreating(true);
    window.addEventListener(NEW_VACANCY, open);
    if (params.get("new") === "1") setParam({ new: null });
    return () => window.removeEventListener(NEW_VACANCY, open);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const scoped = useMemo(() => sortVacancies((rows ?? []).filter((v) => matchesQuery(v, q, department))), [rows, q, department]);
  const counts = useMemo(() => statusCounts(scoped), [scoped]);
  const visible = useMemo(() => scoped.filter((v) => matchesStatus(v, status)), [scoped, status]);
  const filtered = !!(q.trim() || department || status !== "all");
  const departments = useMemo(() => {
    const present = new Set((rows ?? []).map((v) => v.departmentId));
    return ctx.departments.filter((d) => present.has(d.id) || d.id === department);
  }, [rows, ctx.departments, department]);
  const departmentName = ctx.departments.find((d) => d.id === department)?.name;

  const reset = () => {
    setQ("");
    setParam({ q: null, status: null, department: null });
  };

  const changeStatus = async (v: VacancyRow, next: VacancyStatus) => {
    if (next === "Closed" && v.active > 0 && !confirm(t("Закрыть вакансию? Кандидаты в работе ({n}) получат отказ «вакансия закрыта».", { n: v.active }))) return;
    try {
      setActionError(null);
      await recruitmentApi.vacancyStatus(v.id, next);
      await load();
      refreshRecruitingNav();
    } catch (e) {
      setActionError((e as Error).message);
    }
  };

  // Меню по месту: строка списка может смениться (обновление) — тогда закрываем.
  const menuRow = menu ? rows?.find((r) => r.id === menu.id) : null;
  useLayoutEffect(() => {
    if (menu && !menuRow) setMenu(null);
  }, [menu, menuRow]);
  const closeMenu = useCallback(() => setMenu(null), []);

  const search = (mobile: boolean) => (
    <label className={`relative flex items-center ${mobile ? "h-11 w-full" : "order-1 h-10 min-w-[180px] flex-1 @min-[1000px]:max-w-[300px]"}`}>
      <span className="sr-only">{t("Поиск вакансий")}</span>
      <Search aria-hidden className="pointer-events-none absolute left-3 size-4 text-ink-4" />
      <input
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder={t("Должность или город")}
        className={`h-full w-full border border-line bg-surface pl-9 text-ink outline-none placeholder:text-ink-4 focus:border-accent focus:ring-2 focus:ring-accent/20 ${
          mobile ? "rounded-xl pr-12 text-[14px]" : "rounded-[10px] pr-3 text-[13px]"
        }`}
      />
      {mobile && (
        <button
          type="button"
          onClick={() => setDepartmentsOpen(true)}
          aria-label={t(department ? "Отдел: {name}" : "Фильтр по отделу", { name: departmentName ?? "" })}
          className="absolute right-1.5 grid size-8 place-items-center rounded-lg text-ink-3 outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-accent"
        >
          <SlidersHorizontal aria-hidden className="size-[18px]" />
          {department && <span aria-hidden className="absolute right-1 top-1 size-2 rounded-full bg-accent" />}
        </button>
      )}
    </label>
  );

  return (
    <div className="@container flex flex-col gap-4 max-md:gap-3.5">
      {/* Компьютер и планшет: поиск, статус, отдел, «Новая вакансия». */}
      <div className="flex flex-wrap items-center gap-2.5 max-md:hidden @min-[1000px]:flex-nowrap">
        {search(false)}
        <div className="order-3 @min-[1000px]:order-2">
          <StatusFilters value={status} counts={counts} onChange={(s) => setParam({ status: s === "all" ? null : s })} />
        </div>
        <label className="relative order-4 flex h-10 shrink-0 items-center @min-[1000px]:order-3">
          <span className="sr-only">{t("Отдел")}</span>
          <Building aria-hidden className="pointer-events-none absolute left-3 size-4 text-ink-3" />
          <select
            value={department}
            onChange={(e) => setParam({ department: e.target.value || null })}
            className="h-full appearance-none rounded-[10px] border border-line bg-surface pl-9 pr-9 text-[13px] font-medium text-ink-2 outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
          >
            <option value="">{t("Все отделы")}</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {t(d.name)}
              </option>
            ))}
          </select>
          <ChevronDown aria-hidden className="pointer-events-none absolute right-3 size-4 text-ink-3" />
        </label>
        {ctx.canManage && (
          <div className="order-2 ml-auto @min-[1000px]:order-4">
            <NewVacancyButton />
          </div>
        )}
      </div>

      {/* Телефон: поиск с фильтром отдела, статусы. */}
      <div className="flex flex-col gap-3.5 md:hidden">
        {search(true)}
        <StatusFilters value={status} counts={counts} onChange={(s) => setParam({ status: s === "all" ? null : s })} mobile />
        {department && (
          <button
            type="button"
            onClick={() => setParam({ department: null })}
            className="-mt-1.5 flex w-fit items-center gap-1.5 rounded-lg bg-accent-soft px-2.5 py-1 text-[12px] font-semibold text-accent-strong"
          >
            {t(departmentName ?? "Отдел")} ✕
          </button>
        )}
      </div>

      {(loadError || actionError) && (
        <div role="alert" className="flex flex-wrap items-center gap-3 rounded-2xl border border-bad/25 bg-bad-soft px-4 py-3">
          <TriangleAlert aria-hidden className="size-5 shrink-0 text-bad" />
          <p className="min-w-0 flex-1 text-[13px] text-ink-2">{loadError ? t("Не удалось загрузить вакансии: {error}", { error: loadError }) : actionError}</p>
          {loadError ? (
            <button type="button" onClick={() => void load()} disabled={loading} className="flex h-9 items-center gap-2 rounded-lg bg-bad px-3.5 text-[13px] font-semibold text-white disabled:opacity-60">
              <RotateCw aria-hidden className={`size-4 ${loading ? "animate-spin" : ""}`} />
              {t("Повторить")}
            </button>
          ) : (
            <button type="button" onClick={() => setActionError(null)} className="text-[13px] font-semibold text-ink-3 hover:text-ink">
              {t("Закрыть")}
            </button>
          )}
        </div>
      )}

      <h2 className="flex items-baseline gap-2 text-[17px] font-bold text-ink md:hidden">
        {t("Вакансии")}
        {rows && <span className="text-[12px] font-semibold text-ink-4 tabular-nums">{visible.length}</span>}
      </h2>

      {rows === null ? (
        loadError ? null : <LoadingList />
      ) : visible.length === 0 ? (
        <Empty filtered={filtered && rows.length > 0} canManage={ctx.canManage} onReset={reset} />
      ) : (
        <>
          <div className="@max-[999px]:hidden">
            <VacancyTable rows={visible} canManage={ctx.canManage} onMenu={setMenu} />
          </div>
          <div className="grid gap-2.5 @min-[600px]:grid-cols-2 @min-[1000px]:hidden">
            {visible.map((v) => (
              <VacancyCard key={v.id} v={v} canManage={ctx.canManage} onMenu={setMenu} />
            ))}
          </div>
        </>
      )}

      {menu && menuRow && <RowMenu at={menu} row={menuRow} onClose={closeMenu} onStatus={(v, s) => void changeStatus(v, s)} />}

      <Sheet open={departmentsOpen} onClose={() => setDepartmentsOpen(false)} title={t("Отдел")}>
        <div role="radiogroup" aria-label={t("Отдел")} className="flex flex-col pb-2">
          {[{ id: "", name: "Все отделы" }, ...departments].map((d) => {
            const on = d.id === department;
            return (
              <button
                key={d.id || "all"}
                type="button"
                role="radio"
                aria-checked={on}
                onClick={() => {
                  setParam({ department: d.id || null });
                  setDepartmentsOpen(false);
                }}
                className={`flex items-center justify-between rounded-xl px-3 py-3 text-left text-[14px] ${on ? "bg-accent-soft font-semibold text-accent-strong" : "text-ink hover:bg-muted"}`}
              >
                {t(d.name)}
                {on && <span aria-hidden>✓</span>}
              </button>
            );
          })}
        </div>
      </Sheet>

      <Sheet open={creating} onClose={() => setCreating(false)} title={t("Новая вакансия")} size="xl">
        <VacancyForm
          ctx={ctx}
          initial={emptyInput(ctx)}
          onCancel={() => setCreating(false)}
          onSave={async (input) => {
            const created = await recruitmentApi.createVacancy(input);
            router.push(vacancyHref(created.id));
          }}
        />
      </Sheet>
    </div>
  );
}
