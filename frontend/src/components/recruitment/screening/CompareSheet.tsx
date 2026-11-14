"use client";

import Link from "next/link";
import { Crown } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { when } from "@/lib/recruitment";
import type { Comparison, ComparisonCandidate } from "@/lib/screening";
import { CategoryPill, ScoreRing } from "./parts";

const ROWS: { label: string; cell: (c: ComparisonCandidate) => React.ReactNode }[] = [
  { label: "Общий рейтинг", cell: (c) => <span className="flex items-center gap-2"><ScoreRing score={c.overall} size={40} /><CategoryPill score={c.overall} /></span> },
  { label: "Опыт", cell: (c) => c.experience },
  { label: "Навыки", cell: (c) => (c.skills.length ? c.skills.join(", ") : "Не указано в резюме") },
  { label: "Достижения", cell: (c) => c.achievements },
  { label: "Образование", cell: (c) => c.education },
  { label: "Зарплатные ожидания", cell: (c) => c.salary },
  { label: "Сильные стороны", cell: (c) => <Bullets items={c.strengths} /> },
  { label: "Слабые стороны", cell: (c) => <Bullets items={c.weaknesses} /> },
  { label: "Соответствие вакансии", cell: (c) => c.fit },
];

function Bullets({ items }: { items: string[] }) {
  const t = useT();
  if (items.length === 0) return <span className="text-ink-4">{t("—")}</span>;
  return (
    <ul className="space-y-0.5">
      {items.map((x, i) => (
        <li key={i}>• {x}</li>
      ))}
    </ul>
  );
}

/**
 * Сравнение кандидатов: таблица (строки — опыт, навыки, достижения, образование, зарплата, сильные и слабые стороны, соответствие, рейтинг;
 * столбцы — кандидаты в порядке AI), рекомендованный выделен; ниже — аргументированная рекомендация и вывод по каждому.
 */
export function CompareSheet({ comparison, onClose }: { comparison: Comparison | null; onClose: () => void }) {
  const t = useT();
  if (!comparison) return null;
  const ordered = comparison.ranking.map((id) => comparison.candidates.find((c) => c.applicationId === id)).filter((c): c is ComparisonCandidate => !!c);
  const verdict = (id: string) => comparison.verdicts.find((v) => v.applicationId === id)?.verdict;
  return (
    <Sheet open onClose={onClose} title={t("Сравнение кандидатов")} subtitle={`${comparison.vacancyCode} · ${comparison.vacancyTitle} · ${when(comparison.createdAt)}`} size="xl">
      <div className="space-y-4 pb-2">
        <div className="rounded-xl bg-accent-soft px-4 py-3">
          <p className="mb-1 text-[12px] font-semibold text-accent-strong">{t("Рекомендация AI")}</p>
          <p className="text-[13px] leading-relaxed text-ink">{comparison.recommendation}</p>
        </div>
        <div className="overflow-x-auto rounded-xl border border-line">
          <table className="w-full min-w-[640px] border-collapse text-left text-[12px]">
            <thead>
              <tr className="bg-muted/50">
                <th className="w-40 px-3 py-2 font-semibold text-ink-3">{t("Критерий")}</th>
                {ordered.map((c, i) => (
                  <th key={c.applicationId} className={`px-3 py-2 align-top ${c.applicationId === comparison.bestApplicationId ? "bg-ok-soft" : ""}`}>
                    <Link href={`/hr/recruitment/candidates/${c.candidateId}`} className="font-semibold text-ink hover:underline">
                      {i + 1}. {c.fullName}
                    </Link>
                    {c.applicationId === comparison.bestApplicationId && (
                      <span className="mt-0.5 flex items-center gap-1 text-[11px] font-semibold text-ok">
                        <Crown aria-hidden className="size-3.5" />
                        {t("Рекомендован AI")}
                      </span>
                    )}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {ROWS.map((r) => (
                <tr key={r.label} className="border-t border-line align-top">
                  <th scope="row" className="px-3 py-2 font-medium text-ink-2">{t(r.label)}</th>
                  {ordered.map((c) => (
                    <td key={c.applicationId} className={`px-3 py-2 leading-relaxed text-ink ${c.applicationId === comparison.bestApplicationId ? "bg-ok-soft/40" : ""}`}>
                      {r.cell(c)}
                    </td>
                  ))}
                </tr>
              ))}
              <tr className="border-t border-line align-top">
                <th scope="row" className="px-3 py-2 font-medium text-ink-2">{t("Вывод AI")}</th>
                {ordered.map((c) => (
                  <td key={c.applicationId} className="px-3 py-2 leading-relaxed text-ink-2">
                    {verdict(c.applicationId) ?? "—"}
                  </td>
                ))}
              </tr>
            </tbody>
          </table>
        </div>
        <p className="text-[11px] text-ink-4">{t("Таблица — из анализов кандидатов; рекомендация AI — совет, решение принимает HR.")}</p>
      </div>
    </Sheet>
  );
}
