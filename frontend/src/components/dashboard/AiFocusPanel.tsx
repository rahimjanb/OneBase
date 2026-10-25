import Link from "next/link";
import { ArrowRight, ArrowUpRight, MessageSquareText, Sparkles } from "lucide-react";
import type { Insight, InsightLevel } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";

const levelClass: Record<InsightLevel, { chip: string; dot: string }> = {
  bad: { chip: "bg-bad/10 text-bad", dot: "bg-bad" },
  warn: { chip: "bg-warn/10 text-warn", dot: "bg-warn" },
  ok: { chip: "bg-ok/10 text-ok", dot: "bg-ok" },
};

/**
 * «Фокус дня» — до трёх инсайтов AI (критично, внимание, возможность) на градиентной панели с градиентной обводкой. Инсайты — только
 * реальные находки проактивного анализа консультанта; available = false — находки недоступны (нет права или сервис не ответил).
 */
export function AiFocusPanel({ insights, available, departments, tasks, t }: { insights: Insight[]; available: boolean; departments: number; tasks: number; t: T }) {
  return (
    <section className="rounded-[14px] bg-gradient-to-r from-accent/50 via-violet/30 to-transparent p-px">
      <div className="flex flex-col gap-4 rounded-[13px] bg-gradient-to-r from-ai-glow to-surface to-[65%] p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-[10px] border border-accent/40 bg-accent/20 text-accent-light">
              <Sparkles className="size-[18px]" strokeWidth={1.75} />
            </span>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-[15px] font-semibold text-ink">{t("Фокус дня")}</h2>
                {insights.length > 0 && (
                  <span className="rounded-[5px] bg-violet/20 px-[7px] py-0.5 text-[10.5px] font-semibold text-violet-light">{t("AI · {n} инсайта", { n: insights.length })}</span>
                )}
              </div>
              <p className="mt-0.5 text-[12.5px] text-ink-3">{t("Сформировано на основе данных {d} отделов и {n} активных задач", { d: departments, n: tasks })}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <Link
              href="/consultant"
              className="inline-flex h-[34px] items-center gap-2 rounded-[9px] border border-white/12 bg-white/8 px-3 text-[12.5px] font-semibold text-ink transition-colors duration-150 hover:bg-white/12 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              <MessageSquareText className="size-4" strokeWidth={1.75} />
              {t("Спросить AI")}
            </Link>
            <Link href="/ai" className="inline-flex items-center gap-1 text-[12.5px] font-semibold text-accent-light hover:text-ink">
              {t("Открыть обзор")} <ArrowRight className="size-3.5" />
            </Link>
          </div>
        </div>

        {insights.length === 0 && (
          <p className="rounded-[10px] border border-line bg-muted/50 px-4 py-5 text-center text-[13px] text-ink-3">
            {available ? t("Проактивный анализ не нашёл проблем в данных OneBase.") : t("Находки консультанта недоступны: нет права на консультанта или сервис не ответил.")}
          </p>
        )}
        <div className={insights.length === 0 ? "hidden" : "grid gap-3 md:grid-cols-3"}>
          {insights.map((i) => (
            <article key={i.id} className="flex flex-col gap-2.5 rounded-[10px] border border-line bg-muted/50 p-3.5">
              <div className="flex flex-wrap items-center gap-2">
                <span className={`inline-flex items-center gap-1.5 rounded-[5px] px-2 py-0.5 text-[11px] font-semibold ${levelClass[i.level].chip}`}>
                  <span className={`size-1.5 rounded-full ${levelClass[i.level].dot}`} />
                  {t(i.levelLabel)}
                </span>
                <span className="text-[11.5px] text-ink-4">{t(i.department)}</span>
              </div>
              <p className="text-[13.5px] leading-[1.45] text-ink-soft">{t(i.text)}</p>
              <Link href={i.href} className="mt-auto inline-flex items-center gap-1 text-[12.5px] font-semibold text-ink-3 transition-colors duration-150 hover:text-ink">
                {t(i.action)} <ArrowUpRight className="size-[13px]" />
              </Link>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
