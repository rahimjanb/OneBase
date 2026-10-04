import { redirect } from "next/navigation";
import { SettingsForm } from "@/components/field/Misc";
import { FieldPage, Panel } from "@/components/field/ui";
import { fieldGet, fieldMe } from "@/lib/field/api";
import type { FieldSettings } from "@/lib/field/types";

export const metadata = { title: "Настройки" };

type AuditRow = { id: number; timestamp: string; actor: string; action: string; entityType: string | null; entityId: string | null; data: string | null };

const actionLabel: Record<string, string> = {
  "field.customer.agent_changed": "Сменил агента точки",
  "field.customer.updated": "Изменил карточку точки",
  "field.route.built": "Построил маршрут",
  "field.route.changed": "Изменил маршрут",
  "field.task.created": "Поставил задачу",
  "field.task.updated": "Изменил задачу",
  "field.task.status": "Сменил статус задачи",
  "field.visit.started": "Начал визит",
  "field.visit.finished": "Завершил визит",
  "field.visit.cancelled": "Отменил визит",
  "field.joint_visit.created": "Назначил совместный выезд",
  "field.joint_visit.updated": "Записал итог выезда",
  "field.recommendation.approved": "Подтвердил рекомендацию AI",
  "field.recommendation.rejected": "Отклонил рекомендацию AI",
  "field.recommendations.generated": "Запуск AI-планирования",
  "field.member.created": "Добавил участника",
  "field.member.updated": "Изменил участника",
  "field.member.access_granted": "Выдал вход",
  "field.member.password_reset": "Сменил пароль",
  "field.members.imported": "Импорт из Linko",
  "field.team.created": "Создал команду",
  "field.team.updated": "Изменил команду",
  "field.settings.updated": "Изменил настройки",
};

/** Настройки Sales Base и журнал действий (РМ). */
export default async function SettingsPage() {
  const me = await fieldMe();
  if (!me.canManageOrg) redirect("/field");
  const [settings, audit] = await Promise.all([fieldGet<FieldSettings>("settings", "/field/settings"), fieldGet<AuditRow[]>("audit?take=60", "/field/settings")]);
  return (
    <FieldPage title="Настройки" subtitle="Геозона визитов, маршруты, пороги AI-планирования">
      <SettingsForm initial={settings} />
      <Panel title="Журнал действий" hint="последние 60">
        <ul className="divide-y divide-line">
          {audit.map((a) => (
            <li key={a.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 py-2 text-sm">
              <span className="w-32 shrink-0 text-xs text-ink-3">{new Date(a.timestamp).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
              <span className="text-ink">{a.actor}</span>
              <span className="text-ink-2">{actionLabel[a.action] ?? a.action}</span>
              {a.data && <span className="w-full truncate pl-32 text-xs text-ink-3 max-sm:pl-0">{a.data}</span>}
            </li>
          ))}
        </ul>
      </Panel>
    </FieldPage>
  );
}
