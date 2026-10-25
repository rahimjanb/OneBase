import type { T } from "@/i18n/core";
import { formatDate } from "@/i18n/core";
import type { ActivityEvent, Insight, UpcomingTask } from "@/lib/dashboard.mock";
import { TIME_ZONE } from "@/lib/format";
import type { AiAlertsView } from "@/lib/ai";
import { alertText, categoryNames } from "@/lib/ai";
import type { DepartmentTasks, WorkTask } from "@/lib/tasks";

/** Уведомление пользователя (GET /api/notifications) — как в колокольчике. */
export type NotificationRow = { id: string; kind: string | number; title: string; body: string | null; link: string | null; createdAt: string; read: boolean };

/** Три инсайта «Фокуса дня» из реальных находок консультанта: критичные и предупреждения — первыми, затем возможности. */
export function insightsOf(alerts: AiAlertsView, t: T): Insight[] {
  const rank = { Critical: 0, Warning: 1, Opportunity: 2 } as Record<string, number>;
  return [...alerts.alerts]
    .sort((a, b) => (rank[a.severity] ?? 1) - (rank[b.severity] ?? 1))
    .slice(0, 3)
    .map((a) => ({
      id: a.id,
      level: a.severity === "Critical" ? "bad" : a.severity === "Opportunity" ? "ok" : "warn",
      levelLabel: a.severity === "Critical" ? "Критично" : a.severity === "Opportunity" ? "Возможность" : "Внимание",
      department: categoryNames[a.category] ?? a.category,
      text: alertText(a.title, t),
      action: a.href ? "Открыть" : "Подробнее",
      href: a.href ?? "/ai",
    }));
}

const dayOf = (date: Date) => new Intl.DateTimeFormat("en-CA", { timeZone: TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit" }).format(date);

/** Инициалы из имени: «Азиз Назаров» → «АН». */
export const initialsOf = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0]!.toUpperCase())
    .join("");

/**
 * «Ближайшие дедлайны»: открытые задачи отделов со сроком — сначала просроченные и сегодняшние, потом по сроку; не больше take.
 * Срок — «Сегодня», «Завтра» или дата; тон — горит, скоро, спокойный.
 */
export function upcomingOf(tasks: { task: WorkTask; department: DepartmentTasks }[], now: Date, locale: string, t: T, take = 5): UpcomingTask[] {
  const today = dayOf(now);
  const tomorrow = dayOf(new Date(now.getTime() + 86_400_000));
  return tasks
    .filter(({ task }) => (task.status === "New" || task.status === "InProgress") && task.dueDate)
    .sort((a, b) => a.task.dueDate!.localeCompare(b.task.dueDate!))
    .slice(0, take)
    .map(({ task, department }) => {
      const due = task.dueDate!.slice(0, 10);
      const late = task.overdue || due < today;
      const label = due === today ? t("Сегодня") : due === tomorrow ? t("Завтра") : formatDate(new Date(`${due}T12:00:00`), locale, { day: "numeric", month: "short" });
      return {
        id: task.id,
        title: task.title,
        department: department.name,
        href: `/tasks/${department.code}`,
        priority: task.priority === "Urgent" || task.priority === "High" ? "high" : task.priority === "Low" ? "low" : "medium",
        due: late && due !== today ? `${label} · ${t("просрочено")}` : label,
        dueTone: late || due === today ? "bad" : due === tomorrow ? "warn" : "muted",
        assignee: task.assigneeName ? { initials: initialsOf(task.assigneeName), tone: "accent" } : null,
      };
    });
}

/** «12 мин», «3 ч», «2 дн» — сколько прошло с момента события. */
export function agoOf(iso: string, now: Date, t: T): string {
  const minutes = Math.max(0, Math.round((now.getTime() - new Date(iso).getTime()) / 60_000));
  if (minutes < 60) return t("{n} мин", { n: Math.max(1, minutes) });
  if (minutes < 24 * 60) return t("{n} ч", { n: Math.round(minutes / 60) });
  return t("{n} дн", { n: Math.round(minutes / (24 * 60)) });
}

const kindOf = (kind: string | number): ActivityEvent["icon"] => {
  const k = String(kind);
  if (k.includes("Done") || k === "2" || k === "3") return "check";
  if (k.includes("Assigned") || k === "0") return "file";
  if (k.includes("Changed") || k === "1") return "alert";
  return "comment";
};

/** «Лента активности» из уведомлений пользователя: кто/что — заголовок и текст уведомления, когда — относительное время. */
export function activityOf(rows: NotificationRow[], now: Date, t: T, take = 5): ActivityEvent[] {
  return rows.slice(0, take).map((r) => {
    const icon = kindOf(r.kind);
    return {
      id: r.id,
      icon,
      tone: icon === "check" ? "ok" : icon === "alert" ? "warn" : "accent",
      who: r.title,
      what: r.body ?? "",
      when: agoOf(r.createdAt, now, t),
      href: r.link,
    };
  });
}
