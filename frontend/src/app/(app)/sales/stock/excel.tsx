"use client";

import { useState } from "react";
import { FileSpreadsheet, Loader2 } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { Button } from "@/components/ui";
import { tNow, useT } from "@/i18n/client";
import { date } from "@/lib/sales/format";
import type { StockView } from "@/lib/sales/types";

// «Собрать в Excel» на «Рек. остатке»: выбор столбцов и листа «По складам» → файл собирает сервер (/api/sales/stock/xlsx) с теми же
// охватом и фильтрами, что на странице. Ключи столбцов — как в StockExport.Columns на сервере.

type ExcelColumn = { key: string; label: string; group: string; dealersOnly?: boolean };

const groups = ["Товар", "Остаток", "Продажи и запас", "Деньги", "Заказ", "Завод"];

const columns: ExcelColumn[] = [
  { key: "name", label: "Наименование", group: "Товар" },
  { key: "code", label: "Код", group: "Товар" },
  { key: "top", label: "ТОП", group: "Товар" },
  { key: "cat", label: "Категория", group: "Товар" },
  { key: "pack", label: "Фасовка, кг", group: "Товар" },
  { key: "unitKg", label: "Вес штуки, кг", group: "Товар" },
  { key: "boxKg", label: "Вес коробки, кг", group: "Товар" },
  { key: "stockKg", label: "Остаток, кг", group: "Остаток" },
  { key: "stockBoxes", label: "Остаток, коробки", group: "Остаток" },
  { key: "stockPieces", label: "Остаток, штуки", group: "Остаток" },
  { key: "perDay", label: "Продажи в день, кг", group: "Продажи и запас" },
  { key: "rawPerDay", label: "Продажи в день без поправки на аутсток, кг", group: "Продажи и запас" },
  { key: "zeroDays", label: "Дней без товара в прошлом месяце (лист «По складам»)", group: "Продажи и запас", dealersOnly: true },
  { key: "days", label: "Хватит, дн.", group: "Продажи и запас" },
  { key: "need15", label: "Запас на 15 дн., кг", group: "Продажи и запас" },
  { key: "need30", label: "Запас на 30 дн., кг", group: "Продажи и запас" },
  { key: "price", label: "Цена входа, сум", group: "Деньги" },
  { key: "value", label: "Сумма запаса, сум", group: "Деньги" },
  { key: "orderKg", label: "Рек. заказ, кг", group: "Заказ" },
  { key: "orderBoxes", label: "Рек. заказ, коробки", group: "Заказ" },
  { key: "orderSum", label: "Рек. заказ, сум", group: "Заказ" },
  { key: "status", label: "Статус", group: "Заказ" },
  { key: "factoryKg", label: "Завод, кг", group: "Завод", dealersOnly: true },
  { key: "factoryOrder", label: "Не хватает на заводе, кг", group: "Завод", dealersOnly: true },
];

/** Как в таблице страницы. */
const defaults = ["name", "code", "cat", "pack", "unitKg", "stockKg", "perDay", "days", "need15", "need30", "price", "value", "orderKg", "status", "factoryKg"];

const storageKey = "onebase.stock-excel";

/** Выбор запоминается в браузере (удобство, не данные): без доступа к хранилищу — столбцы по умолчанию. */
function load(): { columns: string[]; warehouses: boolean } | null {
  try {
    const raw = window.localStorage.getItem(storageKey);
    const saved = raw ? (JSON.parse(raw) as { columns?: unknown; warehouses?: unknown }) : null;
    return saved && Array.isArray(saved.columns) ? { columns: saved.columns.filter((c): c is string => typeof c === "string"), warehouses: saved.warehouses === true } : null;
  } catch {
    return null;
  }
}

function save(value: { columns: string[]; warehouses: boolean }) {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(value));
  } catch {
    // приватный режим или хранилище запрещено — выбор просто не запомнится
  }
}

/** Кнопка в шапке таблицы «Рек. остатка» (и списка, и «По регионам»). */
export function StockExcelButton({ data }: { data: StockView }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted"
      >
        <FileSpreadsheet className="size-3.5" />
        {t("Собрать в Excel")}
      </button>
      {open && <StockExcelDialog data={data} onClose={() => setOpen(false)} />}
    </>
  );
}

function StockExcelDialog({ data, onClose }: { data: StockView; onClose: () => void }) {
  const t = useT();
  const dealers = data.scope !== "plant" && data.scope !== "export";
  const available = columns.filter((c) => dealers || !c.dealersOnly);
  const [initial] = useState(load);
  const [picked, setPicked] = useState<Set<string>>(() => new Set(initial?.columns.length ? initial.columns : defaults));
  const [warehouses, setWarehouses] = useState(initial?.warehouses ?? false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const chosen = available.filter((c) => picked.has(c.key));

  const toggle = (key: string) =>
    setPicked((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  const download = async () => {
    setBusy(true);
    setError(null);
    save({ columns: [...picked], warehouses });
    const params = new URLSearchParams();
    if (data.scope !== "country") params.set("scope", data.scope);
    if (data.query) params.set("q", data.query);
    if (data.selectedCategories.length) params.set("cat", data.selectedCategories.join(","));
    if (data.selectedPacks.length) params.set("pack", data.selectedPacks.join(","));
    if (data.top !== "all") params.set("top", data.top);
    if (data.status !== "all") params.set("status", data.status);
    params.set("columns", chosen.map((c) => c.key).join(","));
    if (warehouses && dealers) params.set("warehouses", "true");
    const name = `${t("Рекомендуемый остаток")} — ${t(data.scopeName)} ${date(data.snapshotDate)}`.replace(/[\\/:*?"<>|«»]/g, " ").replace(/\s+/g, " ").trim();
    try {
      const response = await fetch(`/bff/api/sales/stock/xlsx/${encodeURIComponent(name)}.xlsx?${params}`);
      if (!response.ok) {
        const text = await response.text();
        let message = text;
        try {
          const json = JSON.parse(text);
          message = json.error ?? json.detail ?? json.title ?? text;
        } catch {
          // не JSON
        }
        throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : message ? tNow(message) : tNow("Ошибка {status}", { status: response.status }));
      }

      const url = URL.createObjectURL(await response.blob());
      const a = Object.assign(document.createElement("a"), { href: url, download: `${name}.xlsx` });
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
      onClose();
    } catch (e) {
      setError(
        e instanceof TypeError
          ? t("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите.")
          : (e as Error).message,
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={t("Собрать в Excel")} onClose={onClose} wide>
      <p className="mb-4 text-sm text-ink-2">
        {t("Строки — как на странице: {scope}, {n} SKU с текущими фильтрами. Отметьте, какие столбцы собрать в отчёт.", {
          scope: t(data.scopeName),
          n: data.items.length.toLocaleString("ru-RU"),
        })}
      </p>
      <div className="mb-3 flex flex-wrap gap-2 text-xs">
        <button type="button" className="rounded-lg border border-line px-2.5 py-1 hover:bg-muted" onClick={() => setPicked(new Set(available.map((c) => c.key)))}>
          {t("Выбрать все")}
        </button>
        <button type="button" className="rounded-lg border border-line px-2.5 py-1 hover:bg-muted" onClick={() => setPicked(new Set())}>
          {t("Снять все")}
        </button>
        <button type="button" className="rounded-lg border border-line px-2.5 py-1 hover:bg-muted" onClick={() => setPicked(new Set(defaults))}>
          {t("Как в таблице")}
        </button>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {groups.map((group) => {
          const items = available.filter((c) => c.group === group);
          if (items.length === 0) return null;
          return (
            <fieldset key={group} className="rounded-lg border border-line p-3">
              <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-ink-3">{t(group)}</legend>
              <div className="space-y-1.5">
                {items.map((c) => (
                  <label key={c.key} className="flex cursor-pointer items-start gap-2 text-sm text-ink">
                    <input type="checkbox" className="mt-0.5 size-4 shrink-0 accent-[var(--color-accent)]" checked={picked.has(c.key)} onChange={() => toggle(c.key)} />
                    <span>{t(c.label)}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          );
        })}
      </div>
      {dealers && (
        <label className="mt-4 flex cursor-pointer items-start gap-2 rounded-lg bg-muted/60 p-3 text-sm text-ink">
          <input type="checkbox" className="mt-0.5 size-4 shrink-0 accent-[var(--color-accent)]" checked={warehouses} onChange={(e) => setWarehouses(e.target.checked)} />
          <span>
            <span className="font-medium">{t("Добавить лист «По складам»")}</span>
            <span className="block text-xs text-ink-2">{t("Каждый товар по заводу и складам дилеров, где есть остаток или продажи, — с выбранными столбцами.")}</span>
          </span>
        </label>
      )}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-5 flex flex-wrap items-center justify-end gap-2">
        <span className="mr-auto text-xs text-ink-3">{t("Выбрано столбцов: {n}", { n: chosen.length })}</span>
        <Button onClick={onClose}>{t("Отмена")}</Button>
        <Button variant="primary" disabled={busy || chosen.length === 0} onClick={download}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <FileSpreadsheet className="size-4" />}
          {busy ? t("Готовим файл…") : t("Скачать")}
        </Button>
      </div>
    </Modal>
  );
}
