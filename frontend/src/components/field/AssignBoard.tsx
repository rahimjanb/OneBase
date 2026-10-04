"use client";

import Link from "next/link";
import { useState } from "react";
import { GripVertical } from "lucide-react";
import type { FieldRoute } from "@/lib/field/types";
import { fieldApi, useAction } from "./hooks";

type Column = { agentId: string; agentName: string; route: FieldRoute | null };

/**
 * Распределение точек на день (супервайзер, большой экран): колонки агентов с точками маршрутов.
 * Перетащите точку к другому агенту — точка будет закреплена за ним, маршруты обоих пересчитаются.
 * На телефоне — меню «к агенту» у каждой точки.
 */
export function AssignBoard({ columns }: { columns: Column[] }) {
  const { busy, error, run } = useAction();
  const [drag, setDrag] = useState<{ marketId: number; name: string; from: string } | null>(null);
  const [over, setOver] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const reassign = (marketId: number, name: string, from: string, to: string) => {
    if (from === to) return;
    const target = columns.find((c) => c.agentId === to);
    if (!confirm(`Передать «${name}» агенту ${target?.agentName}? Точка закрепится за ним.`)) return;
    run(`${marketId}`, () => fieldApi.post<{ toAgent: string; addedToRoute: boolean }>(`customers/${marketId}/agent`, { agentId: to }), {
      onDone: (r) => setMessage(`«${name}» → ${r.toAgent}${r.addedToRoute ? ", добавлена в маршрут" : ""}`),
    });
  };

  return (
    <div className="space-y-3">
      {message && <p className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="flex gap-3 overflow-x-auto pb-2">
        {columns.map((c) => (
          <section
            key={c.agentId}
            onDragOver={(e) => {
              e.preventDefault();
              setOver(c.agentId);
            }}
            onDragLeave={() => setOver(null)}
            onDrop={() => {
              setOver(null);
              if (drag) reassign(drag.marketId, drag.name, drag.from, c.agentId);
              setDrag(null);
            }}
            className={`w-72 shrink-0 rounded-xl border bg-surface ${over === c.agentId ? "border-accent ring-2 ring-accent/30" : "border-line"}`}
          >
            <div className="border-b border-line px-3 py-2.5">
              <Link href={`/field/agents/${c.agentId}`} prefetch={false} className="block truncate text-sm font-semibold text-ink hover:underline">
                {c.agentName}
              </Link>
              <div className="text-xs text-ink-3">{c.route ? `${c.route.visited}/${c.route.planned} точек · ${c.route.distanceKm} км` : "маршрута нет"}</div>
            </div>
            <ul className="max-h-[60vh] space-y-1 overflow-y-auto p-2">
              {(c.route?.points ?? []).map((p) => (
                <li
                  key={p.id}
                  draggable={p.status === "Planned" && busy === null}
                  onDragStart={() => setDrag({ marketId: p.marketId, name: p.name, from: c.agentId })}
                  className={`flex items-center gap-2 rounded-lg border border-line px-2 py-1.5 text-sm ${p.status === "Planned" ? "cursor-grab bg-surface hover:bg-muted" : "bg-muted text-ink-3"}`}
                >
                  {p.status === "Planned" && <GripVertical className="size-3.5 shrink-0 text-ink-3" />}
                  <span className="w-5 shrink-0 text-xs tabular-nums text-ink-3">{p.sequence}</span>
                  <span className="min-w-0 flex-1 truncate" title={p.name}>
                    {p.name}
                  </span>
                  {p.status === "Planned" && (
                    <select
                      aria-label="Передать агенту"
                      value=""
                      onChange={(e) => e.target.value && reassign(p.marketId, p.name, c.agentId, e.target.value)}
                      className="w-7 shrink-0 cursor-pointer rounded border-0 bg-transparent text-xs text-ink-3 lg:hidden"
                    >
                      <option value="">→</option>
                      {columns.filter((o) => o.agentId !== c.agentId).map((o) => (
                        <option key={o.agentId} value={o.agentId}>
                          {o.agentName}
                        </option>
                      ))}
                    </select>
                  )}
                </li>
              ))}
              {!c.route && <li className="px-2 py-4 text-center text-xs text-ink-3">Перетащите точку сюда — она закрепится за агентом</li>}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
