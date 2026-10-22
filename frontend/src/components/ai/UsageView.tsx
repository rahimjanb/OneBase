import Link from "next/link";
import { Note, Section } from "@/components/sales/bits";
import { Tr } from "@/i18n/client";
import { getT } from "@/i18n/server";
import { purposeNames, userLabel, type AiUsageView } from "@/lib/ai";
import { num } from "@/lib/sales/format";

const usd = (v: number) => `$${v < 1 ? v.toFixed(4) : num(v, 2)}`;

function Tile({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="rounded-xl border border-line bg-surface p-5">
      <div className="text-sm text-ink-2">{label}</div>
      <div className="mt-2 text-[1.75rem] font-semibold leading-none tracking-tight text-ink">{value}</div>
      {note && <div className="mt-2 text-xs text-ink-3">{note}</div>}
    </div>
  );
}

function Table({ head, rows }: { head: string[]; rows: (string | number)[][] }) {
  return (
    <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
      <table className="w-full min-w-max text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            {head.map((h, i) => (
              <th key={h} className={`py-2 pr-3 font-semibold ${i > 0 ? "text-right" : ""}`}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i} className="border-b border-line last:border-0">
              {r.map((c, j) => (
                <td key={j} className={`py-2 pr-3 ${j > 0 ? "text-right tabular-nums" : "text-ink"}`}>
                  {c}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length === 0 && (
        <p className="py-4 text-center text-sm text-ink-3">
          <Tr>Вызовов за период нет</Tr>
        </p>
      )}
    </div>
  );
}

/** Серверный компонент (страница «Использование»): тексты — через getT. */
export async function UsageView({ data, agentNames }: { data: AiUsageView; agentNames: Record<string, string> }) {
  const t = await getT();
  const p = data.period;
  const incomplete = p.usage.costIncomplete || data.byModel.some((m) => m.priceMissing);
  return (
    <>
      <div className="mb-4 flex flex-wrap gap-2 text-sm">
        {[1, 7, 30, 90].map((d) => (
          <Link
            key={d}
            href={`/settings/ai/usage?days=${d}`}
            className={`rounded-full border px-3 py-1 ${data.days === d ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
          >
            {d === 1 ? t("Сегодня") : t("{n} дней", { n: d })}
          </Link>
        ))}
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Tile
          label={t("Запросов к AI")}
          value={num(p.requests)}
          note={`${t("сегодня {n}", { n: num(data.today.requests) })}${p.failedRequests ? ` · ${t("с ошибкой {n}", { n: num(p.failedRequests) })}` : ""}`}
        />
        <Tile label={t("Входных токенов")} value={num(p.usage.inputTokens)} note={t("вызовов моделей {n}", { n: num(p.usage.calls) })} />
        <Tile
          label={t("Выходных токенов")}
          value={num(p.usage.outputTokens)}
          note={`${t("сегодня {n}", { n: num(data.today.usage.outputTokens) })}${p.failedCalls ? ` · ${t("вызовов с ошибкой {n}", { n: num(p.failedCalls) })}` : ""}`}
        />
        <Tile
          label={t("Оценка стоимости")}
          value={usd(p.usage.costUsd)}
          note={`${incomplete ? `${t("без моделей, где не задана цена")} · ` : ""}${t("среднее время ответа {s} с", { s: num(p.avgResponseMs / 1000, 1) })}`}
        />
      </div>

      <Section title={t("По дням")}>
        <Table
          head={[t("День"), t("Запросы"), t("Вход"), t("Выход"), t("Стоимость")]}
          rows={data.byDay.map((d) => [d.date.split("-").reverse().join("."), num(d.requests), num(d.inputTokens), num(d.outputTokens), usd(d.costUsd)])}
        />
      </Section>

      <Section title={t("По моделям")}>
        <Table
          head={[t("Модель"), t("Вызовы"), t("Ошибки"), t("Вход"), t("Выход"), t("Стоимость"), t("Среднее время")]}
          rows={data.byModel.map((m) => [
            m.model,
            num(m.calls),
            num(m.failed),
            num(m.inputTokens),
            num(m.outputTokens),
            m.priceMissing ? t("{cost} + нет цены", { cost: usd(m.costUsd) }) : usd(m.costUsd),
            t("{s} с", { s: num(m.avgMs / 1000, 1) }),
          ])}
        />
      </Section>

      <div className="grid grid-cols-[minmax(0,1fr)] gap-x-6 xl:grid-cols-2 min-[106.25rem]:grid-cols-3">
        <Section title={t("По пользователям")}>
          <Table head={[t("Пользователь"), t("Запросы"), t("Токены"), t("Стоимость")]} rows={data.byUser.map((u) => [userLabel(u.user, t), num(u.requests), num(u.tokens), usd(u.costUsd)])} />
        </Section>
        <Section title={t("По AI-сотрудникам")}>
          <Table
            head={[t("Сотрудник"), t("Вызовы"), t("Токены"), t("Стоимость")]}
            rows={data.byAgent.map((a) => [a.agent === "—" ? t("без сотрудника") : t(agentNames[a.agent] ?? a.agent), num(a.calls), num(a.tokens), usd(a.costUsd)])}
          />
        </Section>
        <Section title={t("По задачам")}>
          <Table
            head={[t("Задача"), t("Вызовы"), t("Токены"), t("Стоимость")]}
            rows={data.byPurpose.map((x) => [purposeNames[x.purpose] ? t(purposeNames[x.purpose]) : x.purpose, num(x.calls), num(x.tokens), usd(x.costUsd)])}
          />
        </Section>
      </div>

      <Note>
        {t("Токены — по ответам провайдеров. Стоимость — оценка по ценам, указанным в «Модели» (за 1 млн токенов); счёт провайдера может отличаться.")}
        {incomplete && ` ${t("Для части моделей цена не задана — их стоимость не учтена.")}`}
      </Note>
    </>
  );
}
