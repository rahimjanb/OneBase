"use client";

import { useState } from "react";
import { Download, Eye, Loader2, RotateCcw, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { chatTypeLabel } from "@/lib/telegram";
import {
  deliveryStatus,
  reportLanguages,
  sourceState,
  weeklyPdfApi,
  weeklySources,
  type ReportLanguage,
  type ReportOverview,
  type ReportSendResult,
  type WeeklyPdfFormulas,
  type WeeklyPdfInput,
  type WeeklyPdfKind,
  type WeeklyPdfPreview,
  type WeeklyPdfRole,
  type WeeklyPdfSettings,
} from "@/lib/telegramReports";
import { Switch } from "./TelegramReports";
import { when } from "./TelegramReportsPreview";

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString("ru-RU");

const pdfRoles: { key: WeeklyPdfRole; label: string; hint: string }[] = [
  { key: "rop", label: "РОП", hint: "Вся страна: регионы, СВР, план и прогноз, дилеры — 3 страницы" },
  { key: "rm", label: "РМ", hint: "Регионы направления — 3 страницы" },
  { key: "sv", label: "СВ", hint: "Регион и команда ТП: динамика, рейтинг, КПУ, линейки, клиенты, акции, ДМП, дилер, цели — 10 страниц; карточки ТП — отдельным файлом" },
  { key: "tp", label: "ТП", hint: "Личная карточка ТП: итоги недели и цели на неделю — 2 страницы, только его цифры" },
];

/**
 * «Отчёты PDF»: еженедельный PDF — настройки (только при полных данных, карточки ТП для СВ, суммы КПУ и DSO, формулы целей недели),
 * предпросмотр страницами, скачивание и тест в выбранный чат на реальных данных. По расписанию PDF уходит получателям, подписанным на
 * «8. Еженедельный PDF» (вкладки «Расписание» и «Получатели»).
 */
export function WeeklyPdfPanel({ data, onSaved }: { data: ReportOverview; onSaved: (s: WeeklyPdfSettings) => void }) {
  return (
    <div className="space-y-6">
      <WeeklyPdfSettingsForm settings={data.weeklyPdf} onSaved={onSaved} />
      <WeeklyPdfPreviewPanel data={data} />
    </div>
  );
}

type FormulaForm = Omit<WeeklyPdfFormulas, "returnShare15To30" | "returnShare30Plus"> & { returnPct15To30: number; returnPct30Plus: number };

const formOf = (f: WeeklyPdfFormulas): FormulaForm => ({
  goalAkbFactor: f.goalAkbFactor,
  goalPromoFactor: f.goalPromoFactor,
  goalPromoMin: f.goalPromoMin,
  returnPct15To30: Math.round(f.returnShare15To30 * 100),
  returnPct30Plus: Math.round(f.returnShare30Plus * 100),
  sleepFromDays: f.sleepFromDays,
  sleepDays: f.sleepDays,
  deficitDays: f.deficitDays,
});

const formulasOf = (f: FormulaForm): WeeklyPdfFormulas => ({
  goalAkbFactor: f.goalAkbFactor,
  goalPromoFactor: f.goalPromoFactor,
  goalPromoMin: f.goalPromoMin,
  returnShare15To30: f.returnPct15To30 / 100,
  returnShare30Plus: f.returnPct30Plus / 100,
  sleepFromDays: f.sleepFromDays,
  sleepDays: f.sleepDays,
  deficitDays: f.deficitDays,
});

/** Настройки PDF: только при полных данных, карточки ТП для СВ, суммы КПУ и DSO, формулы целей недели (по умолчанию — формулы образцов). */
function WeeklyPdfSettingsForm({ settings, onSaved }: { settings: WeeklyPdfSettings; onSaved: (s: WeeklyPdfSettings) => void }) {
  const t = useT();
  const pick = (s: WeeklyPdfSettings) => ({ requireCompleteData: s.requireCompleteData, svTpCards: s.svTpCards, includeMoney: s.includeMoney, ...formOf(s.formulas) });
  const [form, setForm] = useState(() => pick(settings));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const defaults = formOf(settings.defaults);
  const dirty = JSON.stringify(form) !== JSON.stringify(pick(settings));
  const edit = (patch: Partial<typeof form>) => (setForm((f) => ({ ...f, ...patch })), setSaved(false));

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const { requireCompleteData, svTpCards, includeMoney, ...formulas } = form;
      const result = await weeklyPdfApi.saveSettings({ requireCompleteData, svTpCards, includeMoney, formulas: formulasOf(formulas) });
      setForm(pick(result));
      onSaved(result);
      setSaved(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const num = (key: keyof FormulaForm, label: string, step: number, min: number, max: number) => (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-ink-2">{t(label)}</span>
      <input type="number" step={step} min={min} max={max} className={field} value={form[key]} onChange={(e) => edit({ [key]: Number(e.target.value) || 0 })} />
      <span className={`mt-1 block text-xs ${form[key] !== defaults[key] ? "text-warn" : "text-ink-3"}`}>{t("по образцам: {v}", { v: defaults[key] })}</span>
    </label>
  );

  const toggle = (key: "requireCompleteData" | "svTpCards" | "includeMoney", label: string, hint: string) => (
    <label className="flex items-start gap-3 text-sm text-ink">
      <Switch checked={form[key]} onChange={(v) => edit({ [key]: v })} label={t(label)} />
      <span>
        <span className="font-medium">{t(label)}</span>
        <span className="block text-xs text-ink-3">{t(hint)}</span>
      </span>
    </label>
  );

  return (
    <div className="rounded-xl border border-line">
      <div className="border-b border-line px-4 py-3">
        <h3 className="font-semibold text-ink">{t("Настройки PDF")}</h3>
        <p className="text-xs text-ink-3">{t("Действуют для предпросмотра, теста и отправки по расписанию.")}</p>
      </div>
      <div className="space-y-5 p-4">
        <div className="grid gap-4 lg:grid-cols-3">
          {toggle(
            "requireCompleteData",
            "Только при полных данных",
            "Заказы и визиты Linko обновлены после конца недели, KPI Linko по области загружены. Иначе PDF не уходит, а в истории — «пропущен» с причиной.",
          )}
          {toggle("svTpCards", "СВ получает карточки своих ТП", "Вместе с отчётом СВ — вторым файлом карточки всех ТП региона.")}
          {toggle("includeMoney", "Суммы КПУ и DSO в PDF", "Премии КПУ и деньги дилера (DSO) — как в образцах; выключено — вместо сумм «—».")}
        </div>

        <div>
          <div className="text-sm font-medium text-ink">{t("Цели следующей недели")}</div>
          <div className="mt-2 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {num("goalAkbFactor", "АКБ: ТТ прошлой недели ×", 0.05, 0.5, 5)}
            {num("goalPromoFactor", "ТТ по акциям: прошлой недели ×", 0.05, 0.5, 5)}
            {num("goalPromoMin", "ТТ по акциям: не меньше", 1, 0, 1000)}
            {num("returnPct15To30", "Вернуть спящих 15–30 дней, %", 5, 0, 100)}
            {num("returnPct30Plus", "Вернуть спящих 30+ дней, %", 5, 0, 100)}
            {num("sleepFromDays", "Спящая ТТ: без заказа от, дней", 1, 1, 90)}
            {num("sleepDays", "Граница «30+», дней", 1, 2, 180)}
            {num("deficitDays", "Дефицит у дилера: меньше, дней продаж", 1, 1, 60)}
          </div>
          <p className="mt-2 text-xs text-ink-3">{t("Продажи недели = план месяца в Linko ÷ рабочие дни месяца × рабочие дни недели (пн–сб без праздников) — не настраивается.")}</p>
        </div>

        {saved && !dirty && <p className="text-sm text-ok">{t("Сохранено.")}</p>}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
          {dirty && (
            <button className={button} onClick={() => setForm(pick(settings))} disabled={busy}>
              {t("Отмена")}
            </button>
          )}
          <button className={button} onClick={() => edit(defaults)} disabled={busy || JSON.stringify(formOf(formulasOf(form))) === JSON.stringify(defaults)}>
            <RotateCcw className="size-4" />
            {t("Формулы образцов")}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Предпросмотр, скачивание и тест: роль и область, неделя (любой её день), язык; страницы картинками и состояние данных. */
function WeeklyPdfPreviewPanel({ data }: { data: ReportOverview }) {
  const t = useT();
  const firstRegion = data.regions[0];
  const [input, setInput] = useState<WeeklyPdfInput>(() => ({
    role: "sv",
    directionId: data.directions[0]?.id ?? null,
    regionId: firstRegion?.id ?? null,
    agentId: firstRegion?.agents[0]?.id ?? null,
    date: null,
  }));
  const [kind, setKind] = useState<WeeklyPdfKind>("team");
  const [language, setLanguage] = useState<ReportLanguage>(data.settings.language);
  const [chatId, setChatId] = useState("");
  const [preview, setPreview] = useState<WeeklyPdfPreview | null>(null);
  const [result, setResult] = useState<ReportSendResult | null>(null);
  const [busy, setBusy] = useState<"preview" | "download" | "test" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const region = data.regions.find((r) => r.id === input.regionId);
  const shownKind: WeeklyPdfKind = input.role === "tp" ? "cards" : input.role === "sv" ? kind : "team";
  const ready =
    data.canUseSales && (input.role === "rop" || (input.role === "rm" ? !!input.directionId : input.role === "sv" ? !!input.regionId : !!input.agentId));

  const run = async (action: "preview" | "download" | "test") => {
    if (action === "test") {
      const chat = data.chats.find((c) => c.id === chatId);
      if (!window.confirm(t("Отправить тестовый PDF на реальных данных в «{chat}»? Получателям он не уйдёт.", { chat: chat?.title ?? "" }))) return;
    }

    setBusy(action);
    setError(null);
    setResult(null);
    try {
      if (action === "preview") {
        setPreview(await weeklyPdfApi.preview(input, language, shownKind));
      } else if (action === "download") {
        await weeklyPdfApi.download(input, language, shownKind, preview?.fileName ?? `weekly-${input.role}.pdf`);
      } else {
        const sent = await weeklyPdfApi.test(input, language, chatId);
        if ("conflict" in sent) throw new Error(sent.conflict);
        setResult(sent);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const change = (patch: Partial<WeeklyPdfInput>) => (setInput((i) => ({ ...i, ...patch })), setPreview(null));
  const pickRegion = (regionId: string) => change({ regionId: regionId || null, agentId: data.regions.find((r) => r.id === regionId)?.agents[0]?.id ?? null });

  return (
    <div className="grid gap-6 xl:grid-cols-[420px_minmax(0,1fr)]">
      <div>
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Для кого")}</span>
        <div className="grid grid-cols-4 gap-1 rounded-lg bg-muted p-1">
          {pdfRoles.map((r) => (
            <button
              key={r.key}
              type="button"
              onClick={() => change({ role: r.key })}
              title={t(r.hint)}
              className={`rounded-md px-2 py-1.5 text-xs font-semibold ${input.role === r.key ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
            >
              {t(r.label)}
            </button>
          ))}
        </div>
        <span className="mt-1 block text-xs text-ink-3">{t(pdfRoles.find((r) => r.key === input.role)!.hint)}</span>

        {input.role === "rm" && (
          <label className="mt-3 block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Направление")}</span>
            <select className={field} value={input.directionId ?? ""} onChange={(e) => change({ directionId: e.target.value || null })}>
              {!input.directionId && <option value="">{t("Выберите направление")}</option>}
              {data.directions.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                  {d.manager ? ` · ${d.manager}` : ""}
                </option>
              ))}
            </select>
          </label>
        )}
        {(input.role === "sv" || input.role === "tp") && (
          <label className="mt-3 block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Регион")}</span>
            <select className={field} value={input.regionId ?? ""} onChange={(e) => pickRegion(e.target.value)}>
              {!region && <option value="">{t("Выберите регион")}</option>}
              {data.regions.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
            {input.role === "sv" && <span className="mt-1 block text-xs text-ink-3">{t("Команда — все ТП региона в «Продажах»: {n}", { n: region?.agents.length ?? 0 })}</span>}
          </label>
        )}
        {input.role === "tp" && (
          <label className="mt-3 block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Торговый представитель")}</span>
            <select className={field} value={input.agentId ?? ""} onChange={(e) => change({ agentId: e.target.value ? Number(e.target.value) : null })} disabled={!region}>
              {!input.agentId && <option value="">{t("Выберите ТП")}</option>}
              {region?.agents.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {input.role === "sv" && (
          <div className="mt-3 grid grid-cols-2 gap-1 rounded-lg bg-muted p-1">
            {(["team", "cards"] as const).map((k) => (
              <button
                key={k}
                type="button"
                onClick={() => (setKind(k), setPreview(null))}
                className={`rounded-md px-2 py-1.5 text-xs font-semibold ${kind === k ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
              >
                {k === "team" ? t("Отчёт СВ") : t("Карточки ТП")}
              </button>
            ))}
          </div>
        )}

        <div className="mt-3 grid grid-cols-2 gap-3">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Неделя (любой её день)")}</span>
            <input type="date" className={field} value={input.date ?? ""} onChange={(e) => change({ date: e.target.value || null })} />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Язык")}</span>
            <select className={field} value={language} onChange={(e) => (setLanguage(e.target.value as ReportLanguage), setPreview(null))}>
              {reportLanguages.map((l) => (
                <option key={l.code} value={l.code}>
                  {t(l.label)}
                </option>
              ))}
            </select>
          </label>
        </div>
        <p className="mt-1 text-xs text-ink-3">{t("Пусто — текущая неделя по отчётный день «Продаж». Неделя — пн–сб; воскресенье относится к прошедшей неделе.")}</p>

        <div className="mt-4 flex flex-wrap gap-2">
          <button className={primary} onClick={() => run("preview")} disabled={busy !== null || !ready}>
            {busy === "preview" ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
            {t("Предпросмотр")}
          </button>
          <button className={button} onClick={() => run("download")} disabled={busy !== null || !ready}>
            {busy === "download" ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
            {t("Скачать PDF")}
          </button>
        </div>
        {busy && busy !== "test" && <p className="mt-2 text-xs text-ink-3">{t("Первая сборка после запуска сервера — до 30–40 секунд, дальше — секунды.")}</p>}

        <div className="mt-6 border-t border-line pt-4">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Тестовая отправка")}</span>
          <select className={field} value={chatId} onChange={(e) => setChatId(e.target.value)} disabled={!data.settings.accountId}>
            <option value="">{data.settings.accountId ? t("Выберите чат или группу") : t("Сначала выберите аккаунт Telegram")}</option>
            {data.chats.map((c) => (
              <option key={c.id} value={c.id} disabled={!c.canSend}>
                {c.title} · {t(chatTypeLabel[c.type])}
                {c.linkedUser ? ` · ${c.linkedUser}` : ""}
              </option>
            ))}
          </select>
          <button className={`${button} mt-3`} onClick={() => run("test")} disabled={busy !== null || !chatId || !ready}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            {t("Отправить тест в этот чат")}
          </button>
          <p className="mt-2 text-xs text-ink-3">
            {t("Тест уходит только в выбранный чат с пометкой «ТЕСТОВАЯ ОТПРАВКА» — так же, как получателю: подпись и PDF документом (у СВ — и карточки ТП, если включено).")}
          </p>
          {!data.telegramEnabled && <p className="mt-1 text-xs text-ink-3">{t("В этой среде Telegram выключен — тестовая отправка вернёт ошибку.")}</p>}
        </div>

        {result && (
          <p className={`mt-4 rounded-lg px-3 py-2 text-sm ${result.status === "sent" ? "bg-ok-soft text-ok" : result.status === "skipped" ? "bg-muted text-ink-2" : "bg-bad-soft text-bad"}`}>
            {result.status === "sent"
              ? t("Отправлено в «{chat}»: {pages} стр.", { chat: result.recipientTitle, pages: result.pages })
              : `${t(deliveryStatus[result.status].label)}: ${t(result.error ?? "")}`}
          </p>
        )}
        {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

        {preview && <PreviewData preview={preview} />}

        <Note>
          {t(
            "Только реальные данные OneBase: продажи и визиты Linko по правилам «Продаж», KPI и планы Linko, акции, остатки, аутсток, DSO. Нет данных — «нет данных», а не ноль. Деньги КПУ и DSO в предпросмотре видны только с правом на финансы. Предпросмотр и скачивание ничего не отправляют.",
          )}
        </Note>
      </div>

      <div className="min-w-0">
        {preview ? (
          <div>
            <p className="rounded-lg bg-muted px-3 py-2 text-sm text-ink">
              {t("Неделя {from}–{to}", { from: day(preview.periodStart), to: day(preview.periodEnd) })} · {preview.scopeName}
              {preview.supervisorName ? ` · ${preview.supervisorName}` : ""}
              <span className="text-ink-3"> · {t("данные по {date}", { date: day(preview.dataThrough) })}</span>
            </p>
            <p className="mt-2 text-xs text-ink-3">
              {t("Страниц: {n}", { n: preview.pages.length })} · {preview.fileName}
            </p>
            <div className={`mt-3 grid gap-4 ${shownKind === "cards" ? "sm:grid-cols-2 2xl:grid-cols-3" : "2xl:grid-cols-2"}`}>
              {preview.pages.map((src, i) => (
                // eslint-disable-next-line @next/next/no-img-element -- страница из ответа сервера (data:), оптимизатор Next не нужен
                <img key={i} src={src} alt={t("Страница {n}", { n: i + 1 })} className="w-full rounded-xl border border-line shadow-sm" />
              ))}
            </div>
          </div>
        ) : (
          <div className="grid h-full min-h-[240px] place-items-center rounded-xl border border-dashed border-line text-sm text-ink-3">
            {t("Нажмите «Предпросмотр» — страницы PDF появятся здесь.")}
          </div>
        )}
      </div>
    </div>
  );
}

/** Состояние данных отчёта: полные ли, источники и их время, замечания. */
function PreviewData({ preview }: { preview: WeeklyPdfPreview }) {
  const t = useT();
  return (
    <div className="mt-4 rounded-xl border border-line p-3">
      {preview.incomplete.length > 0 ? (
        <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
          {t("Данные неполные: {list}. С настройкой «только при полных данных» такой PDF не уйдёт.", {
            list: preview.incomplete.map((k) => t(weeklySources[k] ?? k)).join(", "),
          })}
        </p>
      ) : (
        <p className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{t("Данные полные.")}</p>
      )}
      <ul className="mt-3 space-y-1 text-xs">
        {preview.sourceStatuses.map((s) => (
          <li key={s.key} className="flex justify-between gap-3">
            <span className="text-ink-2">{t(weeklySources[s.key] ?? s.key)}</span>
            <span className={sourceState[s.state].className}>
              {t(sourceState[s.state].label)}
              {s.asOf ? ` · ${when(s.asOf)}` : ""}
            </span>
          </li>
        ))}
      </ul>
      {preview.warnings.length > 0 && (
        <ul className="mt-3 space-y-1 border-t border-line pt-2 text-xs text-ink-2">
          {preview.warnings.map((w, i) => (
            <li key={i}>⚠ {w}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
