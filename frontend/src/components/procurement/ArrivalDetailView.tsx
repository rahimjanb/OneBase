"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { Ban, CheckCircle2, FlaskConical, History, Loader2, PackageCheck, Scale, Truck, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { acceptedSum, shortfall, sourceHref, stockOf, type ArrivalDetail, type ArrivalLine } from "@/lib/arrivals";
import { bff } from "@/lib/bff";
import { batchQualityLabel, batchQualityTone, dateRu, money, parseNumber, qualityCheckStatusLabel, quantity, stampRu } from "@/lib/procurement";
import { ArrivalStatusPill } from "./ArrivalsView";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";
const text = (n: number) => String(n).replace(".", ",");

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="flex items-start justify-between gap-4 py-2 text-sm">
      <dt className="shrink-0 text-ink-3">{t(label)}</dt>
      <dd className="min-w-0 text-right text-ink">{children}</dd>
    </div>
  );
}

/** Шаги поставки: машина приехала → проверка качества (или не нужна) → приёмка на склад; текущий — выделен. */
function Steps({ data }: { data: ArrivalDetail }) {
  const t = useT();
  const a = data.arrival;
  const checked = data.lines.filter((l) => l.control !== "None").length;
  const decided = data.lines.filter((l) => l.control !== "None" && l.quality !== "Pending").length;
  const closed = a.status === "Cancelled" || a.status === "Returned";
  const steps = [
    { key: "arrived", icon: Truck, label: "Машина приехала", note: stampRu(a.arrivedAt), state: "done" },
    {
      key: "quality",
      icon: FlaskConical,
      label: "Качество",
      note: checked === 0 ? t("не нужно") : t("решено {a} из {b}", { a: decided, b: checked }),
      state: a.status === "Quality" ? "current" : checked === 0 || decided === checked ? "done" : "todo",
    },
    {
      key: "accept",
      icon: Scale,
      label: "Взвешивание и приёмка",
      note: a.acceptedAt ? stampRu(a.acceptedAt) : closed ? t(a.status === "Returned" ? "возврат" : "отменена") : "",
      state: a.status === "Ready" ? "current" : a.status === "Accepted" ? "done" : "todo",
    },
    { key: "stock", icon: PackageCheck, label: "На складе", note: a.receiptNumber ?? "", state: a.status === "Accepted" ? "done" : "todo" },
  ] as const;
  return (
    <ol className="grid gap-2 sm:grid-cols-4">
      {steps.map((s) => (
        <li
          key={s.key}
          aria-current={s.state === "current" ? "step" : undefined}
          className={`flex items-center gap-2.5 rounded-xl px-3 py-2.5 ${
            s.state === "done" ? "bg-ok-soft text-ok" : s.state === "current" ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"
          }`}
        >
          <s.icon className="size-4 shrink-0" aria-hidden />
          <span className="min-w-0">
            <span className={`block text-sm ${s.state === "current" ? "font-semibold" : "font-medium"}`}>{t(s.label)}</span>
            {s.note && <span className="block truncate text-xs opacity-80">{s.note}</span>}
          </span>
        </li>
      ))}
    </ol>
  );
}

/** Строка поставки до и после приёмки: что по накладной, партия и срок, проверка и решение, что принято и куда. */
function LineFacts({ line }: { line: ArrivalLine }) {
  const t = useT();
  const diff = line.stockQty !== null ? shortfall(line.declaredBase, line.stockQty) : null;
  return (
    <div className="min-w-0 flex-1">
      <p className="text-sm font-medium text-ink">
        {line.name} <span className="text-xs tabular-nums text-ink-3">{line.sku}</span>
      </p>
      <p className="mt-0.5 text-xs text-ink-3">
        {[
          t("по накладной {q}", { q: `${quantity(line.declared)} ${t(line.unitCode)}${line.unitCode !== line.itemUnitCode ? ` (${quantity(line.declaredBase)} ${t(line.itemUnitCode)})` : ""}` }),
          line.supplierBatchNo ? t("партия {n}", { n: line.supplierBatchNo }) : null,
          line.expiresOn ? t("годна до {date}", { date: dateRu(line.expiresOn) }) : null,
        ]
          .filter(Boolean)
          .join(" · ")}
      </p>
      <p className="mt-1 flex flex-wrap items-center gap-2 text-xs">
        {line.qualityCheckId ? (
          <Link href={`/production/quality/${line.qualityCheckId}`} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
            <FlaskConical className="size-3.5" aria-hidden /> {line.qualityCheckNumber}
            {line.checkStatus && line.checkStatus !== "Decided" && ` · ${t(qualityCheckStatusLabel[line.checkStatus]).toLowerCase()}`}
          </Link>
        ) : (
          <span className="text-ink-3">{t("без контроля качества")}</span>
        )}
        {line.control !== "None" && line.quality !== "Pending" && (
          <span className={`rounded-md px-1.5 py-0.5 font-semibold ${batchQualityTone[line.quality]}`}>{t(line.quality === "Approved" ? "Допущено" : batchQualityLabel[line.quality])}</span>
        )}
        {line.sampleQty !== null && <span className="text-ink-3">{t("проба {q}", { q: `${quantity(line.sampleQty)} ${t(line.itemUnitCode)}` })}</span>}
      </p>
      {line.quality === "Rejected" && (
        <p className="mt-1.5 flex items-start gap-1.5 rounded-lg bg-bad-soft px-2.5 py-1.5 text-xs text-bad">
          <Undo2 className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>
            {t("Вернуть поставщику — на склад не принимается.")}
            {line.decisionComment && ` ${line.decisionComment}`}
          </span>
        </p>
      )}
      {line.stockQty !== null && (
        <p className="mt-1.5 text-xs text-ink-2">
          {t("принято {q}", {
            q: `${quantity(line.stockQty)} ${t(line.itemUnitCode)}${line.weighed && line.acceptedQty ? ` (${quantity(line.acceptedQty)} ${t(line.unitCode)})` : ""}`,
          })}
          {line.batchId && (
            <>
              {" · "}
              <Link href={`/procurement/batches/${line.batchId}`} className="tabular-nums text-accent-strong hover:underline">
                {line.batchNumber}
              </Link>
            </>
          )}
          {diff !== null && diff !== 0 && (
            <span className={diff < 0 ? "text-bad" : "text-warn"}>{` · ${t(diff < 0 ? "недостача {q}" : "излишек {q}", { q: `${quantity(Math.abs(diff))} ${t(line.itemUnitCode)}` })}`}</span>
          )}
          {line.comment && <span className="block text-ink-3">{line.comment}</span>}
        </p>
      )}
    </div>
  );
}

type Draft = { places: string; weight: string; comment: string };

/**
 * Приёмка на склад (кладовщик): по каждой допущенной строке — сколько мест принято и вес нетто (если позиция весовая, а привезли в
 * упаковках: на склад идёт вес). Расхождение с накладной видно сразу; ничего не принято — с причиной. Повторное нажатие не проводит дважды.
 */
function AcceptForm({ data }: { data: ArrivalDetail }) {
  const t = useT();
  const router = useRouter();
  const lines = data.lines.filter((l) => l.quality === "Approved");
  const [drafts, setDrafts] = useState<Record<string, Draft>>(() => Object.fromEntries(lines.map((l) => [l.id, { places: text(l.declared), weight: "", comment: "" }])));
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sent = useRef(false);
  const patch = (id: string, change: Partial<Draft>) => setDrafts((all) => ({ ...all, [id]: { ...all[id], ...change } }));
  const stock = (l: ArrivalLine) => stockOf(l, parseNumber(drafts[l.id].places), parseNumber(drafts[l.id].weight));
  const total = acceptedSum(lines.map((l) => ({ stock: stock(l), price: l.price, factor: l.factor })));

  const save = async () => {
    setError(null);
    const body = [];
    for (const l of lines) {
      const d = drafts[l.id];
      const places = parseNumber(d.places);
      if (places === null || places < 0) return setError(t("«{name}»: укажите, сколько принято.", { name: l.name }));
      if (l.weighed && places > 0 && !(parseNumber(d.weight) ?? 0)) return setError(t("«{name}»: укажите вес нетто, {unit} — на склад идёт фактический вес.", { name: l.name, unit: t(l.itemUnitCode) }));
      if (places === 0 && !d.comment.trim()) return setError(t("«{name}»: ничего не принято — напишите почему (бой, недогруз, отказ).", { name: l.name }));
      if (stock(l) === null) return setError(t("«{name}»: в {unit} получается больше трёх знаков — уточните количество.", { name: l.name, unit: t(l.itemUnitCode) }));
      body.push({ lineId: l.id, quantity: places, weight: l.weighed ? parseNumber(d.weight) : null, comment: d.comment.trim() || null });
    }
    if (sent.current) return;
    sent.current = true;
    setBusy(true);
    try {
      await bff(`procurement/arrivals/${data.arrival.id}/accept`, { method: "POST", body: JSON.stringify({ lines: body, comment: comment.trim() || null, version: data.version }) });
      router.refresh();
    } catch (e) {
      sent.current = false;
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <section className={`${card} border-accent/40`} aria-labelledby="ar-accept">
      <h2 id="ar-accept" className="flex items-center gap-2 text-base font-semibold text-ink">
        <Scale className="size-4 text-accent-strong" aria-hidden /> {t("Взвесьте и примите на склад")}
      </h2>
      <p className="mt-1 text-sm text-ink-2">{t("Напишите, сколько мест и сколько весит на самом деле. На склад пойдёт фактический вес, снабжению, аналитику и финансисту придёт уведомление.")}</p>
      <ul className="mt-3 space-y-3">
        {lines.map((l) => {
          const d = drafts[l.id];
          const s = stock(l);
          const diff = shortfall(l.declaredBase, s);
          return (
            <li key={l.id} className="rounded-xl border border-line p-3">
              <div className="flex flex-wrap items-start gap-3">
                <div className="min-w-0 basis-full sm:basis-0 sm:flex-1">
                  <p className="text-sm font-medium text-ink">{l.name}</p>
                  <p className="mt-0.5 text-xs text-ink-3">
                    {[
                      t("по накладной {q}", { q: `${quantity(l.declared)} ${t(l.unitCode)}${l.unitCode !== l.itemUnitCode ? ` · ${quantity(l.declaredBase)} ${t(l.itemUnitCode)}` : ""}` }),
                      l.supplierBatchNo ? t("партия {n}", { n: l.supplierBatchNo }) : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </p>
                </div>
                <label className="block min-w-0 flex-1 sm:w-32 sm:flex-none">
                  <span className="mb-1 block text-xs font-medium text-ink-2">{l.weighed ? t("Мест, {unit}", { unit: t(l.unitCode) }) : t("Принято, {unit}", { unit: t(l.unitCode) })}</span>
                  <input value={d.places} onChange={(e) => patch(l.id, { places: e.target.value })} inputMode="decimal" className={`${field} text-right tabular-nums`} />
                </label>
                {l.weighed && (
                  <label className="block min-w-0 flex-1 sm:w-36 sm:flex-none">
                    <span className="mb-1 block text-xs font-medium text-ink-2">{t("Вес нетто, {unit}", { unit: t(l.itemUnitCode) })}</span>
                    <input
                      value={d.weight}
                      onChange={(e) => patch(l.id, { weight: e.target.value })}
                      inputMode="decimal"
                      placeholder={quantity(l.declaredBase)}
                      className={`${field} text-right tabular-nums`}
                    />
                  </label>
                )}
              </div>
              <p className="mt-2 flex flex-wrap gap-x-3 text-xs">
                <span className="text-ink-2">{s === null ? t("на склад: —") : t("на склад: {q}", { q: `${quantity(s)} ${t(l.itemUnitCode)}` })}</span>
                {diff !== null && diff !== 0 && (
                  <span className={diff < 0 ? "font-semibold text-bad" : "font-semibold text-warn"}>
                    {t(diff < 0 ? "недостача {q}" : "излишек {q}", { q: `${quantity(Math.abs(diff))} ${t(l.itemUnitCode)}` })}
                  </span>
                )}
              </p>
              {(diff !== null && diff !== 0) || parseNumber(d.places) === 0 ? (
                <input
                  value={d.comment}
                  onChange={(e) => patch(l.id, { comment: e.target.value })}
                  maxLength={500}
                  placeholder={t("Причина расхождения: бой, недовес, недогруз")}
                  className={`${field} mt-2`}
                />
              ) : null}
            </li>
          );
        })}
      </ul>
      <label className="mt-3 block">
        <span className="mb-1.5 block text-sm font-medium text-ink">
          {t("Комментарий к приходу")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
        </span>
        <input value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} className={field} />
      </label>
      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-3">
        <span className="text-sm text-ink-2">
          {t("Сумма по цене тендера")} <span className="font-semibold tabular-nums text-ink">{money(total)}</span> {t("сум")}
        </span>
        <button type="button" onClick={() => void save()} disabled={busy} className={primary}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <PackageCheck className="size-4" aria-hidden />}
          {t("Принять на склад")}
        </button>
      </div>
      <div className="mt-2">
        <ErrorNote text={error} />
      </div>
    </section>
  );
}

/**
 * Поставка (приезд машины): шаги «приехала → качество → взвешивание и приёмка → на складе», строки с проверками и решениями, приёмка
 * кладовщиком (места и вес нетто, расхождения), забракованное — вернуть поставщику, отмена до приёмки с причиной, сведения и история.
 */
export function ArrivalDetailView({ data, seesRequests = false, seesTenders = true }: { data: ArrivalDetail; seesRequests?: boolean; seesTenders?: boolean }) {
  const t = useT();
  const router = useRouter();
  const a = data.arrival;
  const [cancelling, setCancelling] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cancel = async () => {
    if (!reason.trim()) return setError(t("Укажите причину отмены."));
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/arrivals/${a.id}/cancel`, { method: "POST", body: JSON.stringify({ reason: reason.trim(), version: data.version }) });
      setCancelling(false);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const pending = data.lines.filter((l) => l.quality === "Pending").length;
  const banner =
    a.status === "Quality"
      ? { tone: "bg-warn-soft text-warn", icon: FlaskConical, text: t("Лаборатория проверяет качество: ждут решения {n} поз. Принять на склад можно после проверки.", { n: pending }) }
      : a.status === "Ready" && !data.can.accept
        ? { tone: "bg-accent-soft text-accent-strong", icon: Scale, text: t("Качество проверено — кладовщик взвешивает и принимает на склад.") }
        : a.status === "Accepted"
          ? {
              tone: "bg-ok-soft text-ok",
              icon: CheckCircle2,
              text: t("Принято на склад {doc}: на {sum} сум · {who}", { doc: a.receiptNumber ?? "", sum: money(a.acceptedTotal), who: a.acceptedByName ?? "" }),
            }
          : a.status === "Returned"
            ? { tone: "bg-bad-soft text-bad", icon: Undo2, text: t("Забраковано всё — вернуть поставщику. На склад ничего не принято.") }
            : a.status === "Cancelled"
              ? { tone: "bg-muted text-ink-2", icon: Ban, text: t("Поставка отменена: {reason}", { reason: data.cancelReason ?? "" }) }
              : null;

  return (
    <>
      <PageHeader
        title={t("Поставка {n}", { n: a.number })}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span>{a.supplierName}</span>
            <ArrivalStatusPill status={a.status} />
            {a.vehicleNo && <span className="tabular-nums">{a.vehicleNo}</span>}
          </span>
        }
        back="/procurement/arrivals"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Поставки"), href: "/procurement/arrivals" },
          { label: a.number },
        ]}
        actions={
          data.can.cancel ? (
            <button type="button" onClick={() => setCancelling(true)} className={`${secondary} text-bad`}>
              <Ban className="size-4" aria-hidden /> {t("Отменить поставку")}
            </button>
          ) : undefined
        }
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <Steps data={data} />
          {banner && (
            <p className={`flex items-start gap-2 rounded-xl px-3.5 py-2.5 text-sm ${banner.tone}`}>
              <banner.icon className="mt-0.5 size-4 shrink-0" aria-hidden /> {banner.text}
            </p>
          )}
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              {a.status === "Ready" && data.can.accept && <AcceptForm data={data} />}
              <section className={card} aria-labelledby="ar-lines">
                <h2 id="ar-lines" className="mb-3 text-base font-semibold text-ink">
                  {t("Что привезли")} <span className="font-normal text-ink-3">· {data.lines.length}</span>
                </h2>
                <ul className="divide-y divide-line">
                  {data.lines.map((l) => (
                    <li key={l.id} className="flex gap-3 py-3 first:pt-0 last:pb-0">
                      <LineFacts line={l} />
                    </li>
                  ))}
                </ul>
              </section>
            </div>
            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label={a.sourceKind === "tender" ? "Тендер" : "Покупка «Куплено»"}>
                    {seesTenders ? (
                      <Link href={sourceHref(a.sourceKind, a.sourceId)} className="text-accent-strong hover:underline">
                        {a.sourceNumber}
                      </Link>
                    ) : (
                      <span className="tabular-nums">{a.sourceNumber}</span>
                    )}
                    <span className="block text-xs text-ink-3">{a.sourceTitle}</span>
                  </Fact>
                  {data.requestId && (
                    <Fact label="Заявка">
                      {seesRequests ? (
                        <Link href={`/requests/${data.requestId}`} className="text-accent-strong hover:underline">
                          {data.requestNumber}
                        </Link>
                      ) : (
                        <span className="tabular-nums">{data.requestNumber}</span>
                      )}
                    </Fact>
                  )}
                  <Fact label="Поставщик">{a.supplierName}</Fact>
                  <Fact label="Склад">{a.warehouseName}</Fact>
                  {a.vehicleNo && <Fact label="Машина">{a.vehicleNo}</Fact>}
                  {data.driverName && <Fact label="Водитель">{data.driverName}</Fact>}
                  {a.reference && <Fact label="Накладная">{a.reference}</Fact>}
                  <Fact label="Приехала">
                    {stampRu(a.arrivedAt)}
                    <span className="block text-xs text-ink-3">{a.registeredByName}</span>
                  </Fact>
                  {data.readyAt && a.status !== "Cancelled" && <Fact label="Качество проверено">{stampRu(data.readyAt)}</Fact>}
                  {a.acceptedAt && (
                    <Fact label="Принято">
                      {stampRu(a.acceptedAt)}
                      <span className="block text-xs text-ink-3">{a.acceptedByName}</span>
                    </Fact>
                  )}
                  {a.receiptNumber && (
                    <Fact label="Приход">
                      <Link href={`/procurement/movements?q=${encodeURIComponent(a.receiptNumber)}`} className="tabular-nums text-accent-strong hover:underline">
                        {a.receiptNumber}
                      </Link>
                    </Fact>
                  )}
                </dl>
                {data.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{data.comment}</p>}
              </section>
              <section className={card}>
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...data.history].reverse().map((e) => (
                    <li key={e.id} className="relative">
                      <span className="absolute -left-[21px] top-1.5 size-2.5 rounded-full bg-ink-4 ring-4 ring-surface" aria-hidden />
                      <p className="break-words text-sm text-ink">{t(e.text)}</p>
                      {e.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{e.comment}</p>}
                      <p className="mt-0.5 text-xs text-ink-3">
                        {e.actorName} · {stampRu(e.createdAt)}
                      </p>
                    </li>
                  ))}
                </ol>
              </section>
            </div>
          </div>
        </div>
      </PageBody>

      <Sheet
        open={cancelling}
        onClose={() => {
          setCancelling(false);
          setError(null);
        }}
        title={t("Отменить поставку")}
        subtitle={`${a.number} · ${a.supplierName}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setCancelling(false)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void cancel()} disabled={busy} className={`${primary} bg-bad hover:bg-bad`}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Отменить поставку")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Почему отменяется")}</span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={1000}
            rows={3}
            autoFocus
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none"
          />
          <span className="mt-2 block text-xs text-ink-3">{t("На склад ничего не приходовалось. Открытые проверки качества по поставке закроются.")}</span>
        </label>
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
