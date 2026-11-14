"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  ArrowLeft,
  CalendarClock,
  CalendarX,
  ChevronRight,
  CircleDashed,
  CircleSlash,
  CircleX,
  Eye,
  FileText,
  Gift,
  History,
  Info,
  Languages,
  LayoutGrid,
  Loader2,
  Palette,
  Pencil,
  RefreshCw,
  Send,
  Target,
  Timer,
  UserPlus,
  Users,
} from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Alert, Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { NO_FILTERS, SKIP_LABELS, type RecipientFilters } from "@/lib/telegram-recipients";
import {
  isPdfJob,
  reportLanguages,
  reportTypes,
  reportsApi,
  runStatus,
  timeZones,
  weekDays,
  type ReportJob,
  type ReportLanguage,
  type ReportOverview,
} from "@/lib/telegramReports";
import { TelegramDailyPlans } from "./TelegramDailyPlans";
import { IncentivesPanel } from "./TelegramIncentives";
import { ReportsOverviewPanel, type ReportsTab } from "./TelegramReportsOverview";
import { ReportHistoryPanel, ReportPreviewPanel, RoleChip, when } from "./TelegramReportsPreview";
import { WeeklyPdfPanel } from "./TelegramWeeklyPdf";
import { RecipientsTab } from "./recipients/RecipientsTab";

type Tab = ReportsTab;

const tabs: { key: Tab; label: string; short?: string; icon: typeof Users }[] = [
  { key: "overview", label: "Обзор", icon: LayoutGrid },
  { key: "schedule", label: "Расписание", icon: CalendarClock },
  { key: "recipients", label: "Получатели", icon: Users },
  { key: "preview", label: "Предпросмотр и тест", short: "Тест", icon: Eye },
  { key: "pdf", label: "Отчёты PDF", icon: FileText },
  { key: "incentives", label: "Акции", icon: Gift },
  { key: "dailyplan", label: "Дневной план", icon: Target },
  { key: "history", label: "История", icon: History },
];

/** На телефоне — чипы в другом порядке: сначала то, что открывают каждый день. */
const mobileOrder: Tab[] = ["recipients", "schedule", "preview", "history", "incentives", "overview", "pdf", "dailyplan"];

/** Рассылка включена: аккаунт выбран и подключён, включён хотя бы один запуск. */
function sendingState(data: ReportOverview): { on: boolean; label: string } {
  const account = data.accounts.find((a) => a.id === data.settings.accountId);
  if (!account) return { on: false, label: "Аккаунт не выбран" };
  if (account.status !== "Connected") return { on: false, label: "Аккаунт не подключён" };
  if (!data.jobs.some((j) => j.enabled)) return { on: false, label: "Все запуски выключены" };
  return { on: true, label: "Рассылка включена" };
}

/**
 * «Настройки → Telegram → Автоматические отчёты»: строка «Отправка» (правка — в окне), счётчики за 7 дней (ведут в историю или к
 * получателям), вкладки: обзор, расписание запусков, получатели (фильтры, таблица по регионам, массовые действия, панель в 3 шага),
 * предпросмотр и тест, PDF, акции, дневной план, история. На телефоне — своя шапка, плитки и вкладки чипами. Права и области проверяет сервер.
 */
export function TelegramReports({ initial, initialTab, initialFilters = NO_FILTERS }: { initial: ReportOverview; initialTab?: string; initialFilters?: RecipientFilters }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [tab, setTab] = useState<Tab>(() => tabs.find((x) => x.key === initialTab)?.key ?? "overview");
  const [filters, setFilters] = useState(initialFilters);
  const [history, setHistory] = useState<{ status: string; key: number }>({ status: "", key: 0 });
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [addSignal, setAddSignal] = useState(0);
  const reload = async () => setData(await reportsApi.overview());
  const state = sendingState(data);
  const account = data.accounts.find((a) => a.id === data.settings.accountId);
  const enabledJobs = data.jobs.filter((j) => j.enabled).length;

  // Вкладка — в адресе (фильтры «Получателей» дописывает сама вкладка).
  useEffect(() => {
    if (tab === "recipients") return;
    const url = `${window.location.pathname}?tab=${tab}`;
    if (`${window.location.pathname}${window.location.search}` !== url) window.history.replaceState(window.history.state, "", url);
  }, [tab]);

  const open = (next: Tab) => {
    setTab(next);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };
  const toHistory = (status: string) => {
    setHistory((h) => ({ status, key: h.key + 1 }));
    open("history");
  };
  const toRecipients = (patch: Partial<RecipientFilters>) => {
    setFilters({ ...NO_FILTERS, ...patch });
    setHistory((h) => ({ ...h, key: h.key + 1 }));
    open("recipients");
  };
  const badge = (key: Tab) =>
    key === "recipients" ? String(data.recipients.length) : key === "schedule" ? t("{n} из {total}", { n: enabledJobs, total: data.jobs.length }) : null;

  const content = (
    <>
      {tab === "overview" && <ReportsOverviewPanel data={data} onTab={open} />}
      {tab === "schedule" && <Schedule data={data} onSaved={(job) => setData((d) => ({ ...d, jobs: d.jobs.map((j) => (j.code === job.code ? job : j)) }))} />}
      {tab === "preview" && <ReportPreviewPanel data={data} />}
      {tab === "pdf" && <WeeklyPdfPanel data={data} onSaved={(weeklyPdf) => setData((d) => ({ ...d, weeklyPdf }))} />}
      {tab === "incentives" && <IncentivesPanel data={data} onTab={open} />}
      {tab === "dailyplan" && <TelegramDailyPlans canUseSales={data.canUseSales} />}
      {tab === "history" && <ReportHistoryPanel key={history.key} data={data} onChanged={reload} initialStatus={history.status} />}
    </>
  );

  return (
    <>
      <div className="max-lg:hidden">
        <PageHeader
          title={
            <span className="flex flex-wrap items-center gap-3">
              {t("Автоматические отчёты")}
              <span
                className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-[11px] font-bold ${state.on ? "bg-ok-soft text-ok" : "bg-warn-soft text-warn"}`}
              >
                <span aria-hidden className={`size-1.5 rounded-full ${state.on ? "bg-ok" : "bg-warn"}`} />
                {t(state.label)}
              </span>
            </span>
          }
          subtitle={t("Отчёты по расписанию в Telegram для РМ, СВ и ТП по оргструктуре «Продаж»: семь — картинками, восьмой — еженедельный PDF, девятый — накопительная акция")}
          back="/settings/telegram"
          breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Настройки"), href: "/settings" }, { label: "Telegram", href: "/settings/telegram" }, { label: t("Автоматические отчёты") }]}
        />
      </div>
      <PageBody>
        {/* ---------- телефон и планшет: своя шапка ---------- */}
        <header className="-mx-4 -mt-3.5 mb-3 flex min-h-14 items-center gap-3 px-4 pt-[env(safe-area-inset-top)] sm:-mx-6 sm:px-6 md:-mt-[18px] lg:hidden">
          <Link href="/settings/telegram" aria-label={t("Назад")} className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink">
            <ArrowLeft className="size-5" />
          </Link>
          <button type="button" onClick={() => setSettingsOpen(true)} className="min-w-0 flex-1 text-center" aria-label={t("Настройки отправки")}>
            <span className="block text-[15px] font-bold text-ink">{t("Автоотчёты")}</span>
            <span className="flex items-center justify-center gap-1.5 truncate text-[11px] text-ink-3">
              <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${state.on ? "bg-ok" : "bg-warn"}`} />
              {state.on ? t("включены") : t(state.label).toLowerCase()}
              {account?.username ? ` · @${account.username}` : ""}
            </span>
          </button>
          <button
            type="button"
            onClick={() => {
              setTab("recipients");
              setAddSignal((n) => n + 1);
            }}
            disabled={!data.canUseSales || !data.settings.accountId}
            aria-label={t("Добавить получателя")}
            className="grid size-10 shrink-0 place-items-center rounded-full bg-accent text-white disabled:opacity-40"
          >
            <UserPlus className="size-5" />
          </button>
        </header>

        {!data.telegramEnabled && (
          <Alert tone="info">
            {t("Telegram выключен в этой среде (Telegram:Enabled=false): аккаунт работает на сервере OneBase. Здесь можно настроить отчёты и посмотреть картинки, по расписанию отсюда ничего не уходит.")}
          </Alert>
        )}
        {!data.canUseSales && <Alert tone="warn">{t("Нет права на данные продаж (sales.read): регионы, получатели и отчёты на реальных данных недоступны.")}</Alert>}

        <SendingRow data={data} onEdit={() => setSettingsOpen(true)} />
        <Counters data={data} onHistory={toHistory} onRecipients={toRecipients} />

        {/* ---------- вкладки ---------- */}
        <nav aria-label={t("Разделы автоотчётов")} className="mt-5 flex gap-1 overflow-x-auto border-b border-line max-lg:hidden">
          {tabs.map((item) => {
            const on = tab === item.key;
            const b = badge(item.key);
            return (
              <button
                key={item.key}
                type="button"
                aria-current={on ? "page" : undefined}
                onClick={() => open(item.key)}
                className={`-mb-px flex shrink-0 items-center gap-1.5 whitespace-nowrap border-b-2 px-2 py-2.5 text-[13px] font-semibold 2xl:gap-2 2xl:px-3 2xl:text-[14px] ${on ? "border-accent text-ink" : "border-transparent text-ink-2 hover:text-ink"}`}
              >
                <item.icon aria-hidden className="hidden size-4 2xl:block" />
                {t(item.label)}
                {b && <span className={`rounded-md px-1.5 py-px text-[11px] font-bold tabular-nums ${on ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}>{b}</span>}
              </button>
            );
          })}
        </nav>
        <nav aria-label={t("Разделы автоотчётов")} className="no-scrollbar -mx-4 mt-3 flex gap-2 overflow-x-auto px-4 pb-1 sm:-mx-6 sm:px-6 lg:hidden">
          {mobileOrder.map((key) => {
            const item = tabs.find((x) => x.key === key)!;
            const on = tab === key;
            const b = badge(key);
            return (
              <button
                key={key}
                type="button"
                aria-current={on ? "page" : undefined}
                onClick={() => open(key)}
                className={`flex h-10 shrink-0 items-center gap-2 whitespace-nowrap rounded-[12px] px-3.5 text-[14px] font-semibold ${on ? "bg-accent text-white" : "border border-line bg-surface text-ink"}`}
              >
                <item.icon aria-hidden className="size-4" />
                {t(item.short ?? item.label)}
                {b && <span className={`text-[12px] tabular-nums ${on ? "text-white/80" : "text-ink-4"}`}>{key === "schedule" ? `${enabledJobs}/${data.jobs.length}` : b}</span>}
              </button>
            );
          })}
        </nav>

        <div className="mt-4">
          {tab === "recipients" ? (
            <RecipientsTab
              key={history.key}
              data={data}
              setData={setData}
              reload={reload}
              initialFilters={filters}
              onSchedule={() => open("schedule")}
              addSignal={addSignal}
            />
          ) : (
            <section className="rounded-[16px] border border-line bg-surface p-4 sm:p-5">{content}</section>
          )}
        </div>
      </PageBody>
      <SettingsSheet data={data} open={settingsOpen} onClose={() => setSettingsOpen(false)} onSaved={reload} />
    </>
  );
}

/** Строка «Отправка»: аккаунт и текущие настройки чипами; «Изменить» — окно с полями. */
function SendingRow({ data, onEdit }: { data: ReportOverview; onEdit: () => void }) {
  const t = useT();
  const s = data.settings;
  const account = data.accounts.find((a) => a.id === s.accountId);
  const lang = reportLanguages.find((l) => l.code === s.language)?.label ?? s.language;
  const chips = [
    { icon: Languages, text: `${t("Картинки")}: ${t(lang).toLowerCase()}` },
    { icon: CalendarX, text: t("Без закупа: {n} дн.", { n: s.noPurchaseDays }) },
    { icon: RefreshCw, text: s.refreshBeforeSend ? t("Обновлять Linko перед отправкой") : t("Без обновления Linko") },
    { icon: Palette, text: "PNG 1920×1080" },
  ];
  return (
    <div className="mt-4 flex items-center gap-3 rounded-[16px] border border-line bg-surface px-4 py-3 max-lg:hidden">
      <span aria-hidden className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-accent-soft text-accent-strong">
        <Send className="size-[18px]" />
      </span>
      <span className="min-w-0 shrink-0">
        <span className="block text-[11px] text-ink-4">{t("Отправка")}</span>
        <span className={`block max-w-[240px] truncate text-[14px] font-bold ${account ? "text-ink" : "text-warn"}`}>
          {account ? `${account.name}${account.username ? ` · @${account.username}` : ""}` : t("Аккаунт не выбран")}
        </span>
      </span>
      <span className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
        {chips.map((c, i) => (
          <span
            key={c.text}
            className={`h-[30px] items-center gap-1.5 whitespace-nowrap rounded-lg border border-line bg-muted px-2.5 text-[12px] font-medium text-ink-2 ${i >= 2 ? "hidden 2xl:flex" : "flex"}`}
          >
            <c.icon aria-hidden className="size-[13px] shrink-0 text-ink-3" />
            {c.text}
          </span>
        ))}
        <button type="button" onClick={onEdit} title={chips.slice(2).map((c) => c.text).join(" · ")} className="flex h-[30px] items-center rounded-lg border border-line bg-muted px-2.5 text-[12px] font-semibold text-ink-3 2xl:hidden">
          +2
        </button>
      </span>
      <button type="button" onClick={onEdit} className="flex h-9 shrink-0 items-center gap-1.5 rounded-[10px] border border-line-strong bg-surface px-3 text-[13px] font-semibold text-ink hover:bg-muted">
        <Pencil aria-hidden className="size-4" />
        {t("Изменить")}
      </button>
    </div>
  );
}

/** Окно «Отправка»: аккаунт, язык картинок, порог «без закупа», обновление Linko перед отправкой. */
function SettingsSheet({ data, open, onClose, onSaved }: { data: ReportOverview; open: boolean; onClose: () => void; onSaved: () => Promise<void> }) {
  const t = useT();
  const s = data.settings;
  const pick = () => ({ accountId: s.accountId ?? "", language: s.language, noPurchaseDays: s.noPurchaseDays, refreshBeforeSend: s.refreshBeforeSend });
  const [form, setForm] = useState(pick);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [about, setAbout] = useState(false);
  useEffect(() => {
    if (open) {
      setForm(pick());
      setError(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);
  const dirty = JSON.stringify(form) !== JSON.stringify(pick());
  const account = data.accounts.find((a) => a.id === form.accountId);
  const edit = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  const save = async () => {
    if (s.accountId && form.accountId !== s.accountId && data.recipients.length > 0 && !window.confirm(t("Чаты другого аккаунта уберутся у получателей. Сменить аккаунт?"))) return;
    setBusy(true);
    setError(null);
    try {
      await reportsApi.saveSettings({ ...form, accountId: form.accountId || null });
      await onSaved();
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t("Отправка")}
      subtitle={t("Аккаунт и параметры автоотчётов")}
      footer={
        <div className="flex justify-end gap-2">
          <button className={button} onClick={onClose} disabled={busy}>
            {t("Отмена")}
          </button>
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Аккаунт Telegram")}</span>
          <select className={field} value={form.accountId} onChange={(e) => edit({ accountId: e.target.value })}>
            <option value="">{t("Не выбран")}</option>
            {data.accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
                {a.username ? ` · @${a.username}` : ""}
                {a.status !== "Connected" ? ` · ${t("не подключён")}` : ""}
              </option>
            ))}
          </select>
        </label>
        {account && account.status !== "Connected" && <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Аккаунт не подключён — отчёты не уйдут, пока его не подключить на странице Telegram.")}</p>}
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Язык картинок")}</span>
            <select className={field} value={form.language} onChange={(e) => edit({ language: e.target.value as ReportLanguage })}>
              {reportLanguages.map((l) => (
                <option key={l.code} value={l.code}>
                  {t(l.label)}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Без закупа, дней")}</span>
            <input type="number" min={1} max={365} className={field} value={form.noPurchaseDays} onChange={(e) => edit({ noPurchaseDays: Math.min(365, Math.max(1, Number(e.target.value) || 1)) })} />
          </label>
        </div>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={form.refreshBeforeSend} onChange={(e) => edit({ refreshBeforeSend: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
          {t("Обновлять данные Linko перед отправкой")}
        </label>
        <button type="button" aria-expanded={about} onClick={() => setAbout((v) => !v)} className="flex items-center gap-1.5 text-[13px] font-semibold text-accent-strong">
          <Info aria-hidden className="size-4" />
          {t("Как это работает")}
        </button>
        {about && (
          <Note>
            {t(
              "Отчёты уходят с подключённого аккаунта OneBase. Картинки PNG 16:9 (1920×1080) рисует сервер, полоса и метка роли: РМ — синие, СВ — зелёные, ТП — оранжевые; ниже 80% плана — красным. Перед запуском данные Linko обновляются (если последнее обновление старше 10 минут); не обновились — отчёт уходит с пометкой о времени данных.",
            )}
          </Note>
        )}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      </div>
    </Sheet>
  );
}

/** Счётчики за 7 дней: клик — история с этим статусом; «Пропущено» из-за чатов — к получателям без чата. */
function Counters({ data, onHistory, onRecipients }: { data: ReportOverview; onHistory: (status: string) => void; onRecipients: (patch: Partial<RecipientFilters>) => void }) {
  const t = useT();
  const m = data.metrics;
  const skips = (m.skippedBy ?? []).slice(0, 2).map((x) => `${t(SKIP_LABELS[x.key] ?? x.key)} · ${x.count}`).join(", ");
  const next = data.jobs.filter((j) => j.enabled && j.nextRunAt).sort((a, b) => a.nextRunAt!.localeCompare(b.nextRunAt!))[0];
  const noChat = (m.skippedBy ?? []).some((x) => x.key === "no_chat" && x.count > 0);
  const tiles = [
    { key: "sent", label: "Отправлено", short: "Отправлено", value: m.sent, icon: Send, square: "bg-ok-soft text-ok", tone: "text-ok", hint: t("за 7 дней"), go: () => onHistory("sent") },
    {
      key: "failed",
      label: "Ошибок",
      short: "Ошибок",
      value: m.failed,
      icon: CircleX,
      square: m.failed > 0 ? "bg-bad-soft text-bad" : "bg-muted text-ink-3",
      tone: m.failed > 0 ? "text-bad" : "text-ink",
      hint: m.failed > 0 ? t("{n} — посмотреть", { n: m.failed }) : t("всё доставлено"),
      go: () => onHistory("failed"),
    },
    { key: "unknown", label: "Неизвестно, дошёл ли", short: "Неизвестно", value: m.unknown, icon: CircleDashed, square: "bg-muted text-ink-3", tone: m.unknown > 0 ? "text-warn" : "text-ink", hint: "—", go: () => onHistory("unknown") },
    {
      key: "skipped",
      label: "Пропущено",
      short: "Пропущено",
      value: m.skipped,
      icon: CircleSlash,
      square: "bg-warn-soft text-warn",
      tone: "text-warn",
      hint: skips || "—",
      go: () => (noChat ? onRecipients({ issues: ["no_chat"] }) : onHistory("skipped")),
    },
    {
      key: "pending",
      label: "В очереди",
      short: "В очереди",
      value: m.pending,
      icon: Timer,
      square: "bg-accent-soft text-accent-strong",
      tone: "text-accent-strong",
      hint: next ? t("ближайшая — {time}", { time: next.time }) : "—",
      go: () => onHistory("pending"),
    },
  ];
  return (
    <>
      <div className="mt-3 grid grid-cols-3 gap-2.5 max-lg:hidden xl:grid-cols-5">
        {tiles.map((x) => (
          <button key={x.key} type="button" onClick={x.go} className="group flex items-center gap-3 rounded-[14px] border border-line bg-surface px-3.5 py-3 text-left hover:border-line-strong">
            <span aria-hidden className={`grid size-9 shrink-0 place-items-center rounded-[10px] ${x.square}`}>
              <x.icon className="size-[18px]" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-[11px] text-ink-3" title={`${t(x.label)} · ${t("7 дней")}`}>
                <span className="2xl:hidden">{t(x.short)}</span>
                <span className="hidden 2xl:inline">
                  {t(x.label)} · {t("7 дней")}
                </span>
              </span>
              <span className={`block text-[20px] font-bold leading-tight tabular-nums ${x.tone}`}>{x.value.toLocaleString("ru-RU")}</span>
              <span className="block truncate text-[10px] text-ink-4" title={x.hint}>
                {x.hint}
              </span>
            </span>
            <ChevronRight aria-hidden className="size-4 shrink-0 text-ink-4 group-hover:text-ink-2" />
          </button>
        ))}
      </div>
      <div className="grid grid-cols-4 gap-2 lg:hidden">
        {["sent", "skipped", "pending", "failed"].map((key) => {
          const x = tiles.find((tile) => tile.key === key)!;
          return (
            <button key={key} type="button" onClick={x.go} className="min-w-0 rounded-[14px] border border-line bg-surface px-2.5 py-2 text-left">
              <span className="block truncate text-[10px] text-ink-3">{t(x.short)}</span>
              <span className={`block text-[16px] font-bold tabular-nums ${x.key === "failed" ? x.tone : x.tone}`}>{x.value.toLocaleString("ru-RU")}</span>
            </button>
          );
        })}
      </div>
    </>
  );
}

// ---------- Расписание ----------

function Schedule({ data, onSaved }: { data: ReportOverview; onSaved: (job: ReportJob) => void }) {
  const t = useT();
  return (
    <div className="space-y-4">
      {reportTypes.map((type) => (
        <div key={type.key} className="rounded-xl border border-line">
          <div className="border-b border-line px-4 py-3">
            <h3 className="font-semibold text-ink">{t(type.title)}</h3>
            <p className="text-xs text-ink-3">{t(type.hint)}</p>
          </div>
          <div className="divide-y divide-line">
            {data.jobs
              .filter((j) => j.report === type.key)
              .map((job) => (
                <JobRow key={job.code} job={job} onSaved={onSaved} />
              ))}
          </div>
        </div>
      ))}
      <Note>
        {t(
          "Каждый запуск включается отдельно. Включение и смена времени действуют с этого момента: время, которое сегодня уже прошло, не догоняется. Сервер не работал во время запуска — отчёт отправляется, если с тех пор прошло не больше «догона», иначе запуск помечается «пропущен» в истории. Один и тот же отчёт одной области в один чат за день уходит один раз — даже при перезапуске и двух экземплярах сервера.",
        )}
      </Note>
    </div>
  );
}

/** Один запуск: включён ли, время, дни, пояс, догон пропущенного; следующий и последний запуск. Сохранение ничего не отправляет. */
function JobRow({ job, onSaved }: { job: ReportJob; onSaved: (job: ReportJob) => void }) {
  const t = useT();
  const pick = (j: ReportJob) => ({ enabled: j.enabled, time: j.time, timeZone: j.timeZone, days: j.days, catchUpMinutes: j.catchUpMinutes });
  const [form, setForm] = useState(() => pick(job));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dirty = JSON.stringify(form) !== JSON.stringify(pick(job));
  const zones = timeZones.includes(form.timeZone) ? timeZones : [form.timeZone, ...timeZones];

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await reportsApi.saveJob(job.code, form);
      setForm(pick(saved));
      onSaved(saved);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const toggleDay = (day: number) =>
    setForm((f) => ({ ...f, days: f.days.includes(day) ? f.days.filter((d) => d !== day) : [...f.days, day].sort((a, b) => a - b) }));
  const last = job.lastRun;

  return (
    <div className={`px-4 py-3 ${form.enabled ? "bg-accent-soft/15" : ""}`}>
      <div className="flex flex-wrap items-center gap-x-5 gap-y-3">
        <div className="flex min-w-[220px] flex-1 items-center gap-3">
          <Switch checked={form.enabled} onChange={(v) => setForm((f) => ({ ...f, enabled: v }))} label={t("Включить автоматическую отправку")} />
          <div className="min-w-0">
            <div className="font-medium text-ink">
              {job.time} — {t(job.slotTitle)}
            </div>
            <div className="mt-0.5 flex flex-wrap items-center gap-1">
              {job.roles.map((r) => (
                <RoleChip key={r} role={r} />
              ))}
              <span className="ml-1 text-xs text-ink-3">{t("получателей: {n}", { n: job.recipients })}</span>
            </div>
          </div>
        </div>
        <label className="block w-[130px]">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Время")}</span>
          <input type="time" className={field} value={form.time} onChange={(e) => setForm((f) => ({ ...f, time: e.target.value }))} />
        </label>
        <div>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Дни")}</span>
          {job.monthEnd || job.monthStart ? (
            <p className="flex h-9 items-center text-sm text-ink-2">
              {job.monthStart ? t("1-е число месяца (итог прошлого месяца)") : t("Последний рабочий день месяца (пн–сб)")}
            </p>
          ) : (
            <div className="flex gap-1">
              {weekDays.map((d) => {
                const on = form.days.includes(d.day);
                return (
                  <button
                    key={d.day}
                    type="button"
                    onClick={() => toggleDay(d.day)}
                    aria-pressed={on}
                    className={`h-9 min-w-8 rounded-lg px-1.5 text-xs font-semibold ${on ? "bg-accent text-white" : "border border-line bg-surface text-ink-3 hover:bg-muted"}`}
                  >
                    {t(d.label)}
                  </button>
                );
              })}
            </div>
          )}
        </div>
        <label className="block w-[170px]">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Часовой пояс")}</span>
          <select className={field} value={form.timeZone} onChange={(e) => setForm((f) => ({ ...f, timeZone: e.target.value }))}>
            {zones.map((z) => (
              <option key={z} value={z}>
                {z}
              </option>
            ))}
          </select>
        </label>
        <label className="block w-[120px]" title={t("Сколько минут после времени запуска отчёт ещё отправляется, если сервер был выключен")}>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Догон, мин")}</span>
          <input
            type="number"
            min={0}
            max={1440}
            className={field}
            value={form.catchUpMinutes}
            onChange={(e) => setForm((f) => ({ ...f, catchUpMinutes: Math.min(1440, Math.max(0, Number(e.target.value) || 0)) }))}
          />
        </label>
        <div className="flex gap-2 self-end">
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
          {dirty && (
            <button className={button} onClick={() => setForm(pick(job))} disabled={busy}>
              {t("Отмена")}
            </button>
          )}
        </div>
      </div>
      <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-xs text-ink-2">
        <span>
          {job.enabled && job.nextRunAt
            ? t("Следующий запуск: {time}", { time: when(job.nextRunAt) })
            : job.enabled
              ? t("Дни не выбраны — отчёт не уходит.")
              : t("Выключен — сохранение ничего не отправляет.")}
        </span>
        {last && (
          <span>
            {t("Последний: {time}", { time: when(last.startedAt) })} · <span className={runStatus[last.status].className}>{t(runStatus[last.status].label)}</span>
            {last.planned > 0 && ` · ${t("отправлено {sent} из {planned}", { sent: last.sent, planned: last.planned })}`}
            {last.error && <span className="text-ink-3"> · {t(last.error)}</span>}
          </span>
        )}
        {form.enabled && !job.enabled && <span className="text-warn">{t("После сохранения отчёт уйдёт в ближайшее время по расписанию, а не сразу.")}</span>}
        {isPdfJob(job) && <span className="text-ink-3">{t("PDF за последнюю полную неделю пн–сб; предпросмотр, формулы и тест — на вкладке «Отчёты PDF».")}</span>}
      </div>
      {error && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-xs text-bad">{error}</p>}
    </div>
  );
}

export function Switch({ checked, onChange, disabled, label }: { checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors disabled:opacity-40 ${checked ? "bg-accent" : "bg-muted ring-1 ring-inset ring-line"}`}
    >
      <span className={`inline-block size-4 rounded-full bg-white shadow transition-transform ${checked ? "translate-x-[18px]" : "translate-x-0.5"}`} />
    </button>
  );
}
