import Link from "next/link";
import { redirect } from "next/navigation";
import { Plus } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { canOpen, homeFor } from "@/lib/navigation";
import { requestFilterKeys, type RequestFormData, type RequestNav, type RequestPage } from "@/lib/procurement";
import { apiGet, apiSession } from "@/lib/server-api";
import { qs } from "@/lib/tasks";
import type { Me } from "@/lib/users";
import { RequestFilters } from "./RequestFilters";
import { RequestTable } from "./RequestTable";

export type SearchParams = Record<string, string | string[] | undefined>;

const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Раздел недоступен (нет прав «Заявки») — на стартовую страницу пользователя, а не на ошибку. */
export async function requireRequests() {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/requests", me.permissions)) redirect(homeFor(me.permissions));
  return me;
}

/** «Новая заявка» — кнопка в шапке страниц раздела. */
export async function NewRequestButton() {
  const t = await getT();
  return (
    <Link href="/requests/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
      <Plus className="size-4" aria-hidden /> {t("Новая заявка")}
    </Link>
  );
}

/**
 * Страница-список раздела «Заявки» (все, мои, отдела): кнопки раздела, «Новая заявка», фильтры и таблица. Данные — с сервера по
 * параметрам адреса; что видно, решает API.
 */
export async function RequestListPage({ scope, title, subtitle, sp, returnTo, empty }: { scope: "all" | "my" | "department"; title: string; subtitle: string; sp: SearchParams; returnTo: string; empty: string }) {
  await requireRequests();
  const t = await getT();
  const filters = Object.fromEntries(requestFilterKeys.map((k) => [k, one(sp, k)]));
  const [data, form, nav] = await Promise.all([
    apiGet<RequestPage>(`/api/procurement/requests${qs({ ...filters, scope, pageSize: 25 })}`, returnTo),
    apiGet<RequestFormData>("/api/procurement/form", returnTo),
    apiGet<RequestNav>("/api/procurement/nav", returnTo),
  ]);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: title }]}
        actions={nav.canCreate ? <NewRequestButton /> : undefined}
      />
      <PageBody>
        <RequestFilters categories={form.categories} departments={scope === "my" ? [] : form.departments} />
        <RequestTable data={data} empty={empty} />
      </PageBody>
    </>
  );
}
