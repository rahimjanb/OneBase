import { notFound } from "next/navigation";
import { DocumentView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import { docTypeLabel, type FinanceDocument } from "@/lib/finance";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Документ 1С · Финансы · OneBase") };
}

/** Документ 1С: реквизиты, суммы в валюте и в валюте учёта, курс, строки и проводки — только просмотр (менять — в 1С). */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  await requireFinance();
  const t = await getT();
  const { id } = await params;
  const doc = await apiGetOrNull<FinanceDocument>(`/api/finance/documents/${encodeURIComponent(id)}`, `/finance/documents/${id}`);
  if (!doc) notFound();
  return (
    <FinancePage title={`${t(docTypeLabel[doc.type] ?? doc.sourceType)} № ${doc.number}`} crumbs={[{ label: "Проводки", href: "/finance/entries" }, { label: `№ ${doc.number}` }]} filters={false}>
      <DocumentView doc={doc} />
    </FinancePage>
  );
}
