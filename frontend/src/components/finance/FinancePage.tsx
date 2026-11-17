import { FinanceFilters } from "@/components/finance/FinanceBits";
import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { FinanceOrganization } from "@/lib/finance";

/** Шапка страницы «Финансов»: название, пояснение, крошки и отбор (организация и период). */
export async function FinancePage({
  title,
  subtitle,
  crumbs = [],
  ctx,
  single = false,
  filters = true,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  ctx?: { organizations: FinanceOrganization[]; from: string; to: string; organization?: string };
  single?: boolean;
  filters?: boolean;
  children: React.ReactNode;
}) {
  const t = await getT();
  const base: Crumb[] = [{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Финансы"), href: "/finance" }];
  return (
    <>
      <PageHeader title={t(title)} subtitle={subtitle ? t(subtitle) : undefined} breadcrumbs={[...base, ...crumbs, ...(crumbs.length === 0 && title === "Финансы" ? [] : [{ label: t(title) }])]} />
      <PageBody>
        <div className="space-y-4">
          {filters && ctx && <FinanceFilters organizations={ctx.organizations} from={ctx.from} to={ctx.to} organization={ctx.organization} single={single} />}
          {children}
        </div>
      </PageBody>
    </>
  );
}
