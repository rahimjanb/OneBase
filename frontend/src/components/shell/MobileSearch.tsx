"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Building2, ChevronRight, ClipboardList, Loader2, Search, UserRound, X, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { departmentRows } from "@/lib/dashboard.mock";
import type { HrEmployeeRow } from "@/lib/hr";
import { canOpen } from "@/lib/navigation";
import type { PageResult } from "@/lib/tasks";
import { settings, visibleWorkspace } from "./Sidebar";

/** Круглая кнопка шапки телефона 40×40 (как у уведомлений). */
const headerButton =
  "grid size-[40px] place-items-center rounded-full border border-white/10 bg-white/[0.05] text-current transition-colors duration-150 hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";

type Hit = { key: string; href: string; title: string; hint?: string; icon: LucideIcon };
type Found = { tasks: Hit[]; people: Hit[] };

/**
 * Поиск в шапке телефона (на компьютере — поле с ⌘K): полноэкранное окно — разделы и отделы сразу, задачи и сотрудники — с сервера
 * по мере ввода (от двух букв; нет прав на раздел — его результатов нет). Закрывается «Отмена», Esc или выбором результата.
 */
export function MobileSearch({ permissions }: { permissions: readonly string[] | null }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} aria-label={t("Поиск")} aria-haspopup="dialog" className={`${headerButton} lg:hidden`}>
        <Search aria-hidden className="size-[18px]" strokeWidth={1.9} />
      </button>
      {open && <SearchPanel permissions={permissions} onClose={() => setOpen(false)} />}
    </>
  );
}

function SearchPanel({ permissions, onClose }: { permissions: readonly string[] | null; onClose: () => void }) {
  const t = useT();
  const input = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<Found>({ tasks: [], people: [] });
  const [loading, setLoading] = useState(false);
  const q = query.trim();

  useEffect(() => {
    input.current?.focus({ preventScroll: true });
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    const scroller = document.querySelector<HTMLElement>("[data-app-scroll]");
    const overflow = scroller?.style.overflow ?? "";
    if (scroller) scroller.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      if (scroller) scroller.style.overflow = overflow;
    };
  }, [onClose]);

  // Задачи и сотрудники — с сервера, через 250 мс после ввода; ответ на прежний запрос не перетирает новый.
  useEffect(() => {
    if (q.length < 2) {
      setFound({ tasks: [], people: [] });
      setLoading(false);
      return;
    }
    let alive = true;
    setLoading(true);
    const timer = window.setTimeout(async () => {
      const allowed = (href: string) => permissions === null || canOpen(href, permissions);
      const [tasks, people] = await Promise.all([
        allowed("/tasks")
          ? bff<PageResult<{ id: string; title: string; departmentName: string }>>(`work/tasks?scope=all&search=${encodeURIComponent(q)}&pageSize=5`).catch(() => null)
          : null,
        allowed("/hr") ? bff<PageResult<HrEmployeeRow>>(`hr/employees?search=${encodeURIComponent(q)}&pageSize=5`).catch(() => null) : null,
      ]);
      if (!alive) return;
      setFound({
        tasks: (tasks?.items ?? []).map((x) => ({ key: x.id, href: `/tasks/task/${x.id}`, title: x.title, hint: x.departmentName, icon: ClipboardList })),
        people: (people?.items ?? []).map((x) => ({ key: x.id, href: `/hr/employees/${x.id}`, title: x.fullName, hint: x.job ?? x.division ?? undefined, icon: UserRound })),
      });
      setLoading(false);
    }, 250);
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
  }, [q, permissions]);

  const match = (text: string) => !q || text.toLowerCase().includes(q.toLowerCase());
  const sections: Hit[] = [...visibleWorkspace(permissions).filter((i) => !i.soon), ...(permissions === null || canOpen(settings.href, permissions) ? [settings] : [])]
    .filter((i) => match(t(i.label)))
    .map((i) => ({ key: i.href, href: i.href, title: t(i.label), icon: i.icon }));
  const departments: Hit[] = q
    ? departmentRows.filter((d) => match(t(d.name))).map((d) => ({ key: d.code, href: d.href, title: t(d.name), hint: t(d.metric), icon: Building2 }))
    : [];
  const groups = [
    { title: t("Разделы"), hits: sections },
    { title: t("Отделы"), hits: departments },
    { title: t("Задачи"), hits: found.tasks },
    { title: t("Сотрудники"), hits: found.people },
  ].filter((g) => g.hits.length > 0);

  const host = document.querySelector<HTMLElement>("[data-app-shell]") ?? document.body;
  return createPortal(
    <div role="dialog" aria-modal="true" aria-label={t("Поиск")} className="fixed inset-0 z-50 flex flex-col bg-surface-solid pt-[env(safe-area-inset-top)] text-ink lg:hidden">
      <div className="flex shrink-0 items-center gap-2 border-b border-line px-4 py-2.5">
        <label className="flex h-11 min-w-0 flex-1 items-center gap-2 rounded-[12px] border border-line bg-muted px-3">
          <Search aria-hidden className="size-4 shrink-0 text-ink-3" />
          <input
            ref={input}
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("Поиск по задачам, отделам, сотрудникам…")}
            aria-label={t("Поиск")}
            enterKeyHint="search"
            className="min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-ink-3 [&::-webkit-search-cancel-button]:hidden"
          />
          {loading ? (
            <Loader2 aria-hidden className="size-4 shrink-0 animate-spin text-ink-3" />
          ) : (
            query && (
              <button type="button" onClick={() => setQuery("")} aria-label={t("Очистить")} className="-mr-1 grid size-8 place-items-center rounded-full text-ink-3">
                <X className="size-4" />
              </button>
            )
          )}
        </label>
        <button type="button" onClick={onClose} className="h-11 shrink-0 px-1 text-[15px] font-medium text-accent-strong">
          {t("Отмена")}
        </button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-2 pb-[calc(1rem+env(safe-area-inset-bottom))]">
        {groups.map((g) => (
          <section key={g.title} className="pt-3">
            <h2 className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-[0.6px] text-ink-3">{g.title}</h2>
            <ul>
              {g.hits.map((h) => (
                <li key={h.key}>
                  <Link href={h.href} onClick={onClose} className="flex min-h-[48px] items-center gap-3 rounded-[12px] px-3 py-2 hover:bg-muted">
                    <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-muted text-ink-2">
                      <h.icon aria-hidden className="size-[18px]" strokeWidth={1.9} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[15px] text-ink">{h.title}</span>
                      {h.hint && <span className="block truncate text-xs text-ink-3">{h.hint}</span>}
                    </span>
                    <ChevronRight aria-hidden className="size-4 shrink-0 text-ink-3" />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))}
        {q.length >= 2 && !loading && groups.length === 0 && <p className="px-4 py-10 text-center text-sm text-ink-3">{t("Ничего не найдено")}</p>}
      </div>
    </div>,
    host,
  );
}
