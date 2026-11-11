"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { CalendarCheck, ChevronLeft, Gauge, MapPin, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { RecruitingNav } from "@/components/recruitment/RecruitingNav";
import { useT } from "@/i18n/client";
import { DIALOGS_PATH, dialogPath, dialogsApi, type Dialog, type DialogRow, type DialogSummary } from "@/lib/hr-dialogs";
import { filterCounts, matchesFilter, matchesSearch, sortDialogs, type DialogFilter } from "@/lib/hr-dialogs-view";
import { CandidatePanel, interviewText } from "./CandidatePanel";
import { ChatHeader, Composer, Feed, MobileChatHeader, ModeSwitch, type ChatActions } from "./ChatPane";
import { DialogList, FilterChips, NeedHrHint, SearchBox } from "./DialogList";
import { NotConnected, NotConnectedBanner } from "./NotConnected";
import { NumberPill, SettingsLink } from "./parts";

/** Список — раз в 10 с, открытый диалог — раз в 4 с (пока вкладка видна): готового канала событий в OneBase нет; «Обновить» — сразу. */
const LIST_MS = 10_000;
const DIALOG_MS = 4_000;

function useMedia(query: string, server: boolean): boolean {
  return useSyncExternalStore(
    (notify) => {
      const m = window.matchMedia(query);
      m.addEventListener("change", notify);
      return () => m.removeEventListener("change", notify);
    },
    () => window.matchMedia(query).matches,
    () => server,
  );
}

/**
 * Три колонки — когда хватает места самой странице, а не окну: слева меню приложения (≈250 px), и в окне 1280 переписке осталось бы меньше
 * 300 px. 1180 = список 340 + карточка 320 + отступы ≈ 80 + переписка от 440.
 */
const THREE_COLUMNS = 1180;

function useWidth(element: HTMLElement | null): number | null {
  const [width, setWidth] = useState<number | null>(null);
  useLayoutEffect(() => {
    if (!element) return;
    setWidth(element.clientWidth);
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(element);
    return () => observer.disconnect();
  }, [element]);
  return width;
}

const selectedFrom = (pathname: string) => (pathname.startsWith(`${DIALOGS_PATH}/`) ? pathname.slice(DIALOGS_PATH.length + 1).split("/")[0] || null : null);

/**
 * HR → «Подбор» → «Диалоги Telegram» (макеты tM2zD, s7Vqm, N4B9C, G5JPV). Компьютер (окно от 1024): страница шире 1180 — три колонки (список 340 |
 * переписка | карточка кандидата 320), уже — список 300 и переписка, карточка — выезжающая панель; меньше 1024 — экран списка и экран чата (отдельный адрес
 * /hr/recruitment/telegram/{id}, нижняя панель приложения скрыта). Палитра — токены макета (data-palette). Переход между диалогами —
 * без перезагрузки страницы (history), данные — опросом.
 */
export function DialogsScreen({ initialSummary, canManage }: { initialSummary: DialogSummary | null; canManage: boolean }) {
  const t = useT();
  const pathname = usePathname();
  const selectedId = selectedFrom(pathname);
  const wide = useMedia("(min-width: 1024px)", true);
  const [frame, setFrame] = useState<HTMLDivElement | null>(null);
  const frameWidth = useWidth(frame);
  const xl = frameWidth === null || frameWidth >= THREE_COLUMNS;

  const [summary, setSummary] = useState<DialogSummary | null>(initialSummary);
  const [rows, setRows] = useState<DialogRow[] | null>(null);
  const [listLoading, setListLoading] = useState(true);
  const [filter, setFilter] = useState<DialogFilter>("all");
  const [search, setSearch] = useState("");
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [dialogLoading, setDialogLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** Сбой опроса (сеть, перезапуск сервера) — снимается сам при следующем удачном ответе, в отличие от ошибки действия. */
  const [loadError, setLoadError] = useState<string | null>(null);
  const [panel, setPanel] = useState(false);
  const current = useRef<string | null>(selectedId);
  current.current = selectedId;

  const loadList = useCallback(async () => {
    try {
      const [list, s] = await Promise.all([dialogsApi.list(), dialogsApi.summary()]);
      setRows(list);
      setSummary(s);
      setLoadError(null);
    } catch (e) {
      setLoadError((e as Error).message);
    } finally {
      setListLoading(false);
    }
  }, []);

  const loadDialog = useCallback(async (id: string) => {
    try {
      const d = await dialogsApi.get(id);
      if (current.current === id) {
        setDialog(d);
        setRows((list) => list?.map((r) => (r.id === id ? { ...r, unread: 0, mode: d.mode } : r)) ?? null);
        setLoadError(null);
      }
    } catch (e) {
      if (current.current === id) setLoadError((e as Error).message);
    } finally {
      if (current.current === id) setDialogLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadList();
    const timer = setInterval(() => !document.hidden && void loadList(), LIST_MS);
    return () => clearInterval(timer);
  }, [loadList]);

  useEffect(() => {
    setDialog(null);
    setPanel(false);
    if (!selectedId) return;
    setDialogLoading(true);
    void loadDialog(selectedId);
    const timer = setInterval(() => !document.hidden && void loadDialog(selectedId), DIALOG_MS);
    return () => clearInterval(timer);
  }, [selectedId, loadDialog]);

  const ordered = useMemo(() => sortDialogs(rows ?? []), [rows]);
  const visible = useMemo(() => ordered.filter((r) => matchesFilter(r, filter) && matchesSearch(r, search)), [ordered, filter, search]);
  const counts = useMemo(() => filterCounts(rows ?? []), [rows]);
  const connected = !!summary?.number?.connected;
  const canConfigure = !!summary?.canConfigure;

  // На компьютере без выбранного диалога — открыть первый (как в макете).
  useEffect(() => {
    if (wide && !selectedId && connected && ordered.length > 0) window.history.replaceState(null, "", dialogPath(ordered[0].id));
  }, [wide, selectedId, connected, ordered]);

  const open = (id: string) => {
    if (id !== selectedId) window.history.pushState(null, "", dialogPath(id));
  };
  const back = () => window.history.pushState(null, "", DIALOGS_PATH);
  const reset = () => {
    setFilter("all");
    setSearch("");
  };

  const actions: ChatActions = {
    send: async (text, file) => {
      if (!selectedId) return;
      const key = crypto.randomUUID();
      if (file) await dialogsApi.sendFile(selectedId, file, text, key);
      else await dialogsApi.send(selectedId, text, key);
      await Promise.all([loadDialog(selectedId), loadList()]);
    },
    mode: async (mode) => {
      if (!selectedId) return;
      await dialogsApi.mode(selectedId, mode);
      await Promise.all([loadDialog(selectedId), loadList()]);
    },
    retry: async (outgoingId) => {
      if (!selectedId) return;
      setDialog(await dialogsApi.retry(selectedId, outgoingId));
    },
    draft: async () => (selectedId ? (await dialogsApi.draft(selectedId)).text : ""),
    refresh: () => {
      void loadList();
      if (selectedId) void loadDialog(selectedId);
    },
  };
  const onRetry = (id: string) => void actions.retry(id).catch((e: Error) => setError(e.message));
  const reloadCandidate = () => selectedId && void Promise.all([loadDialog(selectedId), loadList()]);
  const emptyKind = (rows?.length ?? 0) === 0 ? "none" : "nothing";

  // ---------- Телефон и планшет: список → чат ----------
  if (!wide) {
    if (selectedId) {
      return (
        <div data-palette="sales-secondary" className="flex h-full min-h-0 flex-col bg-page">
          {dialog ? (
            <>
              <MobileChatHeader dialog={dialog} onBack={back} onCandidate={() => setPanel(true)} />
              <div className="space-y-2.5 border-b border-line px-4 py-2.5">
                <ModeSwitch mode={dialog.mode} onChange={(m) => void actions.mode(m).catch((e: Error) => setError(e.message))} disabled={!dialog.canManage} wide />
                <SummaryChips dialog={dialog} />
              </div>
              <Feed dialog={dialog} loading={dialogLoading} mobile onRetry={onRetry} />
              <Composer key={dialog.id} dialog={dialog} actions={actions} mobile />
              {dialog.candidate && (
                <Sheet open={panel} onClose={() => setPanel(false)} title={dialog.candidate.name} subtitle={t("Карточка кандидата")}>
                  <CandidatePanel c={dialog.candidate} canManage={dialog.canManage} onChanged={reloadCandidate} />
                </Sheet>
              )}
            </>
          ) : (
            <>
              <div className="flex items-center gap-2 px-3 pb-2.5 pt-1.5">
                <button type="button" onClick={back} aria-label={t("Назад")} className="grid size-[40px] place-items-center rounded-full bg-surface text-ink">
                  <ChevronLeft className="size-5" />
                </button>
              </div>
              <Feed dialog={null} loading mobile onRetry={onRetry} />
            </>
          )}
          {(error ?? loadError) && <ErrorBar error={(error ?? loadError)!} onClose={() => (error ? setError(null) : setLoadError(null))} />}
        </div>
      );
    }

    return (
      <div data-palette="sales-secondary" className="min-h-full bg-page">
        <header className="flex h-14 items-center gap-2 px-3">
          <Link href="/hr/recruitment" aria-label={t("Назад")} className="grid size-[40px] place-items-center rounded-full bg-surface text-ink">
            <ChevronLeft className="size-5" />
          </Link>
          <div className="min-w-0 flex-1 text-center">
            <h1 className="truncate text-[15px] font-bold text-ink">{t("Диалоги Telegram")}</h1>
            <p className="text-[11px] font-medium text-ink-3">{t("HR · Подбор")}</p>
          </div>
          {canConfigure ? <SettingsLink compact /> : <span className="size-[40px]" />}
        </header>
        <div className="flex flex-col gap-3 px-4 pb-6 pt-1">
          <RecruitingNav canManage={canManage} />
          <NumberPill summary={summary} wide />
          {!connected ? (
            <section className="rounded-2xl border border-line bg-surface px-4">
              <NotConnected canConfigure={canConfigure} mobile />
            </section>
          ) : (
            <>
              <SearchBox value={search} onChange={setSearch} mobile />
              <FilterChips value={filter} onChange={setFilter} counts={counts} mobile disabled={(rows?.length ?? 0) === 0} />
              {filter !== "need_hr" && <NeedHrHint count={counts.need_hr} onClick={() => setFilter("need_hr")} />}
              <section className="overflow-hidden rounded-2xl border border-line bg-surface">
                <DialogList rows={visible} selectedId={null} onOpen={open} loading={listLoading} mobile emptyText={emptyKind} onReset={reset} />
              </section>
            </>
          )}
          {(error ?? loadError) && <ErrorBar error={(error ?? loadError)!} onClose={() => (error ? setError(null) : setLoadError(null))} />}
        </div>
      </div>
    );
  }

  // ---------- Компьютер ----------
  const card = "flex min-h-0 flex-col overflow-hidden rounded-2xl border border-line bg-surface";
  return (
    <div ref={setFrame} data-palette="sales-secondary" className="flex h-full min-h-0 flex-col">
      <div className="px-6 pt-[18px]">
        <nav aria-label={t("Навигация")} className="flex flex-wrap gap-1.5 text-xs font-medium text-ink-4">
          {[["OneBase", "/"], ["Отделы", "/departments"], ["HR", "/hr"], ["Подбор", "/hr/recruitment"]].map(([label, href]) => (
            <span key={href} className="flex gap-1.5">
              <Link href={href} className="hover:text-ink">{t(label)}</Link>
              <span aria-hidden>/</span>
            </span>
          ))}
          <span className="text-ink-3">{t("Диалоги Telegram")}</span>
        </nav>
        <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-[28px] font-bold leading-tight tracking-[-0.4px] text-ink">{t("Диалоги Telegram")}</h1>
            <p className="mt-1 text-sm font-medium text-ink-3">{t("Переписка с кандидатами с номера HR: ведёт AI или человек")}</p>
          </div>
          <div className="flex items-center gap-2.5">
            <NumberPill summary={summary} />
            {canConfigure && <SettingsLink />}
          </div>
        </div>
      </div>
      <div className="border-b border-line px-6 pb-[18px] pt-3.5">
        <RecruitingNav canManage={canManage} />
      </div>
      {summary && !connected && <NotConnectedBanner canConfigure={canConfigure} />}

      <div className={`grid min-h-0 flex-1 gap-4 px-6 pb-6 pt-4 ${xl ? "grid-cols-[340px_minmax(0,1fr)_320px]" : "grid-cols-[300px_minmax(0,1fr)]"}`}>
        <aside className={card} aria-label={t("Диалоги")}>
          <div className="space-y-3 border-b border-line px-3.5 pb-3 pt-3.5">
            <SearchBox value={search} onChange={setSearch} onRefresh={actions.refresh} refreshing={listLoading} />
            <FilterChips value={filter} onChange={setFilter} counts={counts} disabled={(rows?.length ?? 0) === 0} />
          </div>
          <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">
            <DialogList rows={visible} selectedId={selectedId} onOpen={open} loading={listLoading} emptyText={emptyKind} onReset={reset} />
          </div>
        </aside>

        {summary && !connected ? (
          <section className={`${card} ${xl ? "col-span-2" : ""} justify-center overflow-y-auto px-6`}>
            <NotConnected canConfigure={canConfigure} />
          </section>
        ) : !selectedId ? (
          <section className={`${card} ${xl ? "col-span-2" : ""} items-center justify-center`}>
            <p className="text-[13px] text-ink-3">{t("Выберите диалог слева")}</p>
          </section>
        ) : (
          <>
            <section className={`${card} @container`} aria-label={t("Переписка")}>
              {dialog ? <ChatHeader dialog={dialog} actions={actions} onCandidate={!xl && dialog.candidate ? () => setPanel(true) : undefined} /> : null}
              <Feed dialog={dialog} loading={dialogLoading} onRetry={onRetry} />
              {dialog && <Composer key={dialog.id} dialog={dialog} actions={actions} />}
            </section>
            {xl && (
              <aside className={`${card} overflow-y-auto`} aria-label={t("Карточка кандидата")}>
                {dialog?.candidate ? (
                  <CandidatePanel c={dialog.candidate} canManage={dialog.canManage} onChanged={reloadCandidate} />
                ) : (
                  <p className="m-auto p-6 text-center text-xs text-ink-3">{t(dialog ? "В диалоге нет карточки кандидата" : "Загрузка…")}</p>
                )}
              </aside>
            )}
          </>
        )}
      </div>

      {!xl && panel && dialog?.candidate && (
        <div className="fixed inset-0 z-40 flex justify-end bg-black/40" onClick={() => setPanel(false)} role="dialog" aria-modal="true" aria-label={t("Карточка кандидата")}>
          <div className="relative h-full w-[340px] overflow-y-auto border-l border-line bg-surface-solid" onClick={(e) => e.stopPropagation()}>
            <button type="button" onClick={() => setPanel(false)} aria-label={t("Закрыть")} className="absolute right-3 top-3 z-10 grid size-8 place-items-center rounded-full text-ink-3 hover:bg-muted">
              <X className="size-4" />
            </button>
            <CandidatePanel c={dialog.candidate} canManage={dialog.canManage} onChanged={reloadCandidate} />
          </div>
        </div>
      )}
      {(error ?? loadError) && <ErrorBar error={(error ?? loadError)!} onClose={() => (error ? setError(null) : setLoadError(null))} />}
    </div>
  );
}

/** Сводка под переключателем на телефоне: оценка AI, собеседование, город. */
function SummaryChips({ dialog }: { dialog: Dialog }) {
  const t = useT();
  const c = dialog.candidate;
  if (!c || (!c.score && !c.interviewAt && !c.city)) return null;
  return (
    <div className="-mx-4 flex gap-1.5 overflow-x-auto px-4 [scrollbar-width:none]">
      {c.score && (
        <span className="flex shrink-0 items-center gap-1 rounded-lg bg-ok-soft px-2 py-1 text-[11px] font-semibold text-ok tabular-nums">
          <Gauge className="size-3.5" aria-hidden />
          AI {c.score.total}/100
        </span>
      )}
      {c.interviewAt && (
        <span className="flex shrink-0 items-center gap-1 rounded-lg bg-accent-soft px-2 py-1 text-[11px] font-semibold text-accent-strong">
          <CalendarCheck className="size-3.5" aria-hidden />
          {t("Собес. {at}", { at: interviewText(c.interviewAt, c.interviewTimeZone) })}
        </span>
      )}
      {c.city && (
        <span className="flex shrink-0 items-center gap-1 rounded-lg border border-line bg-surface px-2 py-1 text-[11px] font-semibold text-ink-2">
          <MapPin className="size-3.5" aria-hidden />
          {c.city}
        </span>
      )}
    </div>
  );
}

function ErrorBar({ error, onClose }: { error: string; onClose: () => void }) {
  const t = useT();
  return (
    <div role="alert" className="fixed bottom-6 left-1/2 z-50 flex max-w-[90vw] -translate-x-1/2 items-center gap-3 rounded-xl border border-bad/30 bg-surface-solid px-4 py-2.5 text-sm text-bad shadow-lg">
      <span>{t(error)}</span>
      <button type="button" onClick={onClose} aria-label={t("Закрыть")} className="text-ink-3 hover:text-ink">
        <X className="size-4" />
      </button>
    </div>
  );
}
