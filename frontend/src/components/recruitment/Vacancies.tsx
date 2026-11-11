"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Loader2, Plus } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { VacancyExtras } from "@/components/recruitment/Flow";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import {
  employmentLabel,
  recruitmentApi,
  salaryText,
  STAGES,
  stageLabel,
  stageTone,
  vacancyStatusLabel,
  when,
  type RecruitmentContext,
  type Vacancy,
  type VacancyInput,
  type VacancyStatus,
} from "@/lib/recruitment";

const statusTone: Record<VacancyStatus, string> = {
  Draft: "bg-muted text-ink-2",
  Open: "bg-ok-soft text-ok",
  Paused: "bg-warn-soft text-warn",
  Closed: "bg-muted text-ink-3",
};

export function StatusBadge({ status }: { status: VacancyStatus }) {
  const t = useT();
  return <Badge className={statusTone[status]}>{t(vacancyStatusLabel[status])}</Badge>;
}

export const emptyInput = (ctx: RecruitmentContext): VacancyInput => ({
  title: "",
  departmentId: ctx.departments[0]?.id ?? "",
  description: null,
  requirements: null,
  conditions: null,
  salaryFrom: null,
  salaryTo: null,
  city: null,
  employmentType: "full",
  headcount: 1,
  recruiterId: null,
});

const num = (v: string) => (v.trim() === "" ? null : Number(v.replace(/\s/g, "")));

/** Форма вакансии: должность, отдел, требования, условия, вилка, город, занятость, сколько человек, рекрутер. */
export function VacancyForm({ ctx, initial, onSave, onCancel }: { ctx: RecruitmentContext; initial: VacancyInput; onSave: (v: VacancyInput) => Promise<void>; onCancel: () => void }) {
  const t = useT();
  const [v, setV] = useState(initial);
  const { busy, error, run } = useAction();
  const set = (patch: Partial<VacancyInput>) => setV((x) => ({ ...x, ...patch }));
  return (
    <form
      className="grid gap-3 sm:grid-cols-2"
      onSubmit={(e) => {
        e.preventDefault();
        run("save", () => onSave(v));
      }}
    >
      <Label label="Должность" className="sm:col-span-2">
        <input className={field} value={v.title} onChange={(e) => set({ title: e.target.value })} maxLength={200} required />
      </Label>
      <Label label="Отдел">
        <select className={field} value={v.departmentId} onChange={(e) => set({ departmentId: e.target.value })}>
          {ctx.departments.map((d) => (
            <option key={d.id} value={d.id}>
              {t(d.name)}
            </option>
          ))}
        </select>
      </Label>
      <Label label="Рекрутер">
        <select className={field} value={v.recruiterId ?? ""} onChange={(e) => set({ recruiterId: e.target.value || null })}>
          <option value="">{t("Не назначен")}</option>
          {ctx.recruiters.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </Label>
      <Label label="Зарплата от, сум">
        <input className={field} inputMode="numeric" value={v.salaryFrom ?? ""} onChange={(e) => set({ salaryFrom: num(e.target.value) })} />
      </Label>
      <Label label="Зарплата до, сум">
        <input className={field} inputMode="numeric" value={v.salaryTo ?? ""} onChange={(e) => set({ salaryTo: num(e.target.value) })} />
      </Label>
      <Label label="Город">
        <input className={field} value={v.city ?? ""} onChange={(e) => set({ city: e.target.value || null })} maxLength={100} />
      </Label>
      <div className="grid grid-cols-2 gap-3">
        <Label label="Занятость">
          <select className={field} value={v.employmentType ?? ""} onChange={(e) => set({ employmentType: e.target.value || null })}>
            {Object.entries(employmentLabel).map(([k, label]) => (
              <option key={k} value={k}>
                {t(label)}
              </option>
            ))}
          </select>
        </Label>
        <Label label="Человек">
          <input className={field} type="number" min={1} max={100} value={v.headcount} onChange={(e) => set({ headcount: Number(e.target.value) || 1 })} />
        </Label>
      </div>
      <Label label="Описание" className="sm:col-span-2">
        <textarea className={textarea} value={v.description ?? ""} onChange={(e) => set({ description: e.target.value || null })} maxLength={8000} />
      </Label>
      <Label label="Требования" className="sm:col-span-2">
        <textarea className={textarea} value={v.requirements ?? ""} onChange={(e) => set({ requirements: e.target.value || null })} maxLength={4000} />
      </Label>
      <Label label="Условия работы" hint="График, место, оформление, что даёт компания." className="sm:col-span-2">
        <textarea className={textarea} value={v.conditions ?? ""} onChange={(e) => set({ conditions: e.target.value || null })} maxLength={4000} />
      </Label>
      <div className="flex gap-2 sm:col-span-2">
        <button type="submit" className={primary} disabled={!!busy || !v.title.trim() || !v.departmentId}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
        <button type="button" className={button} onClick={onCancel} disabled={!!busy}>
          {t("Отмена")}
        </button>
      </div>
      <div className="sm:col-span-2">
        <ErrorText error={error} />
      </div>
    </form>
  );
}

/** Вакансия: данные (правка), статус, воронка кандидатов по этапам. */
export function VacancyDetail({ ctx, id, initialEdit = false }: { ctx: RecruitmentContext; id: string; initialEdit?: boolean }) {
  const t = useT();
  const [v, setV] = useState<Vacancy | null>(null);
  const [editing, setEditing] = useState(initialEdit);
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => setV(await recruitmentApi.vacancy(id)));
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!v) return error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;

  const setStatus = (status: VacancyStatus) => {
    const active = STAGES.filter((s) => !["Hired", "Rejected", "Withdrawn"].includes(s)).reduce((n, s) => n + (v.stages[s] ?? 0), 0);
    if (status === "Closed" && active > 0 && !confirm(t("Закрыть вакансию? Кандидаты в работе ({n}) получат отказ «вакансия закрыта».", { n: active }))) return;
    run(status, async () => setV(await recruitmentApi.vacancyStatus(v.id, status)));
  };

  return (
    <div className="space-y-4">
      <Card
        title={`${v.code} · ${v.title}`}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={v.status} />
            {ctx.canManage && v.status !== "Open" && v.status !== "Closed" && (
              <button className={primary} disabled={!!busy} onClick={() => setStatus("Open")}>
                {t("Открыть")}
              </button>
            )}
            {ctx.canManage && v.status === "Open" && (
              <button className={button} disabled={!!busy} onClick={() => setStatus("Paused")}>
                {t("Пауза")}
              </button>
            )}
            {ctx.canManage && v.status === "Closed" && (
              <button className={button} disabled={!!busy} onClick={() => setStatus("Open")}>
                {t("Открыть снова")}
              </button>
            )}
            {ctx.canManage && v.status !== "Closed" && (
              <button className={button} disabled={!!busy} onClick={() => setStatus("Closed")}>
                {t("Закрыть")}
              </button>
            )}
            {ctx.canManage && !editing && (
              <button className={button} onClick={() => setEditing(true)}>
                {t("Изменить")}
              </button>
            )}
          </div>
        }
      >
        {editing ? (
          <VacancyForm
            ctx={ctx}
            initial={v}
            onCancel={() => setEditing(false)}
            onSave={async (input) => {
              setV(await recruitmentApi.updateVacancy(v.id, input));
              setEditing(false);
            }}
          />
        ) : (
          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
            <dt className="text-ink-3">{t("Отдел")}</dt>
            <dd>{t(v.department)}</dd>
            <dt className="text-ink-3">{t("Зарплата")}</dt>
            <dd>{salaryText(v.salaryFrom, v.salaryTo)}</dd>
            <dt className="text-ink-3">{t("Город, занятость")}</dt>
            <dd>
              {v.city ?? "—"}
              {v.employmentType && `, ${t(employmentLabel[v.employmentType] ?? v.employmentType)}`}
            </dd>
            <dt className="text-ink-3">{t("Нужно человек")}</dt>
            <dd>{v.headcount}</dd>
            <dt className="text-ink-3">{t("Рекрутер")}</dt>
            <dd>{v.recruiter ?? "—"}</dd>
            {v.hiringRequestNumber && (
              <>
                <dt className="text-ink-3">{t("По заявке")}</dt>
                <dd>№ {v.hiringRequestNumber}</dd>
              </>
            )}
            <dt className="text-ink-3">{t("Открыта")}</dt>
            <dd>{when(v.openedAt)}</dd>
            {[
              ["Описание", v.description],
              ["Требования", v.requirements],
              ["Условия работы", v.conditions],
            ].map(([label, value]) =>
              value ? (
                <div key={label} className="contents">
                  <dt className="text-ink-3">{t(label as string)}</dt>
                  <dd className="whitespace-pre-line">{value}</dd>
                </div>
              ) : null,
            )}
          </dl>
        )}
        <ErrorText error={error} />
      </Card>

      <VacancyExtras ctx={ctx} vacancy={v} onChange={setV} />

      <Card
        title="Кандидаты"
        actions={
          ctx.canManage && (v.status === "Open" || v.status === "Paused") ? (
            <Link className={button} href={`/hr/recruitment/candidates?new=1&vacancyId=${v.id}`}>
              <Plus className="size-4" />
              {t("Добавить кандидата")}
            </Link>
          ) : null
        }
      >
        <div className="mb-3 flex flex-wrap gap-2">
          {STAGES.map((s) => (
            <Badge key={s} className={stageTone[s]}>
              {t(stageLabel[s])}: {v.stages[s] ?? 0}
            </Badge>
          ))}
        </div>
        {v.applications.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Кандидатов пока нет.")}</p>
        ) : (
          <ul className="divide-y divide-line">
            {v.applications.map((a) => (
              <li key={a.applicationId} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <div>
                  <Link href={`/hr/recruitment/candidates/${a.candidateId}`} className="font-medium text-ink hover:text-accent-strong">
                    {a.candidateName}
                  </Link>
                  <div className="text-xs text-ink-3">
                    {a.phone ?? "—"} · {when(a.stageChangedAt)}
                  </div>
                </div>
                <Badge className={stageTone[a.stage]}>{t(stageLabel[a.stage])}</Badge>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
