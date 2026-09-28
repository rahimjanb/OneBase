import { ChevronDown, Search } from "lucide-react";
import type { DepartmentStatus } from "@/lib/demo-data";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "outline";
  size?: "sm" | "md";
};

export function Button({ variant = "outline", size = "md", className = "", ...props }: ButtonProps) {
  const base = "inline-flex shrink-0 items-center justify-center gap-2 rounded-lg font-medium transition-colors disabled:opacity-50";
  const sizes = { sm: "h-8 px-3 text-xs", md: "h-9 px-4 text-sm" };
  const variants = {
    primary: "bg-accent text-white hover:bg-accent-strong",
    outline: "border border-line bg-surface text-ink hover:bg-muted",
  };
  return <button type="button" className={`${base} ${sizes[size]} ${variants[variant]} ${className}`} {...props} />;
}

export function Card({ className = "", ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={`rounded-xl border border-line bg-surface ${className}`} {...props} />;
}

export function ProgressBar({ value, tone = "accent" }: { value: number; tone?: "accent" | "warn" }) {
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
      <div
        className={`h-full rounded-full ${tone === "warn" ? "bg-warn" : "bg-accent"}`}
        style={{ width: `${Math.min(100, Math.max(0, value))}%` }}
      />
    </div>
  );
}

const statusStyles: Record<DepartmentStatus, { label: string; dot: string; text: string }> = {
  done: { label: "План выполнен", dot: "bg-ok", text: "text-ok" },
  ok: { label: "В норме", dot: "bg-ok", text: "text-ok" },
  attention: { label: "Требует внимания", dot: "bg-warn", text: "text-warn" },
};

export function StatusDot({ status }: { status: DepartmentStatus }) {
  const s = statusStyles[status];
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${s.text}`}>
      <span className={`size-2 rounded-full ${s.dot}`} />
      {s.label}
    </span>
  );
}

export function SearchInput({
  className = "",
  ...props
}: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className={`relative block ${className}`}>
      <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
      <input
        type="search"
        className="h-10 w-full rounded-lg border border-line bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        {...props}
      />
    </label>
  );
}

export function SelectField({ label, options }: { label: string; options: string[] }) {
  return (
    <label className="block">
      <span className="mb-2 block text-sm text-ink-2">{label}</span>
      <span className="relative block">
        <select className="h-11 w-full min-w-[160px] appearance-none rounded-lg border border-line bg-surface pl-3.5 pr-9 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20">
          {options.map((o) => (
            <option key={o}>{o}</option>
          ))}
        </select>
        <ChevronDown className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-ink-2" />
      </span>
    </label>
  );
}

export function SectionTitle({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex items-end justify-between gap-4">
      <div>
        <h2 className="text-lg font-semibold text-ink">{title}</h2>
        {subtitle && <p className="mt-1 text-sm text-ink-2">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

export function EmptyState({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">
      {children}
    </div>
  );
}
