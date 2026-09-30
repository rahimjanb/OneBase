import Link from "next/link";
import {
  ArrowLeftRight,
  ArrowRight,
  ArrowUpRight,
  Asterisk,
  Diamond,
  Factory,
  UserRound,
  type LucideIcon,
} from "lucide-react";
import { Card, ProgressBar, StatusDot } from "@/components/ui";
import type { Department } from "@/lib/demo-data";
import { filesLabel, foldersLabel, percent } from "@/lib/format";

const icons: Record<string, LucideIcon> = {
  production: Factory,
  finance: Diamond,
  sales: ArrowUpRight,
  marketing: Asterisk,
  supply: ArrowLeftRight,
  hr: UserRound,
};

export function DepartmentIcon({ code }: { code: string }) {
  const Icon = icons[code] ?? Diamond;
  return (
    <span className="grid size-10 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
      <Icon className="size-[18px]" strokeWidth={2} />
    </span>
  );
}

/** Отделы со своим разделом в OneBase: карточка ведёт туда, а не в файлы отдела в «Общей базе». */
const sections: Record<string, string> = {
  sales: "/sales",
};

/**
 * Карточка показателей отдела (Dashboard, «Отделы»): открывает раздел отдела. Раздела ещё нет — карточка не кликается
 * (файлы отдела — в «Общей базе»).
 */
export function DepartmentStatusCard({ department: d }: { department: Department }) {
  const href = sections[d.code];
  const card = (
    <Card className={`h-full p-5 ${href ? "transition-colors group-hover:border-accent/40" : "opacity-70"}`}>
      <div className="flex items-center justify-between">
        <span className="font-semibold text-ink">{d.name}</span>
        {href ? (
          <ArrowUpRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
        ) : (
          <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-ink-3">скоро</span>
        )}
      </div>
      <div className="mt-3 flex items-center gap-4">
        <span className="text-[28px] font-semibold leading-none tracking-tight">{percent(d.score)}</span>
        <StatusDot status={d.status} />
      </div>
      <div className="mt-4 text-xs text-ink-2">{d.metric.label}</div>
      <div className="mt-2 flex items-center gap-4">
        <ProgressBar value={d.metric.value} tone={d.status === "attention" ? "warn" : "accent"} />
        <span className="w-10 shrink-0 text-right text-xs font-semibold text-ink">{percent(d.metric.value)}</span>
      </div>
    </Card>
  );
  return href ? (
    <Link href={href} className="group block">
      {card}
    </Link>
  ) : (
    <div aria-disabled="true" title="Раздел отдела в разработке">
      {card}
    </div>
  );
}

/** Карточка рабочего пространства отдела в «Общей базе». */
export function DepartmentWorkspaceCard({ department: d }: { department: Department }) {
  return (
    <Link href={`/base/${d.code}`} className="group block">
      <Card className="h-full p-5 transition-colors group-hover:border-accent/40">
        <div className="flex items-center gap-3">
          <DepartmentIcon code={d.code} />
          <span className="flex-1 font-semibold text-ink">{d.name}</span>
          <ArrowRight className="size-4 text-ink-3 transition-transform group-hover:translate-x-0.5 group-hover:text-accent-strong" />
        </div>
        <div className="mt-5 text-sm text-ink-2">
          {filesLabel(d.filesCount)} <span className="mx-1.5 text-ink-3">·</span> {foldersLabel(d.foldersCount)}
        </div>
        <div className="mt-4 border-t border-line pt-4 text-xs text-ink-3">Последнее обновление: {d.updated}</div>
      </Card>
    </Link>
  );
}
