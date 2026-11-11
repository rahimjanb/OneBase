"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { BadgeCheck, Download, Loader2, Plus, Trash2, Upload } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { CandidateDocuments } from "@/components/recruitment/Documents";
import { CandidateExtras } from "@/components/recruitment/Flow";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import {
  consentLabel,
  contactLabel,
  day,
  documentLabel,
  LANGUAGE_CODES,
  languageCodeLabel,
  languageSourceLabel,
  recruitmentApi,
  sourceLabel,
  STAGES,
  stageLabel,
  stageTone,
  when,
  type CandidateCard,
  type CandidateInput,
  type CandidatePage,
  type ContactKind,
  type RecruitmentContext,
  type Stage,
  type VacancyRow,
} from "@/lib/recruitment";

const KINDS: ContactKind[] = ["Phone", "Email", "TelegramUsername", "Telegram"];

/** Новый кандидат вручную: имя, контакты, источник, вакансия, согласие. Тот же человек по подтверждённому контакту — откроется его карточка. */
function CandidateForm({ vacancies, vacancyId, onDone, onCancel }: { vacancies: VacancyRow[]; vacancyId: string | null; onDone: (id: string, note: string | null) => void; onCancel: () => void }) {
  const t = useT();
  const [input, setInput] = useState<CandidateInput>({
    fullName: "",
    city: null,
    language: null,
    notes: null,
    contacts: [{ kind: "Phone", value: "" }],
    source: "manual",
    sourceNote: null,
    vacancyId,
    consentChannel: null,
  });
  const { busy, error, run } = useAction();
  const set = (patch: Partial<CandidateInput>) => setInput((x) => ({ ...x, ...patch }));
  return (
    <form
      className="grid gap-3 sm:grid-cols-2"
      onSubmit={(e) => {
        e.preventDefault();
        run("save", async () => {
          const result = await recruitmentApi.createCandidate({ ...input, contacts: input.contacts.filter((c) => c.value.trim()) });
          onDone(
            result.candidateId,
            !result.created ? t("Этот человек уже есть в базе — открыта его карточка.") : result.duplicates > 0 ? t("Похоже на дубль: совпал контакт с другим кандидатом.") : null,
          );
        });
      }}
    >
      <Label label="Имя и фамилия">
        <input className={field} value={input.fullName} onChange={(e) => set({ fullName: e.target.value })} maxLength={200} required />
      </Label>
      <Label label="Город">
        <input className={field} value={input.city ?? ""} onChange={(e) => set({ city: e.target.value || null })} maxLength={100} />
      </Label>
      <div className="space-y-2 sm:col-span-2">
        {input.contacts.map((c, i) => (
          <div key={i} className="flex gap-2">
            <select
              className={`${field} w-40`}
              value={c.kind}
              onChange={(e) => set({ contacts: input.contacts.map((x, j) => (j === i ? { ...x, kind: e.target.value as ContactKind } : x)) })}
            >
              {KINDS.map((k) => (
                <option key={k} value={k}>
                  {t(contactLabel[k])}
                </option>
              ))}
            </select>
            <input
              className={field}
              value={c.value}
              placeholder={c.kind === "Phone" ? "+998 90 123 45 67" : c.kind === "Email" ? "name@example.com" : c.kind === "TelegramUsername" ? "@username" : "123456789"}
              onChange={(e) => set({ contacts: input.contacts.map((x, j) => (j === i ? { ...x, value: e.target.value } : x)) })}
            />
          </div>
        ))}
        <button type="button" className={button} onClick={() => set({ contacts: [...input.contacts, { kind: "Email", value: "" }] })}>
          <Plus className="size-4" />
          {t("Ещё контакт")}
        </button>
      </div>
      <Label label="Откуда">
        <select className={field} value={input.source} onChange={(e) => set({ source: e.target.value })}>
          {["manual", "referral", "other"].map((s) => (
            <option key={s} value={s}>
              {t(sourceLabel[s])}
            </option>
          ))}
        </select>
      </Label>
      <Label label="Вакансия">
        <select className={field} value={input.vacancyId ?? ""} onChange={(e) => set({ vacancyId: e.target.value || null })}>
          <option value="">{t("Без вакансии")}</option>
          {vacancies
            .filter((v) => v.status === "Open" || v.status === "Paused")
            .map((v) => (
              <option key={v.id} value={v.id}>
                {v.code} · {v.title}
              </option>
            ))}
        </select>
      </Label>
      <Label label="Согласие на обработку данных" hint="Как кандидат дал согласие, если HR его получил. AI-рекрутер согласия не спрашивает.">
        <select className={field} value={input.consentChannel ?? ""} onChange={(e) => set({ consentChannel: e.target.value || null })}>
          <option value="">{t("Пока нет")}</option>
          {Object.entries(consentLabel).map(([k, label]) => (
            <option key={k} value={k}>
              {t(label)}
            </option>
          ))}
        </select>
      </Label>
      <Label label="Язык общения">
        <select className={field} value={input.language ?? ""} onChange={(e) => set({ language: e.target.value || null })}>
          <option value="">—</option>
          <option value="ru">{t("Русский")}</option>
          <option value="uz">{t("Узбекский")}</option>
        </select>
      </Label>
      <Label label="Заметка" className="sm:col-span-2">
        <textarea className={textarea} value={input.notes ?? ""} onChange={(e) => set({ notes: e.target.value || null })} maxLength={4000} />
      </Label>
      <div className="flex gap-2 sm:col-span-2">
        <button type="submit" className={primary} disabled={!!busy || !input.fullName.trim()}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Добавить")}
        </button>
        <button type="button" className={button} onClick={onCancel}>
          {t("Отмена")}
        </button>
      </div>
      <div className="sm:col-span-2">
        <ErrorText error={error} />
      </div>
    </form>
  );
}

/** База кандидатов: поиск по имени и контактам, фильтр по вакансии, этапу и подозрениям на дубль. */
export function CandidatesView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const router = useRouter();
  const params = useSearchParams();
  const [page, setPage] = useState<CandidatePage | null>(null);
  const [vacancies, setVacancies] = useState<VacancyRow[]>([]);
  const [q, setQ] = useState({ search: "", vacancyId: params.get("vacancyId") ?? "", stage: "", duplicates: false, page: 1 });
  const [creating, setCreating] = useState(params.get("new") === "1" && ctx.canManage);
  const { error, run } = useAction();

  useEffect(() => {
    recruitmentApi.vacancies().then(setVacancies, () => setVacancies([]));
  }, []);
  useEffect(() => {
    const timer = setTimeout(() => run("load", async () => setPage(await recruitmentApi.candidates(q))), 250);
    return () => clearTimeout(timer);
  }, [q]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Label label="Поиск">
          <input className={field} value={q.search} onChange={(e) => setQ({ ...q, search: e.target.value, page: 1 })} placeholder={t("Имя, телефон, e-mail")} />
        </Label>
        <Label label="Вакансия">
          <select className={field} value={q.vacancyId} onChange={(e) => setQ({ ...q, vacancyId: e.target.value, page: 1 })}>
            <option value="">{t("Все")}</option>
            {vacancies.map((v) => (
              <option key={v.id} value={v.id}>
                {v.code} · {v.title}
              </option>
            ))}
          </select>
        </Label>
        <Label label="Этап">
          <select className={field} value={q.stage} onChange={(e) => setQ({ ...q, stage: e.target.value, page: 1 })}>
            <option value="">{t("Любой")}</option>
            {STAGES.map((s) => (
              <option key={s} value={s}>
                {t(stageLabel[s])}
              </option>
            ))}
          </select>
        </Label>
        <label className="mb-2 flex items-center gap-2 text-sm text-ink-2">
          <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={q.duplicates} onChange={(e) => setQ({ ...q, duplicates: e.target.checked, page: 1 })} />
          {t("Возможные дубли")}
        </label>
        {ctx.canManage && (
          <button className={`${primary} ml-auto`} onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            {t("Новый кандидат")}
          </button>
        )}
      </div>

      {creating && (
        <Card title="Новый кандидат">
          <CandidateForm
            vacancies={vacancies}
            vacancyId={q.vacancyId || null}
            onCancel={() => setCreating(false)}
            onDone={(id, note) => router.push(`/hr/recruitment/candidates/${id}${note ? `?note=${encodeURIComponent(note)}` : ""}`)}
          />
        </Card>
      )}

      <ErrorText error={error} />
      {!page ? (
        <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
      ) : page.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line p-6 text-center text-sm text-ink-3">{t("Кандидатов не найдено.")}</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line bg-surface">
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="px-4 py-2.5 font-semibold">{t("Кандидат")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Вакансии и этапы")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Активность")}</th>
                <th className="px-4 py-2.5 font-semibold">{t("Данные удалятся")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {page.items.map((c) => (
                <tr key={c.id} className="hover:bg-muted/40">
                  <td className="px-4 py-2.5">
                    <Link href={`/hr/recruitment/candidates/${c.id}`} className="font-medium text-ink hover:text-accent-strong">
                      {c.fullName}
                    </Link>
                    <div className="flex flex-wrap gap-1 text-xs text-ink-3">
                      {c.phone ?? "—"}
                      {c.city && ` · ${c.city}`}
                      {c.openDuplicates > 0 && <Badge className="bg-bad-soft text-bad">{t("дубль?")}</Badge>}
                    </div>
                  </td>
                  <td className="px-3 py-2.5">
                    <div className="flex flex-wrap gap-1">
                      {c.applications.length === 0 && <span className="text-ink-3">—</span>}
                      {c.applications.map((a) => (
                        <Badge key={a.vacancyId} className={stageTone[a.stage]}>
                          {a.vacancyCode}: {t(stageLabel[a.stage])}
                        </Badge>
                      ))}
                    </div>
                  </td>
                  <td className="px-3 py-2.5 text-ink-2">{when(c.lastActivityAt)}</td>
                  <td className="px-4 py-2.5 text-ink-2">{c.deleteAt ? day(c.deleteAt) : t("идёт подбор")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {page && page.total > page.pageSize && (
        <div className="flex items-center gap-2 text-sm">
          <button className={button} disabled={q.page <= 1} onClick={() => setQ({ ...q, page: q.page - 1 })}>
            ←
          </button>
          <span className="text-ink-2">
            {q.page} / {Math.ceil(page.total / page.pageSize)}
          </span>
          <button className={button} disabled={q.page * page.pageSize >= page.total} onClick={() => setQ({ ...q, page: q.page + 1 })}>
            →
          </button>
        </div>
      )}
    </div>
  );
}

/** Карточка кандидата: данные, контакты, резюме, вакансии и этапы, согласие, дубли, удаление данных. */
export function CandidateCardView({ ctx, id }: { ctx: RecruitmentContext; id: string }) {
  const t = useT();
  const router = useRouter();
  const params = useSearchParams();
  const [c, setC] = useState<CandidateCard | null>(null);
  const [vacancies, setVacancies] = useState<VacancyRow[]>([]);
  const [editing, setEditing] = useState(false);
  const [info, setInfo] = useState({ fullName: "", city: "", language: "", notes: "" });
  const [contact, setContact] = useState<{ kind: ContactKind; value: string }>({ kind: "Phone", value: "" });
  const [vacancyId, setVacancyId] = useState("");
  const [stageFor, setStageFor] = useState<{ id: string; stage: Stage; reason: string; comment: string } | null>(null);
  const [consent, setConsent] = useState("written");
  const { busy, error, run } = useAction();
  const manage = ctx.canManage;

  useEffect(() => {
    run("load", async () => setC(await recruitmentApi.candidate(id)));
    recruitmentApi.vacancies().then(setVacancies, () => setVacancies([]));
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!c) return error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;
  const act = (what: string, action: () => Promise<CandidateCard>) => run(what, async () => setC(await action()));
  const note = params.get("note");

  return (
    <div className="space-y-4">
      {note && <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{note}</p>}
      <Card
        title={c.fullName}
        actions={
          manage && (
            <div className="flex gap-2">
              {!editing && (
                <button
                  className={button}
                  onClick={() => {
                    setInfo({
                      fullName: c.fullName,
                      city: c.city ?? "",
                      language: c.languageInfo?.code && c.languageInfo.source !== "auto" ? c.languageInfo.code : "auto",
                      notes: c.notes ?? "",
                    });
                    setEditing(true);
                  }}
                >
                  {t("Изменить")}
                </button>
              )}
              <button
                className={danger}
                disabled={!!busy}
                onClick={() => {
                  if (confirm(t("Удалить все данные кандидата «{name}»: резюме, контакты, отклики, согласия? Это необратимо.", { name: c.fullName })))
                    run("delete", async () => {
                      await recruitmentApi.deleteCandidate(c.id);
                      router.push("/hr/recruitment/candidates");
                    });
                }}
              >
                <Trash2 className="size-4" />
                {t("Удалить данные")}
              </button>
            </div>
          )
        }
      >
        {editing ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <Label label="Имя и фамилия">
              <input className={field} value={info.fullName} onChange={(e) => setInfo({ ...info, fullName: e.target.value })} />
            </Label>
            <Label label="Город">
              <input className={field} value={info.city} onChange={(e) => setInfo({ ...info, city: e.target.value })} />
            </Label>
            <Label label="Язык переписки">
              <select className={field} value={info.language} onChange={(e) => setInfo({ ...info, language: e.target.value })}>
                <option value="auto">{t("По переписке (определяется сам)")}</option>
                {LANGUAGE_CODES.map((code) => (
                  <option key={code} value={code}>
                    {t(languageCodeLabel[code])}
                  </option>
                ))}
              </select>
            </Label>
            <Label label="Заметка" className="sm:col-span-3">
              <textarea className={textarea} value={info.notes} onChange={(e) => setInfo({ ...info, notes: e.target.value })} />
            </Label>
            <div className="flex gap-2 sm:col-span-3">
              <button
                className={primary}
                disabled={!!busy || !info.fullName.trim()}
                onClick={() =>
                  act("info", async () => {
                    const card = await recruitmentApi.updateCandidate(c.id, {
                      fullName: info.fullName,
                      city: info.city || null,
                      language: c.language,
                      notes: info.notes || null,
                      languageCode: info.language || "auto",
                    });
                    setEditing(false);
                    return card;
                  })
                }
              >
                {t("Сохранить")}
              </button>
              <button className={button} onClick={() => setEditing(false)}>
                {t("Отмена")}
              </button>
            </div>
          </div>
        ) : (
          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
            <dt className="text-ink-3">{t("Город")}</dt>
            <dd>{c.city ?? "—"}</dd>
            <dt className="text-ink-3">{t("Язык переписки")}</dt>
            <dd>
              <CandidateLanguage info={c.languageInfo} />
            </dd>
            <dt className="text-ink-3">{t("Источники")}</dt>
            <dd>{c.sources.map((s) => t(sourceLabel[s.source] ?? s.source) + (s.note ? ` (${s.note})` : "")).join(", ") || "—"}</dd>
            <dt className="text-ink-3">{t("Данные удалятся")}</dt>
            <dd>
              {c.deleteAt
                ? t("{date} — через {days} дн. после завершения подбора", { date: day(c.deleteAt), days: c.retentionDays })
                : t("не удаляются, пока идёт подбор")}
            </dd>
            {c.notes && (
              <>
                <dt className="text-ink-3">{t("Заметка")}</dt>
                <dd className="whitespace-pre-line">{c.notes}</dd>
              </>
            )}
          </dl>
        )}
        <ErrorText error={error} />
      </Card>

      {c.duplicates.length > 0 && (
        <Card title="Возможные дубли">
          <ul className="space-y-2 text-sm">
            {c.duplicates.map((d) => (
              <li key={d.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  <Link href={`/hr/recruitment/candidates/${d.otherCandidateId}`} className="font-medium text-accent-strong hover:underline">
                    {d.otherName}
                  </Link>{" "}
                  — {t("совпал контакт")} {t(contactLabel[d.kind])}: {d.value}
                </span>
                {manage && (
                  <span className="flex gap-2">
                    <button className={button} disabled={!!busy} onClick={() => act("merge", () => recruitmentApi.merge(d.id, c.id, c.id))}>
                      {t("Объединить в эту карточку")}
                    </button>
                    <button className={button} disabled={!!busy} onClick={() => act("dismiss", () => recruitmentApi.dismiss(d.id, c.id))}>
                      {t("Это разные люди")}
                    </button>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title="Контакты">
          <ul className="space-y-1 text-sm">
            {c.contacts.length === 0 && <li className="text-ink-3">{t("Контактов нет.")}</li>}
            {c.contacts.map((x) => (
              <li key={x.id} className="flex items-center justify-between gap-2">
                <span>
                  <span className="text-ink-3">{t(contactLabel[x.kind])}:</span> {x.value}{" "}
                  {x.verified && <BadgeCheck className="inline size-4 text-ok" aria-label={t("подтверждён")} />}
                </span>
                {manage && (
                  <button className="text-ink-3 hover:text-bad" title={t("Удалить")} onClick={() => act("contact", () => recruitmentApi.removeContact(c.id, x.id))}>
                    <Trash2 className="size-4" />
                  </button>
                )}
              </li>
            ))}
          </ul>
          {manage && (
            <div className="mt-3 flex gap-2">
              <select className={`${field} w-36`} value={contact.kind} onChange={(e) => setContact({ ...contact, kind: e.target.value as ContactKind })}>
                {KINDS.map((k) => (
                  <option key={k} value={k}>
                    {t(contactLabel[k])}
                  </option>
                ))}
              </select>
              <input className={field} value={contact.value} onChange={(e) => setContact({ ...contact, value: e.target.value })} />
              <button
                className={button}
                disabled={!!busy || !contact.value.trim()}
                onClick={() =>
                  act("contact", async () => {
                    const card = await recruitmentApi.addContact(c.id, contact.kind, contact.value);
                    setContact({ ...contact, value: "" });
                    return card;
                  })
                }
              >
                <Plus className="size-4" />
              </button>
            </div>
          )}
        </Card>

        <Card title="Согласие на обработку данных">
          <ul className="space-y-1 text-sm">
            {c.consents.length === 0 && <li className="text-warn">{t("Согласия нет — AI-рекрутер этому кандидату не пишет.")}</li>}
            {c.consents.map((x) => (
              <li key={x.id} className="flex items-center justify-between gap-2">
                <span>
                  {t(consentLabel[x.channel] ?? x.channel)}, {when(x.givenAt)} · {t("текст v{n}", { n: x.textVersion })}
                  {x.recordedBy && ` · ${x.recordedBy}`}
                  {x.withdrawnAt && <span className="text-bad"> · {t("отозвано")}</span>}
                </span>
                {manage && !x.withdrawnAt && (
                  <button
                    className={danger}
                    onClick={() => {
                      if (confirm(t("Кандидат отозвал согласие? Все его данные будут удалены сразу.")))
                        run("withdraw", async () => {
                          await recruitmentApi.withdrawConsent(x.id);
                          router.push("/hr/recruitment/candidates");
                        });
                    }}
                  >
                    {t("Отозвал")}
                  </button>
                )}
              </li>
            ))}
          </ul>
          {manage && (
            <div className="mt-3 flex gap-2">
              <select className={field} value={consent} onChange={(e) => setConsent(e.target.value)}>
                {Object.entries(consentLabel).map(([k, label]) => (
                  <option key={k} value={k}>
                    {t(label)}
                  </option>
                ))}
              </select>
              <button className={button} disabled={!!busy} onClick={() => act("consent", () => recruitmentApi.recordConsent(c.id, consent))}>
                {t("Записать согласие")}
              </button>
            </div>
          )}
        </Card>
      </div>

      <Card title="Вакансии и этапы">
        {c.applications.length === 0 && <p className="text-sm text-ink-3">{t("Кандидат пока не рассматривается ни на одну вакансию.")}</p>}
        <ul className="space-y-3">
          {c.applications.map((a) => (
            <li key={a.id} className="rounded-lg border border-line p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Link href={`/hr/recruitment/vacancies/${a.vacancyId}`} className="font-medium text-ink hover:text-accent-strong">
                  {a.vacancyCode} · {a.vacancyTitle}
                </Link>
                <div className="flex items-center gap-2">
                  <Badge className={stageTone[a.stage]}>{t(stageLabel[a.stage])}</Badge>
                  {manage && (
                    <select
                      className={`${field} h-8 w-44`}
                      value=""
                      onChange={(e) => e.target.value && setStageFor({ id: a.id, stage: e.target.value as Stage, reason: "", comment: "" })}
                    >
                      <option value="">{t("Сменить этап…")}</option>
                      {STAGES.filter((s) => s !== a.stage).map((s) => (
                        <option key={s} value={s}>
                          {t(stageLabel[s])}
                        </option>
                      ))}
                    </select>
                  )}
                </div>
              </div>
              {a.rejectReason && <p className="mt-1 text-xs text-bad">{t("Причина отказа")}: {a.rejectReason}</p>}
              {stageFor?.id === a.id && (
                <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_1fr_auto]">
                  {stageFor.stage === "Rejected" && (
                    <input className={field} placeholder={t("Причина отказа (обязательно)")} value={stageFor.reason} onChange={(e) => setStageFor({ ...stageFor, reason: e.target.value })} />
                  )}
                  <input className={field} placeholder={t("Комментарий")} value={stageFor.comment} onChange={(e) => setStageFor({ ...stageFor, comment: e.target.value })} />
                  <div className="flex gap-2">
                    <button
                      className={primary}
                      disabled={!!busy || (stageFor.stage === "Rejected" && !stageFor.reason.trim())}
                      onClick={() =>
                        act("stage", async () => {
                          const card = await recruitmentApi.stage(a.id, stageFor.stage, stageFor.comment || null, stageFor.reason || null);
                          setStageFor(null);
                          return card;
                        })
                      }
                    >
                      {t("→ {stage}", { stage: t(stageLabel[stageFor.stage]) })}
                    </button>
                    <button className={button} onClick={() => setStageFor(null)}>
                      {t("Отмена")}
                    </button>
                  </div>
                </div>
              )}
              {a.screening && Object.keys(a.screening).length > 0 && (
                <div className="mt-2 rounded-lg bg-muted/40 px-3 py-2 text-xs">
                  <p className="mb-1 font-medium text-ink-2">{t("Ответы на вопросы отбора (AI-рекрутер)")}</p>
                  <dl className="space-y-0.5">
                    {Object.entries(a.screening).map(([q, ans]) => (
                      <div key={q}>
                        <dt className="inline text-ink-3">{q}: </dt>
                        <dd className="inline text-ink">{ans}</dd>
                      </div>
                    ))}
                  </dl>
                </div>
              )}
              <details className="mt-2 text-xs text-ink-3">
                <summary className="cursor-pointer">{t("История")}</summary>
                <ul className="mt-1 space-y-0.5">
                  {a.history.map((h, i) => (
                    <li key={i}>
                      {when(h.at)} — {h.from ? `${t(stageLabel[h.from])} → ` : ""}
                      {t(stageLabel[h.to])}
                      {h.actor && ` · ${h.actor}`}
                      {h.comment && ` · ${h.comment}`}
                    </li>
                  ))}
                </ul>
              </details>
            </li>
          ))}
        </ul>
        {manage && (
          <div className="mt-3 flex gap-2">
            <select className={field} value={vacancyId} onChange={(e) => setVacancyId(e.target.value)}>
              <option value="">{t("Рассмотреть на вакансию…")}</option>
              {vacancies
                .filter((v) => (v.status === "Open" || v.status === "Paused") && !c.applications.some((a) => a.vacancyId === v.id))
                .map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.code} · {v.title}
                  </option>
                ))}
            </select>
            <button
              className={button}
              disabled={!!busy || !vacancyId}
              onClick={() =>
                act("application", async () => {
                  const card = await recruitmentApi.addApplication(c.id, vacancyId);
                  setVacancyId("");
                  return card;
                })
              }
            >
              <Plus className="size-4" />
              {t("Добавить")}
            </button>
          </div>
        )}
      </Card>

      <CandidateExtras ctx={ctx} card={c} />

      <CandidateDocuments ctx={ctx} card={c} onCard={setC} />
    </div>
  );
}

/** Язык переписки: какой закреплён и кем; что показало последнее сообщение, если другое (с уверенностью). */
function CandidateLanguage({ info }: { info: CandidateCard["languageInfo"] }) {
  const t = useT();
  if (!info?.code) return <span className="text-ink-3">{t("ещё не определён — кандидат не писал или писал коротко")}</span>;
  const other = info.lastDetected && info.lastDetected !== info.code;
  return (
    <span>
      {t(languageCodeLabel[info.code] ?? info.code)}
      {info.source && <span className="text-ink-3"> · {t(languageSourceLabel[info.source] ?? info.source)}</span>}
      {other && info.lastDetected && (
        <span className="block text-xs text-ink-3">
          {t("последнее сообщение — {lang}, уверенность {p}%: язык не сменился", {
            lang: t(languageCodeLabel[info.lastDetected] ?? info.lastDetected),
            p: Math.round((info.confidence ?? 0) * 100),
          })}
        </span>
      )}
    </span>
  );
}
