"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Check, Loader2, Pencil, Plus, X } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { RequestCategory } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

/**
 * Категории закупок: добавить, переименовать, отключить или включить. Категории не удаляются — по ним остаются старые заявки;
 * отключённую нельзя выбрать в новой позиции. Порядок — как в списке формы.
 */
type Settings = { reapprovalTolerancePercent: number; escalateHeadDays: number; escalateDirectorDays: number };

export function CategoriesView({ categories, settings }: { categories: RequestCategory[]; settings: Settings }) {
  const t = useT();
  const router = useRouter();
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<{ id: string; name: string } | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const run = async (key: string, action: () => Promise<unknown>) => {
    setBusy(key);
    setError(null);
    try {
      await action();
      router.refresh();
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return false;
    } finally {
      setBusy(null);
    }
  };

  const add = async () => {
    if (name.trim().length < 2) {
      setError(t("Название категории — от 2 символов."));
      return;
    }
    if (await run("add", () => bff("procurement/categories", { method: "POST", body: JSON.stringify({ name: name.trim() }) }))) setName("");
  };
  const update = (c: RequestCategory, patch: Partial<Pick<RequestCategory, "name" | "isActive">>) =>
    run(c.id, () => bff(`procurement/categories/${c.id}`, { method: "PUT", body: JSON.stringify({ name: patch.name ?? c.name, sortOrder: c.sortOrder, isActive: patch.isActive ?? c.isActive }) }));

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <ToleranceCard settings={settings} />
      <h2 className="pt-2 text-base font-semibold text-ink">{t("Категории закупок")}</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void add();
        }}
        className="flex gap-2 rounded-2xl border border-line bg-surface p-3"
      >
        <input value={name} onChange={(e) => setName(e.target.value)} maxLength={100} placeholder={t("Новая категория, например «Упаковка»")} aria-label={t("Новая категория")} className={field} />
        <button type="submit" disabled={busy !== null} className="inline-flex h-10 shrink-0 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
          {busy === "add" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Plus className="size-4" aria-hidden />}
          <span className="max-sm:sr-only">{t("Добавить")}</span>
        </button>
      </form>
      <ErrorNote text={error} />

      <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
        {categories.map((c) => (
          <li key={c.id} className={`flex items-center gap-3 px-4 py-3 ${c.isActive ? "" : "bg-muted/40"}`}>
            {editing?.id === c.id ? (
              <form
                onSubmit={async (e) => {
                  e.preventDefault();
                  if (await update(c, { name: editing.name.trim() })) setEditing(null);
                }}
                className="flex min-w-0 flex-1 items-center gap-2"
              >
                <input value={editing.name} onChange={(e) => setEditing({ id: c.id, name: e.target.value })} maxLength={100} autoFocus aria-label={t("Название категории")} className={field} />
                <button type="submit" aria-label={t("Сохранить")} className="grid size-9 shrink-0 place-items-center rounded-lg bg-accent text-white">
                  {busy === c.id ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />}
                </button>
                <button type="button" onClick={() => setEditing(null)} aria-label={t("Отмена")} className="grid size-9 shrink-0 place-items-center rounded-lg border border-line text-ink-3">
                  <X className="size-4" aria-hidden />
                </button>
              </form>
            ) : (
              <>
                <span className="min-w-0 flex-1">
                  <span className={`block truncate text-sm font-medium ${c.isActive ? "text-ink" : "text-ink-3"}`}>{c.name}</span>
                  <span className="text-xs text-ink-3">
                    {c.used > 0 ? t("позиций в заявках: {n}", { n: c.used }) : t("ещё не использовалась")}
                    {!c.isActive && ` · ${t("отключена")}`}
                  </span>
                </span>
                <button type="button" onClick={() => setEditing({ id: c.id, name: c.name })} aria-label={t("Переименовать «{name}»", { name: c.name })} className="grid size-9 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink">
                  <Pencil className="size-4" aria-hidden />
                </button>
                <label className="inline-flex cursor-pointer items-center gap-2 text-sm text-ink-2">
                  <input type="checkbox" checked={c.isActive} disabled={busy === c.id} onChange={(e) => void update(c, { isActive: e.target.checked })} className="peer sr-only" />
                  <span className="relative h-6 w-10 rounded-full bg-line-strong transition-colors peer-checked:bg-accent peer-focus-visible:ring-2 peer-focus-visible:ring-accent/40 after:absolute after:left-0.5 after:top-0.5 after:size-5 after:rounded-full after:bg-white after:shadow after:transition-transform peer-checked:after:translate-x-4" />
                  <span className="sr-only">{c.isActive ? t("Отключить «{name}»", { name: c.name }) : t("Включить «{name}»", { name: c.name })}</span>
                </label>
              </>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Правила снабжения: допуск без повторного согласования (на сколько процентов цена или сумма к закупке утверждённой заявки может вырасти,
 * прежде чем она снова уйдёт директору; 0 — любой рост) и эскалация просроченных задач снабжения — через сколько дней просрочки задача
 * уходит руководителю снабжения и директору. Сохраняются вместе.
 */
function ToleranceCard({ settings }: { settings: Settings }) {
  const t = useT();
  const router = useRouter();
  const [text, setText] = useState(String(settings.reapprovalTolerancePercent).replace(".", ","));
  const [headDays, setHeadDays] = useState(String(settings.escalateHeadDays));
  const [directorDays, setDirectorDays] = useState(String(settings.escalateDirectorDays));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const save = async () => {
    const n = Number(text.replace(",", ".").trim());
    if (!Number.isFinite(n) || n < 0 || n > 100) {
      setError(t("Допуск — от 0 до 100 %, не больше двух знаков после запятой."));
      return;
    }
    const head = Number(headDays.trim());
    const director = Number(directorDays.trim());
    if (!Number.isInteger(head) || head < 1 || head > 30) {
      setError(t("Руководителю снабжения — через 1–30 дней просрочки."));
      return;
    }
    if (!Number.isInteger(director) || director < head || director > 60) {
      setError(t("Директору — не раньше, чем руководителю снабжения, и не позже 60 дней просрочки."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await bff("procurement/settings", { method: "PUT", body: JSON.stringify({ reapprovalTolerancePercent: n, escalateHeadDays: head, escalateDirectorDays: director }) });
      setSaved(true);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="rounded-2xl border border-line bg-surface p-4 sm:p-5">
      <h2 className="text-base font-semibold text-ink">{t("Повторное согласование")}</h2>
      <p className="mt-1 text-sm text-ink-2">
        {t("Утверждённую заявку снабжение может уточнить. Новые и удалённые позиции, рост количества к закупке — всегда к директору снова; рост цены или суммы — если больше допуска.")}
      </p>
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <label className="inline-flex items-center gap-2 text-sm text-ink">
          {t("Допуск роста цены и суммы")}
          <input
            value={text}
            onChange={(e) => {
              setText(e.target.value);
              setSaved(false);
            }}
            inputMode="decimal"
            className="h-10 w-20 rounded-lg border border-line bg-surface px-3 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
          />
          %
        </label>
      </div>
      <h3 className="mt-5 text-sm font-semibold text-ink">{t("Эскалация просроченных задач снабжения")}</h3>
      <p className="mt-1 text-sm text-ink-2">{t("Исполнителю о сроке напоминают «Задачи». Если задача снабжения просрочена дольше, о ней узнаёт руководитель снабжения, затем директор.")}</p>
      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2">
        <label className="inline-flex items-center gap-2 text-sm text-ink">
          {t("Руководителю снабжения через")}
          <input
            value={headDays}
            onChange={(e) => {
              setHeadDays(e.target.value);
              setSaved(false);
            }}
            inputMode="numeric"
            className="h-10 w-16 rounded-lg border border-line bg-surface px-3 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
          />
          {t("дн.")}
        </label>
        <label className="inline-flex items-center gap-2 text-sm text-ink">
          {t("Директору через")}
          <input
            value={directorDays}
            onChange={(e) => {
              setDirectorDays(e.target.value);
              setSaved(false);
            }}
            inputMode="numeric"
            className="h-10 w-16 rounded-lg border border-line bg-surface px-3 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
          />
          {t("дн.")}
        </label>
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />}
          {t("Сохранить")}
        </button>
        {saved && <span className="text-sm text-ok">{t("Сохранено")}</span>}
      </div>
      <div className="mt-2">
        <ErrorNote text={error} />
      </div>
    </section>
  );
}
