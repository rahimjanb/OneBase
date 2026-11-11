"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Loader2, Plus } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { day, recruitmentApi, requestStatusLabel, salaryText, when, type HiringRequest, type HiringRequestInput, type RecruitmentContext, type RecruitmentSettings } from "@/lib/recruitment";

const tone: Record<HiringRequest["status"], string> = {
  Submitted: "bg-warn-soft text-warn",
  Approved: "bg-ok-soft text-ok",
  Rejected: "bg-bad-soft text-bad",
  Cancelled: "bg-muted text-ink-3",
};

const num = (v: string) => (v.trim() === "" ? null : Number(v.replace(/\s/g, "")));

/** Заявки на сотрудника: руководитель подаёт за свой отдел и видит решение; HR одобряет (открывается вакансия) или отклоняет с причиной. */
export function RequestsView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [rows, setRows] = useState<HiringRequest[] | null>(null);
  const [creating, setCreating] = useState(false);
  const [input, setInput] = useState<HiringRequestInput>({
    departmentId: ctx.requestDepartments[0]?.id ?? "",
    position: "",
    headcount: 1,
    reason: null,
    requirements: null,
    salaryFrom: null,
    salaryTo: null,
    desiredStart: null,
  });
  const [decision, setDecision] = useState<{ id: string; approve: boolean; comment: string } | null>(null);
  const { busy, error, run } = useAction();
  const load = () => run("load", async () => setRows(await recruitmentApi.requests()));
  useEffect(() => {
    load();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  const replace = (r: HiringRequest) => setRows((list) => (list ?? []).map((x) => (x.id === r.id ? r : x)));

  return (
    <div className="space-y-4">
      {ctx.canRequest && (
        <div className="flex justify-end">
          <button className={primary} onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            {t("Новая заявка")}
          </button>
        </div>
      )}
      {creating && (
        <Card title="Заявка на сотрудника">
          <form
            className="grid gap-3 sm:grid-cols-2"
            onSubmit={(e) => {
              e.preventDefault();
              run("create", async () => {
                const created = await recruitmentApi.createRequest(input);
                setRows((list) => [created, ...(list ?? [])]);
                setCreating(false);
              });
            }}
          >
            <Label label="Отдел">
              <select className={field} value={input.departmentId} onChange={(e) => setInput({ ...input, departmentId: e.target.value })}>
                {ctx.requestDepartments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {t(d.name)}
                  </option>
                ))}
              </select>
            </Label>
            <Label label="Должность">
              <input className={field} value={input.position} onChange={(e) => setInput({ ...input, position: e.target.value })} maxLength={200} required />
            </Label>
            <Label label="Сколько человек">
              <input className={field} type="number" min={1} max={100} value={input.headcount} onChange={(e) => setInput({ ...input, headcount: Number(e.target.value) || 1 })} />
            </Label>
            <Label label="Когда нужен">
              <input className={field} type="date" value={input.desiredStart ?? ""} onChange={(e) => setInput({ ...input, desiredStart: e.target.value || null })} />
            </Label>
            <Label label="Зарплата от, сум">
              <input className={field} inputMode="numeric" value={input.salaryFrom ?? ""} onChange={(e) => setInput({ ...input, salaryFrom: num(e.target.value) })} />
            </Label>
            <Label label="Зарплата до, сум">
              <input className={field} inputMode="numeric" value={input.salaryTo ?? ""} onChange={(e) => setInput({ ...input, salaryTo: num(e.target.value) })} />
            </Label>
            <Label label="Причина" hint="Новая ставка, замена уволившегося, рост объёма…" className="sm:col-span-2">
              <input className={field} value={input.reason ?? ""} onChange={(e) => setInput({ ...input, reason: e.target.value || null })} maxLength={1000} />
            </Label>
            <Label label="Требования к кандидату" className="sm:col-span-2">
              <textarea className={textarea} value={input.requirements ?? ""} onChange={(e) => setInput({ ...input, requirements: e.target.value || null })} maxLength={4000} />
            </Label>
            <div className="flex gap-2 sm:col-span-2">
              <button type="submit" className={primary} disabled={!!busy || !input.position.trim() || !input.departmentId}>
                {busy === "create" && <Loader2 className="size-4 animate-spin" />}
                {t("Отправить в HR")}
              </button>
              <button type="button" className={button} onClick={() => setCreating(false)}>
                {t("Отмена")}
              </button>
            </div>
          </form>
        </Card>
      )}

      <ErrorText error={error} />
      {rows === null ? (
        <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
      ) : rows.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line p-6 text-center text-sm text-ink-3">{t("Заявок пока нет.")}</p>
      ) : (
        <ul className="space-y-3">
          {rows.map((r) => (
            <li key={r.id} className="rounded-xl border border-line bg-surface p-4 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <span className="font-semibold text-ink">
                    № {r.number} · {r.position}
                  </span>{" "}
                  <span className="text-ink-2">
                    — {t(r.department)}, {r.headcount} {t("чел.")}
                  </span>
                </div>
                <Badge className={tone[r.status]}>{t(requestStatusLabel[r.status])}</Badge>
              </div>
              <div className="mt-1 text-xs text-ink-3">
                {r.createdBy ?? "—"} · {when(r.createdAt)} · {t("зарплата")}: {salaryText(r.salaryFrom, r.salaryTo)}
                {r.desiredStart && ` · ${t("нужен к")} ${day(r.desiredStart)}`}
              </div>
              {r.reason && <p className="mt-1 text-ink-2">{r.reason}</p>}
              {r.requirements && <p className="mt-1 whitespace-pre-line text-ink-2">{r.requirements}</p>}
              {r.decidedAt && (
                <p className="mt-1 text-xs text-ink-2">
                  {t("Решение")}: {r.decidedBy ?? "—"}, {when(r.decidedAt)}
                  {r.decisionComment && ` — ${r.decisionComment}`}
                  {r.vacancyId && ctx.canRead && (
                    <>
                      {" · "}
                      <Link href={`/hr/recruitment/vacancies/${r.vacancyId}`} className="text-accent-strong hover:underline">
                        {t("вакансия")} {r.vacancyCode}
                      </Link>
                    </>
                  )}
                </p>
              )}
              {(r.canDecide || r.canCancel) && (
                <div className="mt-2 flex flex-wrap gap-2">
                  {r.canDecide && (
                    <>
                      <button className={primary} disabled={!!busy} onClick={() => setDecision({ id: r.id, approve: true, comment: "" })}>
                        {t("Одобрить")}
                      </button>
                      <button className={button} disabled={!!busy} onClick={() => setDecision({ id: r.id, approve: false, comment: "" })}>
                        {t("Отклонить")}
                      </button>
                    </>
                  )}
                  {r.canCancel && (
                    <button className={danger} disabled={!!busy} onClick={() => run("cancel", async () => replace(await recruitmentApi.cancelRequest(r.id)))}>
                      {t("Отозвать")}
                    </button>
                  )}
                </div>
              )}
              {decision?.id === r.id && (
                <div className="mt-2 flex flex-wrap gap-2">
                  <input
                    className={field}
                    placeholder={decision.approve ? t("Комментарий (необязательно)") : t("Причина отказа (обязательно)")}
                    value={decision.comment}
                    onChange={(e) => setDecision({ ...decision, comment: e.target.value })}
                  />
                  <button
                    className={primary}
                    disabled={!!busy || (!decision.approve && !decision.comment.trim())}
                    onClick={() =>
                      run("decide", async () => {
                        replace(
                          decision.approve
                            ? await recruitmentApi.approveRequest(r.id, decision.comment || null)
                            : await recruitmentApi.rejectRequest(r.id, decision.comment),
                        );
                        setDecision(null);
                      })
                    }
                  >
                    {decision.approve ? t("Одобрить и открыть вакансию") : t("Отклонить")}
                  </button>
                  <button className={button} onClick={() => setDecision(null)}>
                    {t("Отмена")}
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Настройки подбора: срок хранения данных после завершения подбора и текст согласия (новый текст — новая версия). */
export function RecruitmentSettingsView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [s, setS] = useState<RecruitmentSettings | null>(null);
  const [days, setDays] = useState(7);
  const [text, setText] = useState("");
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => {
      const settings = await recruitmentApi.settings();
      setS(settings);
      setDays(settings.retentionDays);
      setText(settings.consentText);
    });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  if (!s) return error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;
  return (
    <Card title="Персональные данные кандидатов">
      <div className="grid max-w-2xl gap-3">
        <Label
          label="Срок хранения, дней"
          hint="Считается с момента, когда по кандидату закрыты все отклики (принят, отказ, отказался) и нет новой активности. Пока идёт подбор, данные не удаляются. Затем удаляются целиком: контакты, резюме, переписка, отклики."
        >
          <input className={`${field} w-32`} type="number" min={1} max={365} value={days} disabled={!ctx.canManage} onChange={(e) => setDays(Number(e.target.value))} />
        </Label>
        <Label label={t("Текст согласия (версия {n})", { n: s.consentVersion })} hint="Для согласий, которые HR записывает вручную (письменно, устно…); AI-рекрутер этот текст кандидатам не отправляет. Изменение — новая версия; у данных согласий остаётся их версия.">
          <textarea className={`${textarea} min-h-32`} value={text} disabled={!ctx.canManage} onChange={(e) => setText(e.target.value)} maxLength={8000} />
        </Label>
        {ctx.canManage && (
          <div className="flex items-center gap-3">
            <button
              className={primary}
              disabled={!!busy || !text.trim()}
              onClick={() =>
                run("save", async () => {
                  setS(await recruitmentApi.saveSettings(days, text));
                  setSaved(true);
                })
              }
            >
              {t("Сохранить")}
            </button>
            {saved && <span className="text-sm text-ok">{t("Сохранено.")}</span>}
          </div>
        )}
        <ErrorText error={error} />
      </div>
    </Card>
  );
}
