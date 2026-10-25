import Link from "next/link";
import { Note, Section } from "@/components/sales/bits";
import { Tr } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { getT } from "@/i18n/server";
import { purposeNames, userLabel, type AiUsageView } from "@/lib/ai";
import { num } from "@/lib/sales/format";

const usd = (v: number) => `$${v < 1 ? v.toFixed(4) : num(v, 2)}`;

/** Доля промпта, прочитанная из кэша, в процентах — главный показатель того, работает ли кэширование. */
const cacheShare = (cacheRead: number, prompt: number) => (prompt > 0 ? Math.round((cacheRead / prompt) * 100) : 0);

/** Служебные значения из API: «система» — вызовы фоновых задач без пользователя, «без отдела» — у пользователя нет отдела. */
const departmentLabel = (name: string, t: T) => (name === "система" ? t("система") : name === "без отдела" ? t("без отдела") : name);

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
  const sliceHead = (first: string) => [first, t("Вызовы"), t("Токены"), t("Из кэша"), t("Стоимость")];
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

      {(data.budget.dailyUsd !== null || data.budget.monthlyUsd !== null) && (
        <div
          className={`mb-4 rounded-lg px-4 py-3 text-sm ${data.budget.exceeded ? "bg-bad-soft text-bad" : data.budget.warning ? "bg-warn-soft text-warn" : "bg-muted text-ink-2"}`}
        >
          <span className="font-semibold">{data.budget.exceeded ? t("Бюджет на AI исчерпан — новые запросы не выполняются.") : t("Бюджет на AI")}</span>{" "}
          {[
            data.budget.dailyUsd !== null && t("сегодня {spent} из {limit}", { spent: usd(data.budget.spentToday), limit: usd(data.budget.dailyUsd) }),
            data.budget.monthlyUsd !== null && t("в этом месяце {spent} из {limit}", { spent: usd(data.budget.spentMonth), limit: usd(data.budget.monthlyUsd) }),
          ]
            .filter(Boolean)
            .join(" · ")}
          .{" "}
          <Link href="/settings/ai" className="font-medium underline">
            {t("Изменить бюджет")}
          </Link>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Tile
          label={t("Запросов к AI")}
          value={num(p.requests)}
          note={`${t("сегодня {n}", { n: num(data.today.requests) })}${p.fromCache ? ` · ${t("из кэша ответов {n}", { n: num(p.fromCache) })}` : ""}${p.failedRequests ? ` · ${t("с ошибкой {n}", { n: num(p.failedRequests) })}` : ""}`}
        />
        <Tile
          label={t("Входных токенов")}
          value={num(p.usage.promptTokens)}
          note={`${t("из кэша {n} ({pct}%)", { n: num(p.usage.cacheReadTokens), pct: cacheShare(p.usage.cacheReadTokens, p.usage.promptTokens) })} · ${t("вызовов моделей {n}", { n: num(p.usage.calls) })}`}
        />
        <Tile
          label={t("Выходных токенов")}
          value={num(p.usage.outputTokens)}
          note={`${t("сегодня {n}", { n: num(data.today.usage.outputTokens) })}${p.failedCalls ? ` · ${t("вызовов с ошибкой {n}", { n: num(p.failedCalls) })}` : ""}`}
        />
        <Tile
          label={t("Оценка стоимости")}
          value={usd(p.usage.costUsd)}
          note={`${t("сегодня {n}", { n: usd(data.today.usage.costUsd) })} · ${incomplete ? `${t("без моделей, где не задана цена")} · ` : ""}${t("среднее время ответа {s} с", { s: num(p.avgResponseMs / 1000, 1) })}`}
        />
      </div>

      <Section title={t("По дням")}>
        <Table
          head={[t("День"), t("Запросы"), t("Вход"), t("Кэш-чтение"), t("Кэш-запись"), t("Выход"), t("Стоимость")]}
          rows={data.byDay.map((d) => [
            d.date.split("-").reverse().join("."),
            num(d.requests),
            num(d.inputTokens),
            num(d.cacheReadTokens),
            num(d.cacheWriteTokens),
            num(d.outputTokens),
            usd(d.costUsd),
          ])}
        />
      </Section>

      <Section title={t("По моделям")}>
        <Table
          head={[t("Модель"), t("Вызовы"), t("Ошибки"), t("Вход"), t("Кэш-чтение"), t("Кэш-запись"), t("Выход"), t("Стоимость"), t("Среднее время")]}
          rows={data.byModel.map((m) => [
            m.model,
            num(m.calls),
            num(m.failed),
            num(m.inputTokens),
            num(m.cacheReadTokens),
            num(m.cacheWriteTokens),
            num(m.outputTokens),
            m.priceMissing ? t("{cost} + нет цены", { cost: usd(m.costUsd) }) : usd(m.costUsd),
            t("{s} с", { s: num(m.avgMs / 1000, 1) }),
          ])}
        />
      </Section>

      <div className="grid grid-cols-[minmax(0,1fr)] gap-x-6 xl:grid-cols-2">
        <Section title={t("По пользователям")}>
          <Table
            head={[t("Пользователь"), t("Запросы"), t("Токены"), t("Из кэша"), t("Стоимость")]}
            rows={data.byUser.map((u) => [userLabel(u.user, t), num(u.requests), num(u.tokens), num(u.cacheReadTokens), usd(u.costUsd)])}
          />
        </Section>
        <Section title={t("По отделам")}>
          <Table
            head={[t("Отдел"), t("Запросы"), t("Токены"), t("Из кэша"), t("Стоимость")]}
            rows={data.byDepartment.map((d) => [departmentLabel(d.department, t), num(d.requests), num(d.tokens), num(d.cacheReadTokens), usd(d.costUsd)])}
          />
        </Section>
        <Section title={t("По AI-сотрудникам")}>
          <Table
            head={sliceHead(t("Сотрудник"))}
            rows={data.byAgent.map((a) => [a.agent === "—" ? t("без сотрудника") : t(agentNames[a.agent] ?? a.agent), num(a.calls), num(a.tokens), num(a.cacheReadTokens), usd(a.costUsd)])}
          />
        </Section>
        <Section title={t("По задачам")}>
          <Table
            head={sliceHead(t("Задача"))}
            rows={data.byPurpose.map((x) => [purposeNames[x.purpose] ? t(purposeNames[x.purpose]) : x.purpose, num(x.calls), num(x.tokens), num(x.cacheReadTokens), usd(x.costUsd)])}
          />
        </Section>
      </div>

      <Note>
        {t(
          "Токены — по ответам провайдеров: «Вход» — без кэша, чтение и запись кэша промпта — отдельно, по своим ценам из «Модели». Стоимость — оценка по ценам за 1 млн токенов; счёт провайдера может отличаться.",
        )}
        {incomplete && ` ${t("Для части моделей цена не задана — их стоимость не учтена.")}`}
      </Note>
    </>
  );
}
