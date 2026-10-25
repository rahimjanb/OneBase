"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Loader2 } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import type { AiModelPriceKey, AiModelView, AiProviderView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { date } from "@/lib/sales/format";
import { field } from "./form";

const priceField =
  "h-8 w-24 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

/** Четыре цены модели за 1 млн токенов: вход, выход, чтение кэша промпта, запись в кэш (у Claude — свои тарифы на кэш). */
const priceColumns: { key: AiModelPriceKey; head: string; title: string; label: string }[] = [
  { key: "inputPricePerMillion", head: "Вход, $/1М", title: "Цена 1 млн входных токенов, USD — для расчёта стоимости", label: "токены::Цена входа" },
  { key: "outputPricePerMillion", head: "Выход, $/1М", title: "Цена 1 млн выходных токенов, USD", label: "токены::Цена выхода" },
  { key: "cacheReadPricePerMillion", head: "Кэш-чтение, $/1М", title: "Цена 1 млн токенов, прочитанных из кэша промпта, USD", label: "токены::Цена кэш-чтения" },
  { key: "cacheWritePricePerMillion", head: "Кэш-запись, $/1М", title: "Цена 1 млн токенов, записанных в кэш промпта, USD", label: "токены::Цена кэш-записи" },
];

function parsePrice(text: string): number | null | undefined {
  const t = text.trim().replace(",", ".");
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) && n >= 0 ? n : undefined;
}

export function ModelsSettings({ initial, providers }: { initial: AiModelView[]; providers: AiProviderView[] }) {
  const t = useT();
  const [models, setModels] = useState(initial);
  const [query, setQuery] = useState("");
  const [onlyEnabled, setOnlyEnabled] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const save = async (m: AiModelView, patch: Partial<Pick<AiModelView, "enabled" | AiModelPriceKey>>) => {
    setSaving(m.id);
    setError(null);
    const next = { ...m, ...patch };
    try {
      setModels(
        await bff<AiModelView[]>(`ai/models/${m.id}`, {
          method: "PUT",
          body: JSON.stringify({
            enabled: next.enabled,
            inputPricePerMillion: next.inputPricePerMillion,
            outputPricePerMillion: next.outputPricePerMillion,
            cacheReadPricePerMillion: next.cacheReadPricePerMillion,
            cacheWritePricePerMillion: next.cacheWritePricePerMillion,
          }),
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
      <Section title={t("Модели")}>
        <p className="text-sm text-ink-2">
          {t("Список моделей ещё не загружен. Сохраните ключ провайдера и нажмите «Обновить список моделей» в разделе")}{" "}
          <Link href="/settings/ai/providers" className="font-medium text-accent-strong hover:underline">
            {t("Провайдеры")}
          </Link>
          .
        </p>
      </Section>
    );
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-4">
        <input className={`${field} max-w-xs`} value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Поиск модели")} />
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={onlyEnabled} onChange={(e) => setOnlyEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          {t("Только разрешённые")}
        </label>
        {error && <span className="text-sm text-bad">{error}</span>}
      </div>

      {providers.map((p) => {
        const rows = visible.filter((m) => m.provider === p.code);
        if (rows.length === 0) return null;
        return (
          <Section key={p.code} title={p.name} hint={t("{n} разрешено из {total}", { n: rows.filter((m) => m.enabled).length, total: rows.length })}>
            <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
              <table className="w-full min-w-max text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">{t("Разрешить")}</th>
                    <th className="py-2 pr-3 font-semibold">{t("Модель")}</th>
                    <th className="py-2 pr-3 font-semibold">{t("Выпущена")}</th>
                    {priceColumns.map((c) => (
                      <th key={c.key} className="py-2 pr-3 text-right font-semibold last:pr-0" title={t(c.title)}>
                        {t(c.head)}
                      </th>
                    ))}
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
                            aria-label={t("Разрешить {title}", { title: m.model })}
                            className="size-4 accent-[var(--color-accent)]"
                          />
                          {saving === m.id && <Loader2 className="size-3.5 animate-spin text-ink-3" />}
                        </span>
                      </td>
                      <td className="py-2 pr-3">
                        <div className="font-medium text-ink">{m.displayName ?? m.model}</div>
                        {m.displayName && <div className="font-mono text-xs text-ink-3">{m.model}</div>}
                        {!m.available && (
                          <span className="mt-0.5 inline-block rounded-full bg-warn-soft px-2 py-0.5 text-[11px] text-warn">{t("нет в последнем списке провайдера")}</span>
                        )}
                      </td>
                      <td className="py-2 pr-3 tabular-nums text-ink-2">{m.createdAt ? date(m.createdAt) : "—"}</td>
                      {priceColumns.map(({ key, label }) => (
                        <td key={key} className="py-2 pr-3 text-right last:pr-0">
                          <input
                            className={priceField}
                            defaultValue={m[key] ?? ""}
                            key={`${m.id}-${key}-${m[key]}`}
                            placeholder="—"
                            inputMode="decimal"
                            aria-label={t(label)}
                            onBlur={(e) => {
                              const value = parsePrice(e.target.value);
                              if (value === undefined) {
                                setError(t("Цена — неотрицательное число, например 2.5"));
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
        {t(
          "Список моделей приходит из API провайдера. Разрешённые модели можно назначать консультанту и AI-сотрудникам. Цены провайдеры через API не отдают — укажите их по прайсу провайдера, если нужен расчёт стоимости в разделе «Использование»; без цены стоимость не считается.",
        )}{" "}
        {t(
          "У Claude четыре цены: вход, выход, чтение и запись кэша промпта (platform.claude.com/docs/en/about-claude/pricing); без цен кэша вызовы с кэшем остаются без стоимости. Haiku 5.5 при промпте длиннее 100 000 токенов считается по тарифу в 5 раз выше — это учитывается автоматически.",
        )}
      </Note>
    </>
  );
}
