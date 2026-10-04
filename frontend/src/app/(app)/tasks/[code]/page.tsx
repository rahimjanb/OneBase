import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { ChevronLeft, ChevronRight, ExternalLink } from "lucide-react";
import { DateSwitch, ParamSelect, SearchBox } from "@/components/field/DateSwitch";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { FieldTeamTasks, type TeamMember } from "@/components/tasks/FieldTeamTasks";
import { WorkTaskBoard } from "@/components/tasks/WorkTaskBoard";
import { apiGet, apiTry } from "@/lib/server-api";
import type { FieldRouteSummary, FieldTask } from "@/lib/field/types";
import { routeSourceLabel } from "@/lib/field/labels";
import type { DepartmentTasks, PageResult, WorkAssignee, WorkTask } from "@/lib/tasks";

type SearchParams = Record<string, string | string[] | undefined>;

const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Сегодня по времени компании (Ташкент). */
const todayInTashkent = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Tashkent" }).format(new Date());

const qs = (params: Record<string, string | number | undefined | null | false>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== false && v !== "") p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : "";
};

export async function generateMetadata({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const departments = await apiTry<DepartmentTasks[]>("/api/work/departments");
  const name = departments?.find((d) => d.code === code)?.name;
  return { title: name ? `${name} · Задачи · OneBase` : "Задачи · OneBase" };
}

/** Отдел в разделе «Задачи»: задачи отдела, а в «Продажах» — ещё задачи и маршруты полевой команды (Sales Base). */
export default async function DepartmentTasksPage({ params, searchParams }: { params: Promise<{ code: string }>; searchParams: Promise<SearchParams> }) {
  const { code } = await params;
  const sp = await searchParams;
  const returnTo = `/tasks/${code}`;
  const departments = await apiGet<DepartmentTasks[]>("/api/work/departments", returnTo);
  const department = departments.find((d) => d.code === code);
  if (!department) notFound();
  if (!department.canOpen) redirect(`/no-access?from=${encodeURIComponent(returnTo)}`);

  const tabs = [
    ...(department.hasField
      ? [
          { key: "team", label: "Агенты и супервайзеры" },
          { key: "routes", label: "Маршруты" },
        ]
      : []),
    ...(department.hasTasks ? [{ key: "office", label: department.hasField ? "Задачи отдела" : "Задачи" }] : []),
  ];
  const requested = one(sp, "tab");
  const tab = tabs.find((t) => t.key === requested)?.key ?? tabs[0]?.key;
  const today = todayInTashkent();
  const page = Math.max(1, Number(one(sp, "page") ?? 1) || 1);

  return (
    <>
      <PageHeader
        title={department.name}
        subtitle={department.hasField ? "Задачи и маршруты полевой команды, задачи отдела" : "Задачи отдела"}
        back="/tasks"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Задачи", href: "/tasks" }, { label: department.name }]}
      />
      <PageBody>
        <div className="space-y-4">
          {tabs.length > 1 && (
            <nav aria-label="Разделы отдела" className="flex gap-1 overflow-x-auto border-b border-line">
              {tabs.map((t) => (
                <Link
                  key={t.key}
                  href={`${returnTo}${qs({ tab: t.key })}`}
                  aria-current={t.key === tab ? "page" : undefined}
                  className={`-mb-px shrink-0 border-b-2 px-3 py-2.5 text-sm font-medium ${t.key === tab ? "border-accent text-accent-strong" : "border-transparent text-ink-2 hover:text-ink"}`}
                >
                  {t.label}
                </Link>
              ))}
            </nav>
          )}
          {tab === "team" && <TeamTab sp={sp} page={page} today={today} returnTo={returnTo} />}
          {tab === "routes" && <RoutesTab sp={sp} today={today} />}
          {tab === "office" && <OfficeTab code={code} sp={sp} page={page} today={today} returnTo={returnTo} />}
        </div>
      </PageBody>
    </>
  );
}

function StatusTabs({ current, items }: { current: string; items: { key: string; label: string; href: string }[] }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {items.map((i) => (
        <Link
          key={i.key}
          href={i.href}
          aria-current={i.key === current ? "page" : undefined}
          className={`inline-flex h-9 items-center rounded-full border px-3 text-sm ${i.key === current ? "border-accent bg-accent-soft font-semibold text-accent-strong" : "border-line bg-surface text-ink-2 hover:bg-muted"}`}
        >
          {i.label}
        </Link>
      ))}
    </div>
  );
}

function Pager({ page, pageSize, total, href }: { page: number; pageSize: number; total: number; href: (p: number) => string }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <div className="flex items-center justify-center gap-3 pt-2 text-sm text-ink-2">
      {page > 1 ? (
        <Link href={href(page - 1)} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
          <ChevronLeft className="size-4" /> Назад
        </Link>
      ) : (
        <span className="w-[84px]" />
      )}
      <span>
        {page} из {pages}
      </span>
      {page < pages ? (
        <Link href={href(page + 1)} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
          Дальше <ChevronRight className="size-4" />
        </Link>
      ) : (
        <span className="w-[84px]" />
      )}
    </div>
  );
}

/** Задачи агентов и супервайзеров (Sales Base) — данные и права решает backend по области пользователя. */
async function TeamTab({ sp, page, today, returnTo }: { sp: SearchParams; page: number; today: string; returnTo: string }) {
  const view = one(sp, "view") ?? "open";
  const assignee = one(sp, "assignee");
  const search = one(sp, "search");
  const filter = view === "overdue" ? { status: "open", due: "overdue" } : view === "closed" ? { status: "closed" } : view === "all" ? { status: "all" } : { status: "open" };
  const [tasks, members] = await Promise.all([
    apiTry<PageResult<FieldTask>>(`/api/field/tasks${qs({ ...filter, assignedToId: assignee, search, page, pageSize: 30 })}`),
    apiTry<TeamMember[]>("/api/field/agents"),
  ]);
  if (!tasks) {
    return (
      <div className="rounded-xl border border-line bg-surface p-5 text-sm text-ink-2">
        Нет доступа к задачам Sales Base. Их видят администратор, директор, РМ и супервайзеры.
      </div>
    );
  }

  const link = (p: Record<string, string | number | undefined>) => `${returnTo}${qs({ tab: "team", view, assignee, search, ...p })}`;
  return (
    <div className="space-y-3">
      <StatusTabs
        current={view}
        items={[
          { key: "open", label: "Открытые", href: link({ view: "open", page: undefined }) },
          { key: "overdue", label: "Просроченные", href: link({ view: "overdue", page: undefined }) },
          { key: "closed", label: "Закрытые", href: link({ view: "closed", page: undefined }) },
          { key: "all", label: "Все", href: link({ view: "all", page: undefined }) },
        ]}
      />
      <div className="flex flex-wrap gap-2">
        <SearchBox value={search ?? ""} placeholder="Поиск по задачам" />
        {(members?.length ?? 0) > 0 && (
          <ParamSelect
            param="assignee"
            value={assignee ?? ""}
            label="Исполнитель"
            options={[{ value: "", label: "Все исполнители" }, ...(members ?? []).map((m) => ({ value: m.id, label: `${m.name}${m.role === "Supervisor" ? " · супервайзер" : m.teamName ? ` · ${m.teamName}` : ""}` }))]}
          />
        )}
      </div>
      <p className="text-xs text-ink-3">Найдено: {tasks.total}</p>
      <FieldTeamTasks items={tasks.items} members={members ?? []} today={today} canCreate={(members?.length ?? 0) > 0} />
      <Pager page={tasks.page} pageSize={tasks.pageSize} total={tasks.total} href={(p) => link({ page: p })} />
    </div>
  );
}

/** Маршруты агентов на день: прогресс и переход к маршруту в Sales Base. */
async function RoutesTab({ sp, today }: { sp: SearchParams; today: string }) {
  const date = one(sp, "date") ?? today;
  const routes = await apiTry<FieldRouteSummary[]>(`/api/field/routes${qs({ date })}`);
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <DateSwitch value={date} today={today} />
      </div>
      {!routes ? (
        <div className="rounded-xl border border-line bg-surface p-5 text-sm text-ink-2">Нет доступа к маршрутам Sales Base.</div>
      ) : routes.length === 0 ? (
        <div className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">На этот день маршрутов нет</div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line bg-surface">
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-ink-3">
                <th className="px-4 py-2.5 font-medium">Агент</th>
                <th className="px-2 py-2.5 font-medium">Команда</th>
                <th className="px-2 py-2.5 text-right font-medium">Посещено</th>
                <th className="px-2 py-2.5 text-right font-medium">Пропущено</th>
                <th className="px-2 py-2.5 text-right font-medium">Км</th>
                <th className="px-2 py-2.5 font-medium">Источник</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {routes.map((r) => {
                const share = r.planned > 0 ? Math.round((r.visited / r.planned) * 100) : 0;
                return (
                  <tr key={r.id} className="border-b border-line last:border-0">
                    <td className="px-4 py-2.5 font-medium text-ink">{r.agentName}</td>
                    <td className="px-2 py-2.5 text-ink-2">{r.teamName ?? "—"}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">
                      <span className="text-ink">
                        {r.visited}/{r.planned}
                      </span>
                      <span className="ml-1.5 text-xs text-ink-3">{share}%</span>
                    </td>
                    <td className={`px-2 py-2.5 text-right tabular-nums ${r.skipped > 0 ? "text-bad" : "text-ink-3"}`}>{r.skipped}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums text-ink-2">{r.distanceKm.toLocaleString("ru-RU")}</td>
                    <td className="px-2 py-2.5 text-ink-2">{routeSourceLabel[r.source]}</td>
                    <td className="px-4 py-2.5 text-right">
                      <Link href={`/field/routes/${r.id}`} className="inline-flex items-center gap-1 text-accent-strong hover:underline">
                        Открыть <ExternalLink className="size-3.5" />
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

/** Задачи отдела OneBase. */
async function OfficeTab({ code, sp, page, today, returnTo }: { code: string; sp: SearchParams; page: number; today: string; returnTo: string }) {
  const view = one(sp, "view") ?? "open";
  const search = one(sp, "search");
  const filter =
    view === "mine" ? { status: "open", mine: "true" } : view === "created" ? { status: "open", createdByMe: "true" } : view === "overdue" ? { status: "overdue" } : view === "closed" ? { status: "closed" } : { status: "open" };
  const [tasks, assignees] = await Promise.all([
    apiGet<PageResult<WorkTask>>(`/api/work/tasks${qs({ department: code, ...filter, search, page, pageSize: 30 })}`, returnTo),
    apiGet<WorkAssignee[]>(`/api/work/assignees${qs({ department: code })}`, returnTo),
  ]);
  const link = (p: Record<string, string | number | undefined>) => `${returnTo}${qs({ tab: "office", view, search, ...p })}`;
  return (
    <div className="space-y-3">
      <StatusTabs
        current={view}
        items={[
          { key: "open", label: "Открытые", href: link({ view: "open", page: undefined }) },
          { key: "mine", label: "Мне", href: link({ view: "mine", page: undefined }) },
          { key: "created", label: "Поставил я", href: link({ view: "created", page: undefined }) },
          { key: "overdue", label: "Просроченные", href: link({ view: "overdue", page: undefined }) },
          { key: "closed", label: "Закрытые", href: link({ view: "closed", page: undefined }) },
        ]}
      />
      <SearchBox value={search ?? ""} placeholder="Поиск по задачам" />
      <WorkTaskBoard department={code} items={tasks.items} assignees={assignees} today={today} />
      <Pager page={tasks.page} pageSize={tasks.pageSize} total={tasks.total} href={(p) => link({ page: p })} />
    </div>
  );
}
