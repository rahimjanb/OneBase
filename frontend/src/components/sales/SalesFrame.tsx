import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { periodQuery, type SalesSearchParams } from "@/lib/sales/query";

/**
 * Заголовок и тело страницы раздела «Продажи»: «← Назад» слева, крошки, название.
 * Разделы — в верхней панели, период и «Обновить» — в полосе раздела (sales/layout.tsx).
 */
export function SalesFrame({
  title,
  subtitle,
  crumbs = [],
  back,
  sp,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  back?: string;
  sp: SalesSearchParams;
  children: React.ReactNode;
}) {
  const query = periodQuery(sp);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Продажи", href: query ? `/sales?${query}` : "/sales" }, ...crumbs]}
        back={back}
      />
      <PageBody>{children}</PageBody>
    </>
  );
}
