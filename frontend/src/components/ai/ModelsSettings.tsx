"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Loader2 } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import type { AiModelView, AiProviderView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { date } from "@/lib/sales/format";
import { field } from "./form";

const priceField =
  "h-8 w-24 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

function parsePrice(text: string): number | null | undefined {
  const t = text.trim().replace(",", ".");
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) && n >= 0 ? n : undefined;
}

export function ModelsSettings({ initial, providers }: { initial: AiModelView[]; providers: AiProviderView[] }) {
  const [models, setModels] = useState(initial);
  const [query, setQuery] = useState("");
  const [onlyEnabled, setOnlyEnabled] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const save = async (m: AiModelView, patch: Partial<Pick<AiModelView, "enabled" | "inputPricePerMillion" | "outputPricePerMillion">>) => {
    setSaving(m.id);
    setError(null);
    const next = { ...m, ...patch };
    try {
      setModels(
        await bff<AiModelView[]>(`ai/models/${m.id}`, {
          method: "PUT",
          body: JSON.stringify({ enabled: next.enabled, inputPricePerMillion: next.inputPricePerMillion, outputPricePerMillion: next.outputPricePerMillion }),
        }),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(null);
    }
  };

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    return models.filter((m) => (!onlyEnabled || m.enabled) && (!q || m.model.toLowerCase().includes(q) || (m.displayName ?? "").toLowerCase().includes(q)));
  }, [models, query, onlyEnabled]);

  if (models.length === 0) {
    return (
      <Section title="Модели">
        <p className="text-sm text-ink-2">
          Список моделей ещё не загружен. Сохраните ключ провайдера и нажмите «Обновить список моделей» в разделе{" "}
          <Link href="/settings/ai/providers" className="font-medium text-accent-strong hover:underline">
            Провайдеры
          </Link>
          .
        </p>
      </Section>
    );
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-4">
        <input className={`${field} max-w-xs`} value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Поиск модели" />
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={onlyEnabled} onChange={(e) => setOnlyEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          Только разрешённые
        </label>
        {error && <span className="text-sm text-bad">{error}</span>}
      </div>

      {providers.map((p) => {
        const rows = visible.filter((m) => m.provider === p.code);
        if (rows.length === 0) return null;
        return (
          <Section key={p.code} title={p.name} hint={`${rows.filter((m) => m.enabled).length} разрешено из ${rows.length}`}>
            <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
              <table className="w-full min-w-max text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">Разрешить</th>
                    <th className="py-2 pr-3 font-semibold">Модель</th>
                    <th className="py-2 pr-3 font-semibold">Выпущена</th>
                    <th className="py-2 pr-3 text-right font-semibold" title="Цена 1 млн входных токенов, USD — для расчёта стоимости">Вход, $/1М</th>
                    <th className="py-2 text-right font-semibold" title="Цена 1 млн выходных токенов, USD">Выход, $/1М</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((m) => (
                    <tr key={m.id} className="border-b border-line last:border-0">
                      <td className="py-2 pr-3">
                        <span className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={m.enabled}
                            disabled={saving === m.id}
                            onChange={(e) => save(m, { enabled: e.target.checked })}
                            aria-label={`Разрешить ${m.model}`}
                            className="size-4 accent-[var(--color-accent)]"
                          />
                          {saving === m.id && <Loader2 className="size-3.5 animate-spin text-ink-3" />}
                        </span>
                      </td>
                      <td className="py-2 pr-3">
                        <div className="font-medium text-ink">{m.displayName ?? m.model}</div>
                        {m.displayName && <div className="font-mono text-xs text-ink-3">{m.model}</div>}
                        {!m.available && <span className="mt-0.5 inline-block rounded-full bg-warn-soft px-2 py-0.5 text-[11px] text-warn">нет в последнем списке провайдера</span>}
                      </td>
                      <td className="py-2 pr-3 tabular-nums text-ink-2">{m.createdAt ? date(m.createdAt) : "—"}</td>
                      {(["inputPricePerMillion", "outputPricePerMillion"] as const).map((key) => (
                        <td key={key} className="py-2 pr-3 text-right last:pr-0">
                          <input
                            className={priceField}
                            defaultValue={m[key] ?? ""}
                            key={`${m.id}-${key}-${m[key]}`}
                            placeholder="—"
                            inputMode="decimal"
                            aria-label={key === "inputPricePerMillion" ? "Цена входа" : "Цена выхода"}
                            onBlur={(e) => {
                              const value = parsePrice(e.target.value);
                              if (value === undefined) {
                                setError("Цена — неотрицательное число, например 2.5");
                                return;
                              }
                              if (value !== m[key]) void save(m, { [key]: value });
                            }}
                          />
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Section>
        );
      })}

      <Note>
        Список моделей приходит из API провайдера. Разрешённые модели можно назначать консультанту и AI-сотрудникам. Цены провайдеры через API не отдают —
        укажите их по прайсу провайдера, если нужен расчёт стоимости в разделе «Использование»; без цены стоимость не считается.
      </Note>
    </>
  );
}
