"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Award, Ban, Clock, Loader2, Pencil, Plus, RotateCcw, ShoppingCart, Star } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateRu, money, parseNumber, quantity, type Comparison, type Offer, type SupplierRow } from "@/lib/procurement";
import { DocumentsBlock, RatingBadge } from "./ProcKit";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";

/**
 * Сравнение коммерческих предложений по заявке: каждое предложение — колонка (на телефоне — карточка): цены по позициям (с НДС, лучшая
 * отмечена), итог (лучший — среди покрывающих все позиции), доставка, срок (самый быстрый), оплата, гарантия, качество, история
 * поставщика, документы. Снабжение добавляет и правит предложения и создаёт из выбранного заказ; согласует заказ директор.
 */
export function OffersView({ data, suppliers }: { data: Comparison; suppliers: SupplierRow[] }) {
  const t = useT();
  const router = useRouter();
  const [editing, setEditing] = useState<Offer | "new" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const active = data.offers;

  const setStatus = async (offer: Offer, rejected: boolean) => {
    setError(null);
    try {
      await bff(`procurement/offers/${offer.id}/${rejected ? "reject" : "restore"}`, { method: "POST" });
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const orderButton = (o: Offer) =>
    data.canOrder && o.status !== "Rejected" ? (
      <Link href={`/procurement/orders/new?request=${data.requestId}&offer=${o.id}`} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-semibold text-white hover:bg-accent-strong">
        <ShoppingCart className="size-4" aria-hidden /> {t("Заказать")}
      </Link>
    ) : null;

  const Flags = ({ o }: { o: Offer }) => (
    <span className="flex flex-wrap gap-1">
      {o.bestTotal && (
        <span className="inline-flex items-center gap-1 rounded-md bg-ok-soft px-1.5 py-0.5 text-[11px] font-semibold text-ok">
          <Award className="size-3" aria-hidden /> {t("лучший итог")}
        </span>
      )}
      {o.fastest && (
        <span className="inline-flex items-center gap-1 rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-strong">
          <Clock className="size-3" aria-hidden /> {t("быстрее всех")}
        </span>
      )}
      {o.status === "Selected" && <span className="rounded-md bg-violet-soft px-1.5 py-0.5 text-[11px] font-semibold text-violet-light">{t("выбрано")}</span>}
      {o.status === "Rejected" && <span className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] font-semibold text-ink-3">{t("отклонено")}</span>}
      {o.expired && <span className="rounded-md bg-warn-soft px-1.5 py-0.5 text-[11px] font-semibold text-warn">{t("истекло")}</span>}
    </span>
  );

  const rows: { label: string; render: (o: Offer) => React.ReactNode }[] = [
    {
      label: "Итог",
      render: (o) => (
        <span className={`text-base font-semibold tabular-nums ${o.bestTotal ? "text-ok" : "text-ink"}`}>
          {money(o.total)}
          {o.covered < data.items.length && <span className="block text-[11px] font-normal text-warn">{t("покрыты не все позиции")}</span>}
        </span>
      ),
    },
    { label: "Позиции", render: (o) => <span className="tabular-nums">{money(o.subtotal)}{o.covered < data.items.length && <span className="block text-[11px] text-warn">{t("{a} из {b} позиций", { a: o.covered, b: data.items.length })}</span>}</span> },
    { label: "НДС", render: (o) => <span className="tabular-nums">{o.pricesIncludeVat ? t("{n}% в цене", { n: o.vatRate }) : `${money(o.vat)} (${o.vatRate}%)`}</span> },
    { label: "Доставка", render: (o) => <span className="tabular-nums">{o.deliveryCost > 0 ? money(o.deliveryCost) : t("бесплатно")}</span> },
    { label: "Срок поставки", render: (o) => <span className={o.fastest ? "font-semibold text-accent-strong" : ""}>{o.deliveryDays !== null ? t("{n} дн.", { n: o.deliveryDays }) : "—"}</span> },
    { label: "Оплата", render: (o) => o.paymentTerms ?? "—" },
    { label: "Гарантия", render: (o) => o.warranty ?? "—" },
    { label: "Качество", render: (o) => (o.quality ? <span className="inline-flex items-center gap-0.5 text-warn">{Array.from({ length: o.quality }, (_, i) => <Star key={i} className="size-3.5 fill-current" aria-hidden />)}</span> : <span className="text-ink-3">—</span>) },
    { label: "Действует до", render: (o) => <span className={o.expired ? "text-warn" : ""}>{dateRu(o.validUntil)}</span> },
    { label: "Поставщик", render: (o) => <span className="text-xs"><RatingBadge rating={o.supplierRating} /><span className="block text-ink-3">{t("заказов {a}, просрочено {b}", { a: o.supplierOrders, b: o.supplierOverdue })}</span></span> },
  ];

  return (
    <div className="mx-auto max-w-7xl space-y-4">
      <ErrorNote text={error} />
      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t("В заявке нет позиций к закупке — всё выдаётся со склада.")}</p>
      ) : active.length === 0 ? (
        <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-12 text-center">
          <h2 className="text-lg font-semibold text-ink">{t("Предложений пока нет")}</h2>
          <p className="mt-1.5 max-w-md text-sm text-ink-2">{t("Запросите цены у поставщиков и добавьте их предложения — система сравнит цены, итог, срок и условия.")}</p>
          {data.canEdit && (
            <button type="button" onClick={() => setEditing("new")} className="mt-5 inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Добавить предложение")}
            </button>
          )}
        </section>
      ) : (
        <>
          {data.canEdit && (
            <div className="flex justify-end">
              <button type="button" onClick={() => setEditing("new")} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
                <Plus className="size-4" aria-hidden /> {t("Добавить предложение")}
              </button>
            </div>
          )}
          <div className="hidden overflow-x-auto rounded-2xl border border-line bg-surface lg:block">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line align-top">
                  <th className="sticky left-0 min-w-56 bg-surface px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Позиция · к закупке")}</th>
                  {active.map((o) => (
                    <th key={o.id} className={`min-w-52 px-4 py-3 text-left font-normal ${o.status === "Rejected" ? "opacity-50" : ""}`}>
                      <Link href={`/procurement/suppliers/${o.supplierId}`} className="block font-semibold text-ink hover:text-accent-strong">
                        {o.supplierName}
                      </Link>
                      <div className="mt-1">
                        <Flags o={o} />
                      </div>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.requestItemId} className="border-b border-line">
                    <td className="sticky left-0 bg-surface px-4 py-2.5">
                      <span className="text-ink">{item.name}</span>
                      <span className="block text-xs text-ink-3">
                        {quantity(item.toPurchase)} {item.unitCode}
                        {item.estimatedPrice !== null && ` · ${t("в заявке {p}", { p: money(item.estimatedPrice) })}`}
                        {item.orderedQty > 0 && ` · ${t("заказано {n}", { n: quantity(item.orderedQty) })}`}
                      </span>
                    </td>
                    {active.map((o) => {
                      const line = o.lines.find((l) => l.requestItemId === item.requestItemId);
                      return (
                        <td key={o.id} className={`px-4 py-2.5 tabular-nums ${o.status === "Rejected" ? "opacity-50" : ""}`}>
                          {line ? (
                            <>
                              <span className={line.bestPrice ? "font-semibold text-ok" : "text-ink"}>{money(line.unitPrice)}</span>
                              {line.grossPrice !== line.unitPrice && <span className="text-[11px] text-ink-3"> ({t("с НДС {p}", { p: money(line.grossPrice) })})</span>}
                              <span className="block text-[11px] text-ink-3">
                                × {quantity(line.quantity)} = {money(line.sum)}
                              </span>
                            </>
                          ) : (
                            <span className="text-ink-3">{t("нет цены")}</span>
                          )}
                        </td>
                      );
                    })}
                  </tr>
                ))}
                {rows.map((row) => (
                  <tr key={row.label} className="border-b border-line">
                    <td className="sticky left-0 bg-surface px-4 py-2.5 text-xs font-semibold uppercase tracking-wide text-ink-3">{t(row.label)}</td>
                    {active.map((o) => (
                      <td key={o.id} className={`px-4 py-2.5 text-ink-2 ${o.status === "Rejected" ? "opacity-50" : ""}`}>
                        {row.render(o)}
                      </td>
                    ))}
                  </tr>
                ))}
                <tr className="align-top">
                  <td className="sticky left-0 bg-surface px-4 py-3 text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Документы и действия")}</td>
                  {active.map((o) => (
                    <td key={o.id} className="px-4 py-3">
                      <div className="flex flex-wrap gap-1.5">
                        {orderButton(o)}
                        {data.canEdit && o.orders.length === 0 && (
                          <button type="button" onClick={() => setEditing(o)} className={secondary}>
                            <Pencil className="size-4" aria-hidden /> {t("Изменить")}
                          </button>
                        )}
                        {data.canEdit && o.orders.length === 0 && (
                          <button type="button" onClick={() => void setStatus(o, o.status !== "Rejected")} className={secondary}>
                            {o.status === "Rejected" ? <RotateCcw className="size-4" aria-hidden /> : <Ban className="size-4" aria-hidden />}
                            {o.status === "Rejected" ? t("Вернуть") : t("Отклонить")}
                          </button>
                        )}
                      </div>
                      {o.orders.map((x) => (
                        <Link key={x.id} href={`/procurement/orders/${x.id}`} className="mt-1.5 block text-xs text-accent-strong hover:underline">
                          {t("заказ {n}", { n: x.number })}
                        </Link>
                      ))}
                      <div className="mt-2">
                        <DocumentsBlock owner="offer" id={o.id} documents={o.documents} can={data.canEdit} kinds={["offer", "invoice", "other"]} />
                      </div>
                    </td>
                  ))}
                </tr>
              </tbody>
            </table>
          </div>

          <ul className="space-y-3 lg:hidden">
            {active.map((o) => (
              <li key={o.id} className={`rounded-2xl border border-line bg-surface p-4 ${o.status === "Rejected" ? "opacity-60" : ""}`}>
                <div className="flex items-start justify-between gap-2">
                  <Link href={`/procurement/suppliers/${o.supplierId}`} className="font-semibold text-ink">
                    {o.supplierName}
                  </Link>
                  <span className={`text-lg font-semibold tabular-nums ${o.bestTotal ? "text-ok" : "text-ink"}`}>{money(o.total)}</span>
                </div>
                <div className="mt-1">
                  <Flags o={o} />
                </div>
                {o.covered < data.items.length && <p className="mt-1 text-xs text-warn">{t("{a} из {b} позиций", { a: o.covered, b: data.items.length })}</p>}
                <ul className="mt-2 space-y-1 text-sm">
                  {o.lines.map((l) => (
                    <li key={l.requestItemId} className="flex justify-between gap-2">
                      <span className="truncate text-ink-2">{l.name}</span>
                      <span className={`shrink-0 tabular-nums ${l.bestPrice ? "font-semibold text-ok" : "text-ink"}`}>{money(l.unitPrice)}</span>
                    </li>
                  ))}
                </ul>
                <p className="mt-2 text-xs text-ink-3">
                  {o.deliveryDays !== null && `${t("{n} дн.", { n: o.deliveryDays })} · `}
                  {o.deliveryCost > 0 ? t("доставка {p}", { p: money(o.deliveryCost) }) : t("доставка бесплатно")}
                  {o.paymentTerms && ` · ${o.paymentTerms}`}
                </p>
                <div className="mt-3 flex flex-wrap gap-1.5">
                  {orderButton(o)}
                  {data.canEdit && o.orders.length === 0 && (
                    <button type="button" onClick={() => setEditing(o)} className={secondary}>
                      <Pencil className="size-4" aria-hidden /> {t("Изменить")}
                    </button>
                  )}
                  {data.canEdit && o.orders.length === 0 && (
                    <button type="button" onClick={() => void setStatus(o, o.status !== "Rejected")} className={secondary}>
                      {o.status === "Rejected" ? <RotateCcw className="size-4" aria-hidden /> : <Ban className="size-4" aria-hidden />}
                      {o.status === "Rejected" ? t("Вернуть") : t("Отклонить")}
                    </button>
                  )}
                </div>
                {o.orders.map((x) => (
                  <Link key={x.id} href={`/procurement/orders/${x.id}`} className="mt-1.5 block text-xs text-accent-strong hover:underline">
                    {t("заказ {n}", { n: x.number })}
                  </Link>
                ))}
                <div className="mt-3 border-t border-line pt-3">
                  <DocumentsBlock owner="offer" id={o.id} documents={o.documents} can={data.canEdit} kinds={["offer", "invoice", "other"]} />
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
      <OfferSheet data={data} suppliers={suppliers} offer={editing === "new" ? null : editing} open={editing !== null} onClose={() => setEditing(null)} />
    </div>
  );
}

/** Форма предложения: поставщик, цены и количества по позициям, НДС, доставка, срок, оплата, гарантия, срок действия, качество 1–5. */
function OfferSheet({ data, suppliers, offer, open, onClose }: { data: Comparison; suppliers: SupplierRow[]; offer: Offer | null; open: boolean; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [supplierId, setSupplierId] = useState("");
  const [prices, setPrices] = useState<Record<string, { price: string; qty: string }>>({});
  const [vatRate, setVatRate] = useState("12");
  const [includeVat, setIncludeVat] = useState(true);
  const [deliveryCost, setDeliveryCost] = useState("0");
  const [deliveryDays, setDeliveryDays] = useState("");
  const [terms, setTerms] = useState("");
  const [warranty, setWarranty] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [quality, setQuality] = useState<number | null>(null);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setSupplierId(offer?.supplierId ?? "");
    setPrices(
      Object.fromEntries(
        data.items.map((i) => {
          const line = offer?.lines.find((l) => l.requestItemId === i.requestItemId);
          return [i.requestItemId, { price: line ? String(line.unitPrice).replace(".", ",") : "", qty: String(line?.quantity ?? i.toPurchase).replace(".", ",") }];
        }),
      ),
    );
    setVatRate(String(offer?.vatRate ?? 12).replace(".", ","));
    setIncludeVat(offer?.pricesIncludeVat ?? true);
    setDeliveryCost(String(offer?.deliveryCost ?? 0).replace(".", ","));
    setDeliveryDays(offer?.deliveryDays !== null && offer?.deliveryDays !== undefined ? String(offer.deliveryDays) : "");
    setTerms(offer?.paymentTerms ?? "");
    setWarranty(offer?.warranty ?? "");
    setValidUntil(offer?.validUntil ?? "");
    setQuality(offer?.quality ?? null);
    setComment(offer?.comment ?? "");
  }, [open, offer, data.items]);

  const save = async () => {
    setError(null);
    if (!supplierId) return setError(t("Выберите поставщика."));
    const lines = data.items
      .map((i) => ({ requestItemId: i.requestItemId, unitPrice: parseNumber(prices[i.requestItemId]?.price ?? ""), quantity: parseNumber(prices[i.requestItemId]?.qty ?? "") }))
      .filter((l) => l.unitPrice !== null);
    if (lines.length === 0) return setError(t("Укажите цены хотя бы по одной позиции."));
    setBusy(true);
    try {
      const body = JSON.stringify({
        supplierId,
        validUntil: validUntil || null,
        deliveryDays: deliveryDays ? Number(deliveryDays) : null,
        deliveryCost: parseNumber(deliveryCost) ?? 0,
        vatRate: parseNumber(vatRate) ?? 0,
        pricesIncludeVat: includeVat,
        paymentTerms: terms.trim() || null,
        warranty: warranty.trim() || null,
        quality,
        comment: comment.trim() || null,
        lines,
      });
      if (offer) await bff(`procurement/offers/${offer.id}`, { method: "PUT", body });
      else await bff(`procurement/requests/${data.requestId}/offers`, { method: "POST", body });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={offer ? t("Изменить предложение") : t("Новое предложение")}
      subtitle={`${data.number} · ${data.title}`}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Поставщик")}</span>
          <select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} className={field}>
            <option value="">{t("Выберите поставщика")}</option>
            {suppliers.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
          <Link href="/procurement/suppliers" className="mt-1 inline-block text-xs text-accent-strong hover:underline">
            {t("Нет в списке — добавьте в «Поставщиках»")}
          </Link>
        </label>
        <div>
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Цены за единицу")}</span>
          <ul className="divide-y divide-line rounded-xl border border-line">
            {data.items.map((i) => (
              <li key={i.requestItemId} className="flex flex-wrap items-center gap-2 px-3 py-2">
                <span className="min-w-0 flex-1 basis-40 text-sm text-ink">
                  {i.name}
                  <span className="block text-[11px] text-ink-3">{i.estimatedPrice !== null ? t("в заявке {p}", { p: money(i.estimatedPrice) }) : t("в заявке без цены")}</span>
                </span>
                <input
                  value={prices[i.requestItemId]?.qty ?? ""}
                  onChange={(e) => setPrices((p) => ({ ...p, [i.requestItemId]: { ...p[i.requestItemId], qty: e.target.value } }))}
                  inputMode="decimal"
                  aria-label={t("Количество: {name}", { name: i.name })}
                  className="h-9 w-20 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums"
                />
                <span className="w-10 text-xs text-ink-3">{i.unitCode}</span>
                <input
                  value={prices[i.requestItemId]?.price ?? ""}
                  onChange={(e) => setPrices((p) => ({ ...p, [i.requestItemId]: { ...p[i.requestItemId], price: e.target.value } }))}
                  inputMode="decimal"
                  placeholder={t("цена")}
                  aria-label={t("Цена за единицу: {name}", { name: i.name })}
                  className="h-9 w-32 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums"
                />
              </li>
            ))}
          </ul>
        </div>
        <div className="grid gap-3 sm:grid-cols-4">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("НДС, %")}</span>
            <input value={vatRate} onChange={(e) => setVatRate(e.target.value)} inputMode="decimal" className={field} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Доставка, сум")}</span>
            <input value={deliveryCost} onChange={(e) => setDeliveryCost(e.target.value)} inputMode="decimal" className={field} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Срок, дней")}</span>
            <input value={deliveryDays} onChange={(e) => setDeliveryDays(e.target.value.replace(/\D/g, ""))} inputMode="numeric" className={field} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Действует до")}</span>
            <input type="date" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} className={field} />
          </label>
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-4">
            <input type="checkbox" checked={includeVat} onChange={(e) => setIncludeVat(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
            {t("Цены указаны с НДС")}
          </label>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Условия оплаты")}</span>
            <input value={terms} onChange={(e) => setTerms(e.target.value)} maxLength={500} className={field} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Гарантия")}</span>
            <input value={warranty} onChange={(e) => setWarranty(e.target.value)} maxLength={200} className={field} />
          </label>
        </div>
        <div>
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Качество (ваша оценка)")}</span>
          <div className="flex gap-1">
            {[1, 2, 3, 4, 5].map((n) => (
              <button
                key={n}
                type="button"
                onClick={() => setQuality(quality === n ? null : n)}
                aria-label={t("Оценка {n}", { n })}
                aria-pressed={quality !== null && n <= quality}
                className={`grid size-9 place-items-center rounded-lg ${quality !== null && n <= quality ? "text-warn" : "text-ink-4"} hover:bg-muted`}
              >
                <Star className={`size-5 ${quality !== null && n <= quality ? "fill-current" : ""}`} aria-hidden />
              </button>
            ))}
          </div>
        </div>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        <p className="text-xs text-ink-3">{t("PDF или Excel с предложением прикрепите после сохранения — в колонке предложения.")}</p>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
