"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { ArrowRight, Check, ChevronRight, Loader2, Pencil, Plus, Search, Truck, X } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { UserAvatar } from "@/components/tasks/TaskKit";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { groupSum, parseSum, type DepartmentCard, type DepartmentPerson, type SupplySettings } from "@/lib/departments";
import {
  rawAuthorLabel,
  rawAuthors,
  rawRouteProblem,
  rawStepLabel,
  rawSteps,
  type RawAuthor,
  type RawStep,
  type WarehousesView,
} from "@/lib/procurement";
import type { QualitySettings } from "@/lib/quality";
import { QualitySettingsSection } from "./QualitySettingsSection";
import { WarehousesSection } from "./WarehousesSection";

const section = "rounded-xl border border-line bg-surface p-5";
const sectionTitle = "text-base font-semibold text-ink";
const small = "h-10 rounded-lg border border-line bg-surface px-3 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none disabled:opacity-60";

/**
 * Настройки одного отдела: руководители (право «Пользователи»); у снабжения — маршрут согласования заявок (финансист, директор, лимит
 * без директора), правила (допуск, эскалация), склады и кто решает по качеству партий — право «Настройки снабжения»; у продаж — ссылка на
 * их оргструктуру. Без права — только чтение.
 */
export function DepartmentSettings({
  department,
  users,
  canEditHeads,
  canEditSupply,
  supply,
  warehouses = null,
  quality = null,
}: {
  department: DepartmentCard;
  users: DepartmentPerson[];
  canEditHeads: boolean;
  canEditSupply: boolean;
  supply: SupplySettings | null;
  warehouses?: WarehousesView | null;
  quality?: QualitySettings | null;
}) {
  const t = useT();
  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4">
      <HeadsSection department={department} users={users} canEdit={canEditHeads} />
      {supply && <SupplyRoute settings={supply} users={users} canEdit={canEditSupply} />}
      {warehouses && <WarehousesSection view={warehouses} />}
      {quality && <QualitySettingsSection settings={quality} />}

      {department.code === "sales" && (
        <section className={section}>
          <h2 className={sectionTitle}>{t("Продажи: оргструктура и цели")}</h2>
          <p className="mt-1 text-sm text-ink-2">{t("Направления, СВР и дилеры регионов, справочник ТП и цели настраиваются в разделе продаж.")}</p>
          <Link href="/sales/setup" className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
            {t("Открыть настройки продаж")} <ArrowRight className="size-4" aria-hidden />
          </Link>
        </section>
      )}
      {!supply && department.code !== "sales" && <p className="px-1 text-sm text-ink-3">{t("Других настроек у отдела пока нет.")}</p>}
    </div>
  );
}

/** Руководители отдела: согласуют заявки сотрудников отдела и управляют задачами отдела (те же, что в «Задачах»). */
function HeadsSection({ department, users, canEdit }: { department: DepartmentCard; users: DepartmentPerson[]; canEdit: boolean }) {
  const t = useT();
  const router = useRouter();
  const [editing, setEditing] = useState(false);
  const [selected, setSelected] = useState<string[]>(department.heads.map((h) => h.id));
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const visible = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("ru");
    return users.filter((u) => !q || `${u.name} ${u.position ?? ""} ${u.department ?? ""}`.toLocaleLowerCase("ru").includes(q)).slice(0, 80);
  }, [users, query]);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await bff(`settings/departments/${department.code}/heads`, { method: "PUT", body: JSON.stringify({ userIds: selected }) });
      setEditing(false);
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className={section} aria-labelledby="dept-heads">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 id="dept-heads" className={sectionTitle}>
            {t("Руководители отдела")}
          </h2>
          <p className="mt-1 text-sm text-ink-2">
            {t("Руководитель первым согласует заявки на закупку сотрудников отдела и управляет задачами отдела. Если руководителей несколько, согласовать может любой.")}
          </p>
        </div>
        {canEdit && !editing && (
          <button type="button" className={button} onClick={() => (setSelected(department.heads.map((h) => h.id)), setEditing(true))}>
            <Pencil className="size-4" aria-hidden /> {t("Изменить")}
          </button>
        )}
      </div>

      {!editing && (
        <ul className="mt-4 flex flex-wrap gap-2">
          {department.heads.length === 0 && <li className="text-sm text-warn">{t("Руководитель не назначен — заявки сотрудников отдела сразу идут финансисту.")}</li>}
          {department.heads.map((h) => (
            <li key={h.id} className="inline-flex items-center gap-2 rounded-full border border-line bg-muted/50 py-1 pl-1 pr-3 text-sm">
              <UserAvatar name={h.name} size={26} />
              <span className="text-ink">{h.name}</span>
              {h.position && <span className="text-ink-3">· {h.position}</span>}
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <div className="mt-4">
          <label className="relative mb-2 block">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
            <input className={`${field} pl-9`} value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Найти сотрудника")} aria-label={t("Найти сотрудника")} />
          </label>
          <ul className="max-h-72 space-y-0.5 overflow-y-auto rounded-lg border border-line p-1.5">
            {visible.map((u) => (
              <li key={u.id}>
                <label className="flex cursor-pointer items-center gap-2.5 rounded-md px-2 py-1.5 hover:bg-muted">
                  <input
                    type="checkbox"
                    checked={selected.includes(u.id)}
                    onChange={(e) => setSelected((list) => (e.target.checked ? [...list, u.id] : list.filter((id) => id !== u.id)))}
                    className="size-4 accent-[var(--color-accent)]"
                  />
                  <span className="min-w-0 flex-1 text-sm">
                    <span className="text-ink">{u.name}</span>
                    {(u.position || u.department) && <span className="block truncate text-xs text-ink-3">{[u.position, u.department].filter(Boolean).join(" · ")}</span>}
                  </span>
                </label>
              </li>
            ))}
            {visible.length === 0 && <li className="px-2 py-3 text-sm text-ink-3">{t("Никого не нашлось.")}</li>}
          </ul>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <button type="button" className={primary} onClick={() => void save()} disabled={busy}>
              {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />}
              {t("Сохранить")}
            </button>
            <button type="button" className={button} onClick={() => setEditing(false)} disabled={busy}>
              {t("Отмена")}
            </button>
            <span className="text-sm text-ink-3">{t("Выбрано: {n}", { n: selected.length })}</span>
          </div>
          <div className="mt-2">
            <ErrorNote text={error} />
          </div>
        </div>
      )}
    </section>
  );
}

/**
 * Снабжение: маршрут согласования заявок — руководитель отдела сотрудника, финансист, директор (если сумма больше лимита), снабжение;
 * правила повторного согласования и эскалации. Сохраняется одним запросом (PUT /api/procurement/settings, все поля).
 */
function SupplyRoute({ settings, users, canEdit }: { settings: SupplySettings; users: DepartmentPerson[]; canEdit: boolean }) {
  const t = useT();
  const router = useRouter();
  const [finance, setFinance] = useState(settings.financeApproverId ?? "");
  const [director, setDirector] = useState(settings.directorApproverId ?? "");
  const [limit, setLimit] = useState(groupSum(settings.directorLimit));
  const [tolerance, setTolerance] = useState(String(settings.reapprovalTolerancePercent).replace(".", ","));
  const [headDays, setHeadDays] = useState(String(settings.escalateHeadDays));
  const [directorDays, setDirectorDays] = useState(String(settings.escalateDirectorDays));
  const [overPercent, setOverPercent] = useState(String(settings.overconsumptionPercent ?? 5).replace(".", ","));
  const [route, setRoute] = useState<Record<RawAuthor, RawStep[]>>(settings.rawSteps ?? { supply: ["analyst", "finance"], analyst: ["finance"], finance: ["analyst"] });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const touch = <T,>(set: (v: T) => void) => (v: T) => (set(v), setSaved(false));

  // Отключённый согласующий остаётся в списке, чтобы было видно, кто выбран, — сервер не даст сохранить его снова.
  const options = (id: string, name: string | null) => (id && !users.some((u) => u.id === id) ? [{ id, name: name ?? "—", position: null, department: null }, ...users] : users);

  const save = async () => {
    const limitValue = parseSum(limit);
    if (!limitValue) return setError(t("Лимит без директора — целая сумма больше нуля, до 1 трлн сум."));
    const tol = Number(tolerance.replace(",", ".").trim());
    if (!Number.isFinite(tol) || tol < 0 || tol > 100) return setError(t("Допуск — от 0 до 100 %, не больше двух знаков после запятой."));
    const head = Number(headDays.trim());
    const dir = Number(directorDays.trim());
    if (!Number.isInteger(head) || head < 1 || head > 30) return setError(t("Руководителю снабжения — через 1–30 дней просрочки."));
    if (!Number.isInteger(dir) || dir < head || dir > 60) return setError(t("Директору — не раньше, чем руководителю снабжения, и не позже 60 дней просрочки."));
    const over = Number(overPercent.replace(",", ".").trim());
    if (!Number.isFinite(over) || over < 0 || over > 100) return setError(t("Порог перерасхода сырья — от 0 до 100 %, не больше двух знаков после запятой."));
    const routeProblem = rawRouteProblem(route);
    if (routeProblem) return setError(t(routeProblem));
    setBusy(true);
    setError(null);
    try {
      await bff("procurement/settings", {
        method: "PUT",
        body: JSON.stringify({
          reapprovalTolerancePercent: tol,
          escalateHeadDays: head,
          escalateDirectorDays: dir,
          financeApproverId: finance || null,
          directorApproverId: director || null,
          directorLimit: limitValue,
          // Прежний маршрут сырья (флажок и руководитель снабжения) не присылается — сервер оставляет его как был.
          rawRouteEnabled: null,
          supplyApproverId: null,
          overconsumptionPercent: over,
          rawSteps: route,
        }),
      });
      setSaved(true);
      setLimit(groupSum(limitValue));
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const person = (value: string, onChange: (v: string) => void, list: DepartmentPerson[], label: string) => (
    <select className={`${field} mt-2 max-w-md`} value={value} onChange={(e) => onChange(e.target.value)} disabled={!canEdit} aria-label={label}>
      <option value="">{t("Не выбран")}</option>
      {list.map((u) => (
        <option key={u.id} value={u.id}>
          {[u.name, u.position].filter(Boolean).join(" · ")}
        </option>
      ))}
    </select>
  );

  return (
    <>
      <section className={section} aria-labelledby="supply-route">
        <h2 id="supply-route" className={sectionTitle}>
          {t("Маршрут обычных закупок")}
        </h2>
        <p className="mt-1 text-sm text-ink-2">{t("Кто согласует заявку, прежде чем она попадёт в снабжение. Цена в заявке обязательна — по сумме решается, нужен ли директор.")}</p>

        <ol className="mt-5 flex flex-col">
          <Step n={1} title={t("Руководитель отдела")}>
            <p className="text-sm text-ink-2">{t("Заявку сотрудника сначала согласует руководитель его отдела. Заявку самого руководителя — сразу финансист.")}</p>
            <p className="mt-1 text-xs text-ink-3">{t("Руководители назначаются на странице каждого отдела.")}</p>
          </Step>
          <Step n={2} title={t("Финансист")}>
            {person(finance, touch(setFinance), options(finance, settings.financeApproverName), t("Финансист"))}
            {!finance && <p className="mt-1.5 text-xs text-warn">{t("Не выбран: заявки после руководителя будут ждать финансиста.")}</p>}
          </Step>
          <Step n={3} title={t("Директор — если сумма больше лимита")}>
            {person(director, touch(setDirector), options(director, settings.directorApproverName), t("Директор"))}
            <label className="mt-3 flex flex-wrap items-center gap-2 text-sm text-ink">
              {t("Лимит покупки без директора")}
              <input
                value={limit}
                onChange={(e) => touch(setLimit)(e.target.value)}
                onBlur={() => parseSum(limit) && setLimit(groupSum(parseSum(limit)!))}
                inputMode="numeric"
                disabled={!canEdit}
                className={`${small} w-40`}
                aria-label={t("Лимит покупки без директора, сум")}
              />
              {t("сум")}
            </label>
            <p className="mt-1.5 text-xs text-ink-3">{t("Заявка на сумму до лимита включительно после финансиста идёт сразу в снабжение.")}</p>
            {!director && <p className="mt-1 text-xs text-warn">{t("Директор не выбран: крупные заявки будут ждать директора.")}</p>}
          </Step>
          <Step n={4} title={t("Снабжение")} last>
            <p className="flex items-center gap-2 text-sm text-ink-2">
              <Truck className="size-4 shrink-0 text-ink-3" aria-hidden />
              {t("Снабженцы покупают по согласованной заявке, купленное приходуется на склад и выдаётся сотруднику.")}
            </p>
          </Step>
        </ol>
      </section>

      <section className={section} aria-labelledby="supply-raw">
        <h2 id="supply-raw" className={sectionTitle}>
          {t("Маршрут закупки сырья")}
        </h2>
        <p className="mt-1 text-sm text-ink-2">
          {t(
            "Заявки «Закупка сырья» создают снабженцы, аналитики и финансист — в них только позиции категорий сырья («Заявки → Настройки»). Кто согласует — зависит от автора; после последнего шага заявка уходит в снабжение.",
          )}
        </p>
        <RawRouteTable route={route} onChange={touch(setRoute)} canEdit={canEdit} />
        <p className="mt-3 text-xs text-ink-3">
          {t(
            "Аналитики — пользователи с ролью «Аналитик» («Настройки → Пользователи»); финансист, директор и лимит — из маршрута выше. Свою заявку автор не согласует. Новый маршрут действует для заявок, отправленных после сохранения.",
          )}
        </p>
      </section>

      <section className={section} aria-labelledby="supply-rules">
        <h2 id="supply-rules" className={sectionTitle}>
          {t("Правила снабжения")}
        </h2>
        <h3 className="mt-3 text-sm font-semibold text-ink">{t("Повторное согласование")}</h3>
        <p className="mt-1 text-sm text-ink-2">
          {t("Утверждённую заявку снабжение может уточнить. Новые и удалённые позиции, рост количества к закупке — всегда на согласование снова; рост цены или суммы — если больше допуска.")}
        </p>
        <label className="mt-2 inline-flex items-center gap-2 text-sm text-ink">
          {t("Допуск роста цены и суммы")}
          <input value={tolerance} onChange={(e) => touch(setTolerance)(e.target.value)} inputMode="decimal" disabled={!canEdit} className={`${small} w-20`} />%
        </label>
        <h3 className="mt-5 text-sm font-semibold text-ink">{t("Эскалация просроченных задач снабжения")}</h3>
        <p className="mt-1 text-sm text-ink-2">{t("Исполнителю о сроке напоминают «Задачи». Если задача снабжения просрочена дольше, о ней узнаёт руководитель снабжения, затем директор.")}</p>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2">
          <label className="inline-flex items-center gap-2 text-sm text-ink">
            {t("Руководителю снабжения через")}
            <input value={headDays} onChange={(e) => touch(setHeadDays)(e.target.value)} inputMode="numeric" disabled={!canEdit} className={`${small} w-16`} />
            {t("дн.")}
          </label>
          <label className="inline-flex items-center gap-2 text-sm text-ink">
            {t("Директору через")}
            <input value={directorDays} onChange={(e) => touch(setDirectorDays)(e.target.value)} inputMode="numeric" disabled={!canEdit} className={`${small} w-16`} />
            {t("дн.")}
          </label>
        </div>
        <h3 className="mt-5 text-sm font-semibold text-ink">{t("Перерасход сырья в производстве")}</h3>
        <p className="mt-1 text-sm text-ink-2">{t("При закрытии задания на выпуск расход сверяется с нормой на фактический выход. Строка с перерасходом больше порога помечается, руководитель производства получает уведомление.")}</p>
        <label className="mt-2 inline-flex items-center gap-2 text-sm text-ink">
          {t("Порог перерасхода")}
          <input value={overPercent} onChange={(e) => touch(setOverPercent)(e.target.value)} inputMode="decimal" disabled={!canEdit} className={`${small} w-20`} />%
        </label>
      </section>

      {canEdit ? (
        <div className="flex flex-wrap items-center gap-3">
          <button type="button" onClick={() => void save()} disabled={busy} className={`${primary} h-10`}>
            {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />}
            {t("Сохранить настройки снабжения")}
          </button>
          {saved && <span className="text-sm text-ok">{t("Сохранено")}</span>}
          <div className="basis-full">
            <ErrorNote text={error} />
          </div>
        </div>
      ) : (
        <p className="px-1 text-sm text-ink-3">{t("Менять маршрут и правила может тот, у кого есть право «Настройки снабжения».")}</p>
      )}
    </>
  );
}

/** Шаг маршрута: номер в кружке, линия к следующему шагу, заголовок и содержимое. */
/**
 * Маршрут сырья по авторам: строка на автора — шаги по порядку (убрать — крестик, добавить — список). Свой шаг автору не предлагается;
 * директор — один раз (всегда или если сумма больше лимита). Порядок — как добавлены: чтобы поменять, уберите шаг и добавьте снова.
 */
function RawRouteTable({ route, onChange, canEdit }: { route: Record<RawAuthor, RawStep[]>; onChange: (route: Record<RawAuthor, RawStep[]>) => void; canEdit: boolean }) {
  const t = useT();
  const own: Record<RawAuthor, RawStep | null> = { supply: null, analyst: "analyst", finance: "finance" };
  const set = (author: RawAuthor, steps: RawStep[]) => onChange({ ...route, [author]: steps });
  return (
    <ul className="mt-4 divide-y divide-line rounded-xl border border-line">
      {rawAuthors.map((author) => {
        const steps = route[author] ?? [];
        const director = steps.includes("director") || steps.includes("director_limit");
        const free = rawSteps.filter((s) => !steps.includes(s) && s !== own[author] && !(director && (s === "director" || s === "director_limit")));
        return (
          <li key={author} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-44 shrink-0 text-sm font-medium text-ink">{t(rawAuthorLabel[author])}</span>
            <ol className="flex min-w-0 flex-wrap items-center gap-1.5" aria-label={t("Шаги: {who}", { who: t(rawAuthorLabel[author]) })}>
              {steps.map((step, i) => (
                <li key={step} className="flex items-center gap-1.5">
                  {i > 0 && <ChevronRight className="size-3.5 text-ink-3" aria-hidden />}
                  <span className="inline-flex h-8 items-center gap-1 rounded-lg border border-line bg-muted pl-2.5 pr-1 text-sm text-ink">
                    {t(rawStepLabel[step])}
                    {canEdit && (
                      <button
                        type="button"
                        onClick={() => set(author, steps.filter((s) => s !== step))}
                        aria-label={t("Убрать шаг «{step}»", { step: t(rawStepLabel[step]) })}
                        className="grid size-6 place-items-center rounded-md text-ink-3 hover:text-bad"
                      >
                        <X className="size-3.5" aria-hidden />
                      </button>
                    )}
                  </span>
                </li>
              ))}
              {canEdit && free.length > 0 && (
                <li className="flex items-center gap-1.5">
                  {steps.length > 0 && <ChevronRight className="size-3.5 text-ink-3" aria-hidden />}
                  <label className="relative inline-flex h-8 items-center gap-1 rounded-lg border border-dashed border-line-strong px-2.5 text-sm text-ink-2 hover:text-ink">
                    <Plus className="size-3.5" aria-hidden />
                    {t("Шаг")}
                    <select
                      value=""
                      onChange={(e) => e.target.value && set(author, [...steps, e.target.value as RawStep])}
                      className="absolute inset-0 cursor-pointer opacity-0"
                      aria-label={t("Добавить шаг: {who}", { who: t(rawAuthorLabel[author]) })}
                    >
                      <option value="" />
                      {free.map((s) => (
                        <option key={s} value={s}>
                          {t(rawStepLabel[s])}
                        </option>
                      ))}
                    </select>
                  </label>
                </li>
              )}
              <li className="flex items-center gap-1.5 text-sm text-ink-3">
                <ChevronRight className="size-3.5" aria-hidden />
                {t("Снабжение")}
              </li>
            </ol>
            {steps.length === 0 && <span className="text-xs text-warn">{t("Нужен хотя бы один шаг")}</span>}
          </li>
        );
      })}
    </ul>
  );
}

function Step({ n, title, last = false, children }: { n: number; title: string; last?: boolean; children: React.ReactNode }) {
  return (
    <li className="relative flex gap-4 pb-6 last:pb-0">
      {!last && <span aria-hidden className="absolute left-[15px] top-8 h-[calc(100%-2rem)] w-px bg-line-strong" />}
      <span className="z-10 grid size-8 shrink-0 place-items-center rounded-full border border-accent/40 bg-accent-soft text-sm font-semibold text-accent-strong">{n}</span>
      <div className="min-w-0 flex-1 pt-1">
        <h3 className="text-sm font-semibold text-ink">{title}</h3>
        <div className="mt-1">{children}</div>
      </div>
    </li>
  );
}
