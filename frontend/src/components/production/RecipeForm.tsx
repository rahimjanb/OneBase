"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Loader2, Plus, Save, Trash2, X } from "lucide-react";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, type RequestUnit } from "@/lib/procurement";
import { requirement, type ProductOption, type RecipeDetail } from "@/lib/production";
import { Label, card, field, newKey, primary, secondary } from "./ProdKit";

type Line = { key: string; itemId: string | null; sku: string | null; name: string; unitCode: string; quantity: string; loss: string };

const text = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n).replace(".", ","));

/**
 * Рецептура (черновик): код (у новой — потом не меняется), название, выход — товар Linko (по желанию), название и единица выхода, на какое
 * количество выхода заданы нормы; позиции склада с нормой в их единице и потерями, %. Внизу — сколько уйдёт на 1 единицу выхода. Правка —
 * с версией (изменили в другом окне — сервер скажет).
 */
export function RecipeForm({ initial, units }: { initial?: RecipeDetail; units: RequestUnit[] }) {
  const t = useT();
  const router = useRouter();
  const [code, setCode] = useState(initial?.code ?? "");
  const [name, setName] = useState(initial?.name ?? "");
  const [product, setProduct] = useState<{ id: number; name: string } | null>(
    initial?.linkoProductId ? { id: initial.linkoProductId, name: initial.linkoProductName ?? String(initial.linkoProductId) } : null,
  );
  const [outputName, setOutputName] = useState(initial?.outputName ?? "");
  const [outputUnit, setOutputUnit] = useState(initial?.outputUnit ?? (units.some((u) => u.code === "кг") ? "кг" : (units[0]?.code ?? "")));
  const [outputQty, setOutputQty] = useState(text(initial?.outputQuantity ?? 100));
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [lines, setLines] = useState<Line[]>(
    initial?.lines.map((l) => ({ key: l.id, itemId: l.itemId, sku: l.sku, name: l.name, unitCode: l.unitCode, quantity: text(l.quantity), loss: text(l.lossPercent || null) })) ?? [
      { key: newKey(), itemId: null, sku: null, name: "", unitCode: "", quantity: "", loss: "" },
    ],
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Подсказки товаров Linko для выхода.
  const [productText, setProductText] = useState("");
  const [products, setProducts] = useState<ProductOption[]>([]);
  useEffect(() => {
    if (product || productText.trim().length < 2) {
      setProducts([]);
      return;
    }
    let alive = true;
    const timer = window.setTimeout(() => {
      bff<ProductOption[]>(`production/products?q=${encodeURIComponent(productText.trim())}`)
        .then((found) => alive && setProducts(found))
        .catch(() => alive && setProducts([]));
    }, 250);
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
  }, [productText, product]);

  const setLine = (key: string, patch: Partial<Line>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const output = parseNumber(outputQty);

  const save = async () => {
    setError(null);
    if (!initial && !code.trim()) return setError(t("Укажите код рецептуры."));
    if (!name.trim()) return setError(t("Укажите название рецептуры."));
    if (output === null || output <= 0) return setError(t("Укажите выход рецептуры — на какое количество заданы нормы."));
    const filled = lines.filter((l) => l.itemId || l.quantity.trim());
    for (const l of filled) {
      if (!l.itemId) return setError(t("Строка {n}: выберите позицию склада.", { n: filled.indexOf(l) + 1 }));
      const q = parseNumber(l.quantity);
      if (q === null || q <= 0) return setError(t("«{name}»: укажите норму.", { name: l.name }));
      const loss = l.loss.trim() ? parseNumber(l.loss) : 0;
      if (loss === null || loss < 0 || loss > 100) return setError(t("«{name}»: потери — от 0 до 100 %.", { name: l.name }));
    }
    const body = {
      code: code.trim(),
      name: name.trim(),
      linkoProductId: product?.id ?? null,
      outputName: outputName.trim() || null,
      outputUnit,
      outputQuantity: output,
      comment: comment.trim() || null,
      lines: filled.map((l) => ({ itemId: l.itemId, quantity: parseNumber(l.quantity), lossPercent: l.loss.trim() ? parseNumber(l.loss) : 0 })),
      version: initial?.version ?? null,
    };
    setBusy(true);
    try {
      if (initial) {
        await bff(`production/recipes/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
        router.push(`/production/recipes/${initial.id}`);
      } else {
        const { id } = await bff<{ id: string }>("production/recipes", { method: "POST", body: JSON.stringify(body) });
        router.push(`/production/recipes/${id}`);
      }
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">{t("Рецептура")}</h2>
        <div className="grid gap-3 sm:grid-cols-[180px_minmax(0,1fr)]">
          <label className="block">
            <Label>{t("Код")}</Label>
            <input value={code} onChange={(e) => setCode(e.target.value)} disabled={!!initial} maxLength={32} placeholder="CK-01" className={`${field} tabular-nums`} />
          </label>
          <label className="block">
            <Label>{t("Название")}</Label>
            <input value={name} onChange={(e) => setName(e.target.value)} maxLength={200} placeholder={t("Например: печенье сахарное")} className={field} />
          </label>
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <div className="relative">
            <Label optional>{t("Товар Linko (выход)")}</Label>
            {product ? (
              <div className="flex h-10 items-center gap-2 rounded-lg border border-accent/30 bg-accent-soft/50 pl-3 pr-1 text-sm">
                <span className="min-w-0 flex-1 truncate text-ink">{product.name}</span>
                <button type="button" onClick={() => setProduct(null)} aria-label={t("Убрать товар Linko")} className="grid size-8 place-items-center rounded-md text-ink-3 hover:text-ink">
                  <X className="size-4" aria-hidden />
                </button>
              </div>
            ) : (
              <input value={productText} onChange={(e) => setProductText(e.target.value)} placeholder={t("Найдите товар по названию или коду")} className={field} />
            )}
            {products.length > 0 && (
              <ul className="absolute left-0 right-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-xl border border-line-strong bg-surface-solid p-1 shadow-lg">
                {products.map((p) => (
                  <li key={p.id}>
                    <button
                      type="button"
                      onClick={() => {
                        setProduct({ id: p.id, name: p.name });
                        if (!outputName.trim()) setOutputName(p.name);
                        if (!name.trim()) setName(p.name);
                        setProductText("");
                      }}
                      className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-muted"
                    >
                      {p.name}
                      <span className="block text-[11px] text-ink-3">{[p.code, p.unit].filter(Boolean).join(" · ")}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <label className="block">
            <Label optional>{t("Название выхода")}</Label>
            <input value={outputName} onChange={(e) => setOutputName(e.target.value)} maxLength={200} placeholder={t("как в рецептуре")} className={field} />
          </label>
        </div>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:max-w-sm">
          <label className="block">
            <Label>{t("Нормы на выход")}</Label>
            <input value={outputQty} onChange={(e) => setOutputQty(e.target.value)} inputMode="decimal" className={`${field} text-right tabular-nums`} />
          </label>
          <label className="block">
            <Label>{t("Единица выхода")}</Label>
            <select value={outputUnit} onChange={(e) => setOutputUnit(e.target.value)} className={field}>
              {units.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.code}
                </option>
              ))}
            </select>
          </label>
        </div>
        <p className="mt-2 text-xs text-ink-3">{t("Нормы позиций — на это количество выхода. Например, нормы на 100 кг печенья.")}</p>
      </section>

      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">
          {t("Состав")} <span className="font-normal text-ink-3">· {t("позиции склада, норма и потери")}</span>
        </h2>
        <div className="mb-1 hidden grid-cols-[minmax(0,1fr)_120px_96px_120px_40px] gap-2 px-0.5 text-[11px] font-medium uppercase tracking-wide text-ink-3 sm:grid">
          <span>{t("Позиция склада")}</span>
          <span className="text-right">{t("Норма")}</span>
          <span className="text-right">{t("Потери, %")}</span>
          <span className="text-right">{t("На 1 {unit}", { unit: outputUnit })}</span>
        </div>
        <ul className="space-y-2">
          {lines.map((l, i) => {
            const q = parseNumber(l.quantity);
            const per = q !== null && output ? requirement(q, 1, output, parseNumber(l.loss) ?? 0) : null;
            return (
              <li key={l.key} className="grid grid-cols-[minmax(0,1fr)_auto] gap-2 rounded-xl border border-line p-2.5 sm:grid-cols-[minmax(0,1fr)_120px_96px_120px_40px] sm:items-center sm:border-0 sm:p-0">
                <div className="min-w-0">
                  <CatalogPicker
                    name={l.name}
                    sku={l.sku}
                    linked={!!l.itemId}
                    label={t("Позиция {n}", { n: i + 1 })}
                    onPick={(o) => setLine(l.key, { itemId: o.id, sku: o.sku, name: o.name, unitCode: o.unitCode })}
                    onUnlink={() => setLine(l.key, { itemId: null, sku: null, name: "", unitCode: "" })}
                  />
                </div>
                <button
                  type="button"
                  onClick={() => setLines((ls) => (ls.length > 1 ? ls.filter((x) => x.key !== l.key) : ls))}
                  disabled={lines.length === 1}
                  aria-label={t("Удалить позицию {n}", { n: i + 1 })}
                  className="grid size-10 place-items-center rounded-lg text-ink-3 hover:text-bad disabled:opacity-30 sm:order-last"
                >
                  <Trash2 className="size-4" aria-hidden />
                </button>
                <div className="col-span-2 grid grid-cols-3 gap-2 sm:contents">
                  <label className="relative block">
                    <input
                      value={l.quantity}
                      onChange={(e) => setLine(l.key, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder={t("Норма")}
                      aria-label={t("Позиция {n}: норма", { n: i + 1 })}
                      className={`${field} pr-9 text-right tabular-nums`}
                    />
                    <span className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-ink-3">{l.unitCode}</span>
                  </label>
                  <input
                    value={l.loss}
                    onChange={(e) => setLine(l.key, { loss: e.target.value })}
                    inputMode="decimal"
                    placeholder="0"
                    aria-label={t("Позиция {n}: потери, %", { n: i + 1 })}
                    className={`${field} text-right tabular-nums`}
                  />
                  <span className="flex h-10 items-center justify-end text-sm tabular-nums text-ink-2">{per !== null ? `${quantity(per)} ${l.unitCode}` : "—"}</span>
                </div>
              </li>
            );
          })}
        </ul>
        <button
          type="button"
          onClick={() => setLines((ls) => [...ls, { key: newKey(), itemId: null, sku: null, name: "", unitCode: "", quantity: "", loss: "" }])}
          disabled={lines.length >= 100}
          className={`${secondary} mt-3`}
        >
          <Plus className="size-4" aria-hidden /> {t("Добавить позицию")}
        </button>
      </section>

      <section className={card}>
        <label className="block">
          <Label optional>{t("Комментарий")}</Label>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={2000}
            rows={3}
            placeholder={t("Технология, что изменилось в версии")}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
          />
        </label>
      </section>

      <ErrorNote text={error} />
      <div className="flex justify-end gap-2">
        <Link href={initial ? `/production/recipes/${initial.id}` : "/production/recipes"} className={`${secondary} h-10`}>
          {t("Отмена")}
        </Link>
        <button type="button" onClick={() => void save()} disabled={busy} className={`${primary} h-10`}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />} {t("Сохранить черновик")}
        </button>
      </div>
    </div>
  );
}
