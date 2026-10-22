"use client";

import { useCallback, useEffect, useState } from "react";
import { ChevronDown, Loader2, RefreshCw } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { statusView, when, type ChangeSet, type ChangeSetRow, type SetStatus } from "@/lib/data-manager";
import { ChangeSetCard } from "./ChangeSetCard";

type Props = {
  canChange: boolean;
  /** Номер обновления: растёт после применения, отмены или отката в чате — журнал перечитывается. */
  version: number;
  acting: string | null;
  onApply: (set: ChangeSet) => void;
  onCancel: (set: ChangeSet) => void;
  onRollback: (set: ChangeSet) => void;
};

const filters: { key: "all" | SetStatus; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "Draft", label: "Ждут подтверждения" },
  { key: "Applied", label: "Применены" },
  { key: "RolledBack", label: "Откачены" },
  { key: "Cancelled", label: "Отменены" },
];

/** Журнал операций всех администраторов: кто, когда, что подготовил и применил; раскрытие — полный план с действиями. */
export function OperationsJournal({ canChange, version, acting, onApply, onCancel, onRollback }: Props) {
  const t = useT();
  const [rows, setRows] = useState<ChangeSetRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<"all" | SetStatus>("all");
  const [open, setOpen] = useState<string | null>(null);
  const [details, setDetails] = useState<Record<string, ChangeSet>>({});

  const load = useCallback(async () => {
    setError(null);
    try {
      setRows(await bff<ChangeSetRow[]>("data-manager/change-sets?take=100"));
    } catch (e) {
      setError((e as Error).message);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, version]);

  useEffect(() => {
    if (!open) return;
    bff<ChangeSet>(`data-manager/change-sets/${open}`)
      .then((set) => setDetails((prev) => ({ ...prev, [set.id]: set })))
      .catch((e) => setError((e as Error).message));
  }, [open, version]);

  const visible = (rows ?? []).filter((r) => filter === "all" || r.status === filter || (filter === "RolledBack" && r.status === "PartiallyRolledBack"));

  return (
    <div className="mx-auto max-w-5xl px-4 py-6 sm:px-6">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="flex-1 text-lg font-semibold text-ink">{t("Журнал операций")}</h2>
        <button type="button" onClick={() => void load()} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm text-ink-2 hover:bg-muted">
          <RefreshCw className="size-4" />
          {t("Обновить")}
        </button>
      </div>
      <p className="mt-1 text-sm text-ink-2">
        {t("Планы изменений всех администраторов: подготовленные AI, подтверждённые, отменённые и откаченные. Значения «было → стало» хранятся для каждой строки.")}
      </p>
      <div className="mt-4 flex flex-wrap gap-1.5">
        {filters.map((f) => (
          <button
            key={f.key}
            type="button"
            onClick={() => setFilter(f.key)}
            className={`rounded-full px-3 py-1 text-sm ${filter === f.key ? "bg-accent-soft font-medium text-accent-strong" : "text-ink-2 hover:bg-muted"}`}
          >
            {t(f.label)}
          </button>
        ))}
      </div>

      {error && <div className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}
      {rows === null && !error && (
        <div className="mt-8 flex justify-center text-ink-3">
          <Loader2 className="size-5 animate-spin" />
        </div>
      )}
      {rows !== null && visible.length === 0 && <p className="mt-8 text-center text-sm text-ink-3">{t("Операций пока нет")}</p>}

      <div className="mt-4 space-y-2">
        {visible.map((r) => {
          const status = statusView[r.status];
          const isOpen = open === r.id;
          return (
            <div key={r.id} className="rounded-xl border border-line bg-surface">
              <button type="button" onClick={() => setOpen(isOpen ? null : r.id)} className="flex w-full items-start gap-3 px-4 py-3 text-left" aria-expanded={isOpen}>
                <ChevronDown className={`mt-0.5 size-4 shrink-0 text-ink-3 transition-transform ${isOpen ? "" : "-rotate-90"}`} />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium text-ink">{t(r.title)}</span>
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${status.tone}`}>{t(status.label)}</span>
                  </div>
                  <div className="mt-0.5 text-xs text-ink-3">
                    {when(r.createdAt)}
                    {r.userName ? ` · ${t("подготовил {name}", { name: r.userName })}` : ""}
                    {r.appliedAt ? ` · ${t("применил {name} {date}", { name: r.appliedBy ?? "—", date: when(r.appliedAt) })}` : ""}
                    {r.rolledBackAt ? ` · ${t("откачен {date}", { date: when(r.rolledBackAt) })}` : ""}
                  </div>
                </div>
                {r.counts && (
                  <div className="hidden shrink-0 text-right text-xs text-ink-2 sm:block">
                    +{r.counts.create} ~{r.counts.update} −{r.counts.delete}
                    {r.counts.conflict > 0 && <span className="ml-1 text-warn">! {r.counts.conflict}</span>}
                  </div>
                )}
              </button>
              {isOpen && (
                <div className="border-t border-line p-3">
                  {details[r.id] ? (
                    <ChangeSetCard
                      set={details[r.id]}
                      canChange={canChange}
                      acting={acting === r.id}
                      locked={acting !== null}
                      onApply={onApply}
                      onCancel={onCancel}
                      onRollback={onRollback}
                    />
                  ) : (
                    <div className="flex justify-center py-4 text-ink-3">
                      <Loader2 className="size-5 animate-spin" />
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
