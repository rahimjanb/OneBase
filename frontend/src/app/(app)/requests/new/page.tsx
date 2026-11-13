import { RequestForm, type RequestPrefill } from "@/components/requests/RequestForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ItemCard, RequestFormData } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";
import { todayInTashkent } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новая заявка · OneBase") };
}

type Params = { kind?: string; item?: string; name?: string; unit?: string; qty?: string; price?: string; by?: string; reason?: string };

const number = (s: string | undefined) => {
  const v = s ? Number(s.replace(",", ".")) : NaN;
  return Number.isFinite(v) && v >= 0 ? v : null;
};

/**
 * Новая заявка на закупку: тип, позиции, причина, приоритет, желаемая дата, вложения; черновик или сразу отправка. ?kind=raw — сразу
 * «Закупка сырья» (если она пользователю разрешена). Из «Аналитика» (?item=…&qty=…&price=…&by=…&reason=…) — заполнена: позиция каталога,
 * количество, цена, «нужно к» и причина; высокий приоритет. Позиция недоступна по правам — подставляется название и единица.
 */
export default async function NewRequestPage({ searchParams }: { searchParams: Promise<Params> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const form = await apiGet<RequestFormData>("/api/procurement/form", "/requests/new");
  const raw = sp.kind === "raw" && form.canCreateRaw;
  let prefill: RequestPrefill | null = null;
  if (raw && (sp.item || sp.name)) {
    const card = sp.item && /^[0-9a-f-]{36}$/i.test(sp.item) ? await apiGetOrNull<ItemCard>(`/api/procurement/items/${sp.item}`, "/requests/new") : null;
    const name = card?.item.name ?? sp.name?.slice(0, 200) ?? "";
    const today = todayInTashkent();
    const by = sp.by && /^\d{4}-\d{2}-\d{2}$/.test(sp.by) && sp.by >= today ? sp.by : today;
    prefill = {
      title: t("Закупка сырья: {name}", { name }).slice(0, 200),
      reason: (sp.reason ?? "").slice(0, 1000),
      priority: "High",
      desiredDate: by,
      item: {
        itemId: card?.item.itemId ?? null,
        sku: card?.item.sku ?? null,
        name,
        categoryId: card?.item.categoryId ?? "",
        unitCode: card?.item.unitCode ?? sp.unit ?? "шт",
        available: card?.item.available ?? null,
        quantity: number(sp.qty) ?? 0,
        price: number(sp.price),
      },
    };
  }
  return (
    <>
      <PageHeader
        title={t("Новая заявка")}
        subtitle={t("Что нужно купить, сколько и зачем — заявку согласуют по маршруту и передадут в снабжение")}
        back="/requests/my"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Новая заявка") }]}
      />
      <PageBody>
        <RequestForm form={form} defaultKind={sp.kind === "raw" ? "Raw" : "Regular"} prefill={prefill} />
      </PageBody>
    </>
  );
}
