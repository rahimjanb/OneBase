"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CloudDownload, Loader2, Pencil, Plus, Trash2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { stampRu, type StockImportResult, type Warehouse } from "@/lib/procurement";
import { StockImportReport } from "./StockImport";

/** Загрузка журнала склада из листа таблицы Google: склад, таблица из «Источников данных», лист, расписание, последний запуск. */
export type SheetImport = {
  id: string;
  warehouseId: string;
  warehouseName: string;
  dataOwner: "None" | "OneBase" | "Sheet";
  connectionId: string;
  connectionName: string;
  sheet: string;
  schedule: "manual" | "interval" | "daily";
  intervalMinutes: number | null;
  dailyAt: string | null;
  enabled: boolean;
  nextRunAt: string | null;
  lastRunAt: string | null;
  lastStatus: "ok" | "warning" | "error" | null;
  lastMessage: string | null;
  ownerName: string;
  version: number;
};

type View = { items: SheetImport[]; connections: { id: string; name: string; sheets: string[] }[] };

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

const blank = { warehouseId: "", connectionId: "", sheet: "", schedule: "manual" as SheetImport["schedule"], intervalMinutes: "60", dailyAt: "07:00", enabled: true };

/**
 * «Google-таблица» в «Движениях» (кладовщик, переходный период): журнал склада ведётся в таблице Google — OneBase загружает лист тем же
 * разбором, что файл (те же столбцы, проверки и отчёт; повтор не проводит строки второй раз), кнопкой или по расписанию. Таблицу подключает
 * администратор в «Настройки → Источники данных» и даёт к ней доступ.
 */
export function SheetImports({ warehouses }: { warehouses: Warehouse[] }) {
  const t = useT();
  const router = useRouter();
  const posts = warehouses.filter((w) => w.isActive && w.posts);
  const [open, setOpen] = useState(false);
  const [view, setView] = useState<View | null>(null);
  const [editing, setEditing] = useState<SheetImport | "new" | null>(null);
  const [form, setForm] = useState(blank);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<StockImportResult | null>(null);

  const load = () =>
    bff<View>("procurement/stock/sheets")
      .then(setView)
      .catch((e: Error) => setError(e.message));
  useEffect(() => {
    if (open) void load();
  }, [open]);

  if (posts.length === 0) return null;

  const edit = (item: SheetImport | "new") => {
    setEditing(item);
    setResult(null);
    setError(null);
    setForm(
      item === "new"
        ? { ...blank, warehouseId: posts.find((w) => w.dataOwner !== "OneBase")?.id ?? posts[0].id, connectionId: view?.connections[0]?.id ?? "" }
        : {
            warehouseId: item.warehouseId,
            connectionId: item.connectionId,
            sheet: item.sheet,
            schedule: item.schedule,
            intervalMinutes: String(item.intervalMinutes ?? 60),
            dailyAt: item.dailyAt ?? "07:00",
            enabled: item.enabled,
          },
    );
  };

  const save = async () => {
    setError(null);
    if (!form.connectionId) return setError(t("Выберите таблицу Google."));
    if (!form.sheet.trim()) return setError(t("Укажите лист таблицы."));
    setBusy("save");
    try {
      const body = JSON.stringify({
        warehouseId: form.warehouseId,
        connectionId: form.connectionId,
        sheet: form.sheet.trim(),
        schedule: form.schedule,
        intervalMinutes: form.schedule === "interval" ? Number(form.intervalMinutes) : null,
        dailyAt: form.schedule === "daily" ? form.dailyAt : null,
        enabled: form.enabled,
        version: editing && editing !== "new" ? editing.version : null,
      });
      if (editing && editing !== "new") await bff(`procurement/stock/sheets/${editing.id}`, { method: "PUT", body });
      else await bff("procurement/stock/sheets", { method: "POST", body });
      setEditing(null);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  const run = async (item: SheetImport, dryRun: boolean) => {
    setError(null);
    setResult(null);
    setBusy(`${item.id}:${dryRun}`);
    try {
      setResult(await bff<StockImportResult>(`procurement/stock/sheets/${item.id}/run?dryRun=${dryRun}`, { method: "POST" }));
      if (!dryRun) {
        await load();
        router.refresh();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  const remove = async (item: SheetImport) => {
    setBusy(`${item.id}:delete`);
    try {
      await bff(`procurement/stock/sheets/${item.id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  const connection = view?.connections.find((c) => c.id === form.connectionId);
  const schedule = (s: SheetImport) =>
    s.schedule === "interval" ? t("каждые {n} мин", { n: s.intervalMinutes ?? 0 }) : s.schedule === "daily" ? t("каждый день в {time}", { time: s.dailyAt ?? "" }) : t("кнопкой");

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className={secondary}>
        <CloudDownload className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Google-таблица")}</span>
      </button>
      <Sheet
        open={open}
        onClose={() => {
          setOpen(false);
          setEditing(null);
          setResult(null);
        }}
        size="xl"
        title={t("Журнал склада из Google-таблицы")}
        subtitle={t("Переходный период: журнал ведётся в таблице, OneBase загружает лист тем же разбором, что файл Excel")}
      >
        {!view ? (
          <p className="flex items-center gap-2 py-6 text-sm text-ink-3">
            <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Загрузка")}
          </p>
        ) : editing ? (
          <div className="space-y-3">
            {view.connections.length === 0 ? (
              <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-ink">
                {t("Таблиц Google нет: администратор подключает таблицу в «Настройки → Источники данных» и даёт к ней доступ.")}
              </p>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-ink">{t("Склад")}</span>
                  <select value={form.warehouseId} onChange={(e) => setForm({ ...form, warehouseId: e.target.value })} className={field}>
                    {posts.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-ink">{t("Таблица")}</span>
                  <select value={form.connectionId} onChange={(e) => setForm({ ...form, connectionId: e.target.value, sheet: "" })} className={field}>
                    {view.connections.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-ink">{t("Лист")}</span>
                  <input value={form.sheet} onChange={(e) => setForm({ ...form, sheet: e.target.value })} list="sheet-names" maxLength={200} placeholder={t("Название листа")} className={field} />
                  <datalist id="sheet-names">
                    {(connection?.sheets ?? []).map((s) => (
                      <option key={s} value={s} />
                    ))}
                  </datalist>
                </label>
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-ink">{t("Когда загружать")}</span>
                  <select value={form.schedule} onChange={(e) => setForm({ ...form, schedule: e.target.value as SheetImport["schedule"] })} className={field}>
                    <option value="manual">{t("Только кнопкой")}</option>
                    <option value="interval">{t("Каждые N минут")}</option>
                    <option value="daily">{t("Раз в день")}</option>
                  </select>
                </label>
                {form.schedule === "interval" && (
                  <label className="block">
                    <span className="mb-1.5 block text-sm font-medium text-ink">{t("Каждые, минут")}</span>
                    <input value={form.intervalMinutes} onChange={(e) => setForm({ ...form, intervalMinutes: e.target.value.replace(/\D/g, "") })} inputMode="numeric" className={field} />
                  </label>
                )}
                {form.schedule === "daily" && (
                  <label className="block">
                    <span className="mb-1.5 block text-sm font-medium text-ink">{t("Время (Ташкент)")}</span>
                    <input type="time" value={form.dailyAt} onChange={(e) => setForm({ ...form, dailyAt: e.target.value })} className={field} />
                  </label>
                )}
                <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2">
                  <input type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                  {t("Загрузка включена")}
                </label>
              </div>
            )}
            <p className="text-xs text-ink-3">
              {t("Лист — как файл журнала: дата, приход или расход, артикул или название, количество, цех; для сырья — партия и срок годности. По расписанию загрузка идёт от вашего имени.")}
            </p>
            <ErrorNote text={error} />
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setEditing(null)} className={secondary}>
                {t("Назад")}
              </button>
              <button type="button" onClick={() => void save()} disabled={busy !== null || view.connections.length === 0} className={primary}>
                {busy === "save" && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Сохранить")}
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-3">
            {view.items.length === 0 ? (
              <p className="text-sm text-ink-3">{t("Загрузок из таблиц пока нет.")}</p>
            ) : (
              <ul className="divide-y divide-line rounded-xl border border-line">
                {view.items.map((s) => (
                  <li key={s.id} className="flex flex-wrap items-start gap-3 px-3 py-3">
                    <span className="min-w-0 flex-1 text-sm">
                      <span className="block font-medium text-ink">
                        {s.connectionName} / {s.sheet}
                      </span>
                      <span className="block text-xs text-ink-3">
                        {t("на склад «{w}»", { w: s.warehouseName })} · {schedule(s)}
                        {!s.enabled && ` · ${t("выключена")}`}
                        {s.nextRunAt && ` · ${t("следующая {at}", { at: stampRu(s.nextRunAt) })}`}
                      </span>
                      {s.dataOwner === "OneBase" && <span className="block text-xs text-bad">{t("Склад ведётся в OneBase — загрузка на него закрыта.")}</span>}
                      {s.lastRunAt && (
                        <span className={`block text-xs ${s.lastStatus === "error" ? "text-bad" : s.lastStatus === "warning" ? "text-warn" : "text-ok"}`}>
                          {stampRu(s.lastRunAt)}: {t(s.lastMessage ?? "")}
                        </span>
                      )}
                    </span>
                    <span className="flex shrink-0 flex-wrap gap-1.5">
                      <button type="button" onClick={() => void run(s, true)} disabled={busy !== null} className={secondary}>
                        {busy === `${s.id}:true` && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Проверить")}
                      </button>
                      <button type="button" onClick={() => void run(s, false)} disabled={busy !== null} className={primary}>
                        {busy === `${s.id}:false` && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Загрузить сейчас")}
                      </button>
                      <button type="button" onClick={() => edit(s)} aria-label={t("Изменить загрузку")} className="grid size-9 place-items-center rounded-lg border border-line text-ink-2 hover:bg-muted">
                        <Pencil className="size-4" aria-hidden />
                      </button>
                      <button type="button" onClick={() => void remove(s)} aria-label={t("Удалить загрузку")} className="grid size-9 place-items-center rounded-lg border border-line text-ink-3 hover:text-bad">
                        <Trash2 className="size-4" aria-hidden />
                      </button>
                    </span>
                  </li>
                ))}
              </ul>
            )}
            <button type="button" onClick={() => edit("new")} className={secondary}>
              <Plus className="size-4" aria-hidden /> {t("Добавить лист")}
            </button>
            <ErrorNote text={error} />
            {result && (
              <div className="rounded-xl border border-line p-3">
                <p className="mb-2 text-sm font-semibold text-ink">{result.dryRun ? t("Проверка листа — ничего не сохранено") : t("Лист загружен")}</p>
                <StockImportReport result={result} />
              </div>
            )}
          </div>
        )}
      </Sheet>
    </>
  );
}
