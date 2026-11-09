"use client";

import { useEffect, useRef } from "react";
import { Download, ExternalLink, Loader2, RefreshCw, ScanText, Trash2, Upload } from "lucide-react";
import { button } from "@/components/ai/form";
import { Badge, Card, ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import {
  analysisStatusLabel,
  documentBusy,
  documentLabel,
  documentStatusLabel,
  ocrStatusLabel,
  recruitmentApi,
  sourceLabel,
  when,
  type CandidateCard,
  type CandidateDocument,
  type DocumentStatus,
  type ProfileField,
  type RecruitmentContext,
  type ResumeProfile,
} from "@/lib/recruitment";

const statusTone: Record<DocumentStatus, string> = {
  Received: "bg-muted text-ink-2",
  Stored: "bg-muted text-ink-2",
  Processing: "bg-accent-soft text-accent-strong",
  Done: "bg-ok-soft text-ok",
  Failed: "bg-bad-soft text-bad",
  Rejected: "bg-warn-soft text-warn",
};

/**
 * Карточка кандидата → «Документы»: все присланные файлы (каждая версия отдельно, ничего не перезаписывается), откуда пришли, статус обработки,
 * разбор резюме с пометками «не подтверждено» и «выведено», просмотр, скачивание оригинала, повторная обработка. Пока документ обрабатывается
 * в фоне — карточка обновляется сама.
 */
export function CandidateDocuments({ ctx, card, onCard }: { ctx: RecruitmentContext; card: CandidateCard; onCard: (c: CandidateCard) => void }) {
  const t = useT();
  const { busy, error, run } = useAction();
  const manage = ctx.canManage;
  const polls = useRef(0);
  const busyDocs = card.resumes.some(documentBusy);

  useEffect(() => {
    if (!busyDocs) {
      polls.current = 0;
      return;
    }
    if (polls.current > 40) return; // ~3 минуты; дальше — «Обновить» вручную
    const timer = setTimeout(() => {
      polls.current++;
      recruitmentApi.candidate(card.id).then(onCard, () => undefined);
    }, 4000);
    return () => clearTimeout(timer);
  }, [busyDocs, card, onCard]);

  const act = (what: string, action: () => Promise<CandidateCard>) => run(what, async () => onCard(await action()));
  const resumes = card.resumes.filter((d) => d.documentType == null || d.documentType === "resume");

  return (
    <Card
      title="Документы"
      actions={
        manage && (
          <label className={`${button} cursor-pointer`}>
            {busy === "upload" ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
            {t("Загрузить")}
            <input
              type="file"
              className="hidden"
              accept=".pdf,.docx,.doc,.rtf,.odt,.txt,.jpg,.jpeg,.png,.webp"
              onChange={(e) => {
                const file = e.target.files?.[0];
                e.target.value = "";
                if (file) act("upload", () => recruitmentApi.uploadResume(card.id, file));
              }}
            />
          </label>
        )
      }
    >
      {card.resumes.length === 0 && (
        <p className="text-sm text-ink-3">
          {t("Документов пока нет. Резюме из Telegram появятся здесь сами; можно загрузить PDF, Word, RTF, ODT, TXT или фото.")}
        </p>
      )}
      <ul className="space-y-3">
        {card.resumes.map((d) => (
          <DocumentRow
            key={d.id}
            d={d}
            version={resumes.length > 1 && resumes.includes(d) ? { n: resumes.length - resumes.indexOf(d), of: resumes.length } : null}
            manage={manage}
            busy={busy}
            onAct={act}
          />
        ))}
      </ul>
      <ErrorText error={error} />
    </Card>
  );
}

function DocumentRow({
  d,
  version,
  manage,
  busy,
  onAct,
}: {
  d: CandidateDocument;
  version: { n: number; of: number } | null;
  manage: boolean;
  busy: string | null;
  onAct: (what: string, action: () => Promise<CandidateCard>) => void;
}) {
  const t = useT();
  const stored = d.status !== "Received";
  const image = d.canPreview && (d.contentType ?? "").startsWith("image/");
  const meta = [
    (d.detectedType ?? d.fileName.split(".").pop() ?? "").toUpperCase(),
    `${Math.max(1, Math.ceil(d.sizeBytes / 1024))} ${t("КБ")}`,
    d.pageCount ? t("{n} стр.", { n: d.pageCount }) : null,
    t(sourceLabel[d.source] ?? d.source),
    when(d.createdAt),
    d.vacancyCode ? t("отклик {code}", { code: d.vacancyCode }) : null,
  ].filter(Boolean);

  return (
    <li className="rounded-lg border border-line p-3 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="break-all font-medium">{d.fileName}</span>
            <Badge className={statusTone[d.status]}>
              {documentBusy(d) && <Loader2 className="mr-1 size-3 animate-spin" />}
              {t(documentStatusLabel[d.status])}
            </Badge>
            {version && <span className="text-xs text-ink-3">{t("версия {n} из {of}", version)}</span>}
            {d.documentType && d.documentType !== "resume" && <Badge>{t(documentLabel[d.documentType] ?? d.documentType)}</Badge>}
          </div>
          <p className="mt-0.5 text-xs text-ink-3">{meta.join(" · ")}</p>
        </div>
        <div className="flex flex-wrap items-center gap-3 text-xs">
          {d.canPreview && (
            <a className="inline-flex items-center gap-1 text-accent-strong hover:underline" href={recruitmentApi.previewUrl(d.id)} target="_blank" rel="noreferrer">
              <ExternalLink className="size-4" /> {t("Открыть")}
            </a>
          )}
          {stored && (
            <a className="inline-flex items-center gap-1 text-accent-strong hover:underline" href={recruitmentApi.resumeUrl(d.id)}>
              <Download className="size-4" /> {t("Скачать оригинал")}
            </a>
          )}
          {manage && d.status !== "Rejected" && !documentBusy(d) && (
            <button
              className="inline-flex items-center gap-1 text-accent-strong hover:underline disabled:opacity-50"
              disabled={!!busy}
              onClick={() => onAct(`re:${d.id}`, () => recruitmentApi.reprocessResume(d.id))}
            >
              {busy === `re:${d.id}` ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />} {t("Обработать заново")}
            </button>
          )}
          {manage && d.canReadAi && !documentBusy(d) && d.documentType !== "id_document" && (
            <button
              className="inline-flex items-center gap-1 text-accent-strong hover:underline disabled:opacity-50"
              disabled={!!busy}
              title={t("Платный вызов AI: модель читает фото или PDF заново")}
              onClick={() => onAct(`ai:${d.id}`, () => recruitmentApi.readResume(d.id))}
            >
              {busy === `ai:${d.id}` ? <Loader2 className="size-4 animate-spin" /> : <ScanText className="size-4" />} {t("Распознать заново (AI)")}
            </button>
          )}
          {manage && (
            <button
              className="text-ink-3 hover:text-bad"
              title={t("Удалить документ")}
              onClick={() => {
                if (confirm(t("Удалить «{name}» вместе с оригиналом? Это необратимо.", { name: d.fileName }))) onAct("delete", () => recruitmentApi.deleteResume(d.id));
              }}
            >
              <Trash2 className="size-4" />
            </button>
          )}
        </div>
      </div>

      {stored && d.status !== "Rejected" && (
        <p className="mt-1 text-xs text-ink-3">
          {t("Текст: {ocr} · разбор: {analysis}", { ocr: t(ocrStatusLabel[d.ocrStatus]), analysis: t(analysisStatusLabel[d.analysisStatus]) })}
        </p>
      )}
      {(d.processingError || d.textError) && <p className="mt-1 text-xs text-warn">{t(d.processingError ?? d.textError ?? "")}</p>}

      {image && (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={recruitmentApi.previewUrl(d.id)} alt={d.fileName} className="mt-2 max-h-56 rounded border border-line object-contain" loading="lazy" />
      )}

      {d.summary && <p className="mt-2 text-ink-2">{d.summary}</p>}
      {d.profile && <ProfileView p={d.profile} />}

      {d.textPreview && (
        <details className="mt-2">
          <summary className="cursor-pointer text-xs text-ink-3">
            {t("Текст документа")}
            {d.textByAi && <Badge className="ml-2 bg-accent-soft text-accent-strong">{t("распознал AI")}</Badge>}
          </summary>
          <p className="mt-1 whitespace-pre-line text-xs text-ink-2">{d.textPreview}</p>
        </details>
      )}
    </li>
  );
}

/** Разбор резюме: проверенное — как есть; непроверенное (цитаты нет в тексте или текст прочитан плохо) и выведенное — помечены. */
function ProfileView({ p }: { p: ResumeProfile }) {
  const t = useT();
  const rows: [string, ProfileField | null | undefined][] = [
    ["ФИО", p.fullName],
    ["Должность", p.professionalTitle],
    ["Стаж, лет", p.experienceYears],
    ["Телефон", p.phone],
    ["Почта", p.email],
    ["Город", p.location],
    ["Ожидания по зарплате", p.salaryExpectation],
    ["Дополнительно", p.additionalInformation],
  ];
  const shown = rows.filter(([, f]) => f?.value);
  const lists: [string, ProfileField[]][] = [
    ["Навыки", p.skills],
    ["Языки", p.languages],
    ["Сертификаты", p.certificates],
  ];
  if (shown.length === 0 && p.workExperience.length === 0 && p.education.length === 0 && lists.every(([, l]) => l.length === 0)) return null;

  return (
    <details className="mt-2" open>
      <summary className="cursor-pointer text-xs text-ink-3">
        {t("Из резюме")}
        {p.quality !== "good" && <span className="ml-2 text-warn">{t("текст распознан с неразборчивыми местами — проверьте поля")}</span>}
      </summary>
      <dl className="mt-2 grid gap-x-6 gap-y-1 text-xs sm:grid-cols-[auto_1fr]">
        {shown.map(([label, f]) => (
          <Row key={label} label={label}>
            <Value f={f!} />
          </Row>
        ))}
        {p.workExperience.length > 0 && (
          <Row label="Опыт">
            <ul className="space-y-0.5">
              {p.workExperience.map((w, i) => (
                <li key={i}>
                  {[w.from && w.to ? `${w.from}–${w.to}` : w.from ?? w.to, w.company, w.position].filter(Boolean).join(" · ")}
                  {!w.verified && <Mark text="не подтверждено" />}
                </li>
              ))}
            </ul>
          </Row>
        )}
        {p.education.length > 0 && (
          <Row label="Образование">
            <ul className="space-y-0.5">
              {p.education.map((e, i) => (
                <li key={i}>
                  {[e.institution, e.degree, e.year].filter(Boolean).join(" · ")}
                  {!e.verified && <Mark text="не подтверждено" />}
                </li>
              ))}
            </ul>
          </Row>
        )}
        {lists
          .filter(([, l]) => l.length > 0)
          .map(([label, l]) => (
            <Row key={label} label={label}>
              <span className="flex flex-wrap gap-1">
                {l.map((f, i) => (
                  <span key={i} className={`rounded px-1.5 py-0.5 ${f.verified ? "bg-muted text-ink-2" : "border border-dashed border-line text-ink-3"}`} title={f.evidence ?? undefined}>
                    {f.value}
                  </span>
                ))}
              </span>
            </Row>
          ))}
      </dl>
    </details>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <>
      <dt className="text-ink-3">{t(label)}</dt>
      <dd className="text-ink-2">{children}</dd>
    </>
  );
}

function Value({ f }: { f: ProfileField }) {
  return (
    <span title={f.evidence ? `«${f.evidence}»` : undefined}>
      {f.value}
      {f.basis === "inferred" ? <Mark text="выведено" /> : !f.verified && <Mark text="не подтверждено" />}
    </span>
  );
}

function Mark({ text }: { text: string }) {
  const t = useT();
  return <span className="ml-1.5 text-ink-3">· {t(text)}</span>;
}
