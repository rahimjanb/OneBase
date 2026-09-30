import { notFound } from "next/navigation";
import { Download } from "lucide-react";
import { Section } from "@/components/sales/bits";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { apiGetOrNull } from "@/lib/server-api";
import { dateTime } from "@/lib/sales/format";

export const metadata = { title: "Документ базы знаний · OneBase" };

type DocumentContent = { id: string; title: string; fileName: string; departmentCode: string | null; createdAt: string; sizeBytes: number; chunks: string[] };

/** Документ базы знаний — источник ответа консультанта. Открывается, только если документ доступен пользователю. */
export default async function KnowledgeDocumentPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const doc = await apiGetOrNull<DocumentContent>(`/api/ai/knowledge/documents/${encodeURIComponent(id)}/content`, `/knowledge/${id}`);
  if (!doc) notFound();
  return (
    <>
      <PageHeader
        title={doc.title}
        subtitle={`${doc.fileName} · загружен ${dateTime(doc.createdAt)}`}
        back="/consultant"
        breadcrumbs={[{ label: "Консультант", href: "/consultant" }, { label: "База знаний" }, { label: doc.title }]}
        actions={
          <a
            href={`/bff/api/ai/knowledge/documents/${doc.id}/file`}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted"
          >
            <Download className="size-4" />
            Скачать оригинал
          </a>
        }
      />
      <PageBody>
        <Section title="Текст документа" hint={`${doc.chunks.length} фрагм. — так его видит AI`}>
          <div className="space-y-3">
            {doc.chunks.map((chunk, i) => (
              <div key={i} id={`fragment-${i + 1}`} className="rounded-lg border border-line px-4 py-3">
                <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-ink-3">Фрагмент {i + 1}</div>
                <p className="whitespace-pre-wrap text-sm leading-relaxed text-ink">{chunk}</p>
              </div>
            ))}
          </div>
        </Section>
      </PageBody>
    </>
  );
}
