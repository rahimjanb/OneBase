"use client";

import { useState } from "react";
import { GitCompareArrows, Lightbulb, Package, Sparkles, Target, TrendingDown, TrendingUp, UserRoundX, Wallet, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import type { QuickQuestion } from "@/lib/consultant-view";

const icons: Record<QuickQuestion["icon"], LucideIcon> = {
  "trending-up": TrendingUp,
  target: Target,
  "trending-down": TrendingDown,
  compare: GitCompareArrows,
  package: Package,
  "user-x": UserRoundX,
  wallet: Wallet,
  lightbulb: Lightbulb,
};

/** Цвет плитки значка: success, danger, warning, accent, violet, бирюзовый (#2DD4BF) — по ТЗ. */
const tones: Record<string, string> = {
  success: "bg-ok-soft text-ok",
  danger: "bg-bad-soft text-bad",
  warning: "bg-warn-soft text-warn",
  accent: "bg-accent-soft text-link",
  violet: "bg-violet-soft text-violet",
  teal: "bg-[#2dd4bf]/15 text-teal",
};

/** Стартовый экран пустого чата: значок, «Чем помочь бизнесу?», подзаголовок и быстрые вопросы (2×3 на телефоне, 3×2 на компьютере). */
export function StartScreen({ questions, disabled, onAsk }: { questions: QuickQuestion[]; disabled: boolean; onAsk: (text: string) => void }) {
  const t = useT();
  const [more, setMore] = useState(false);
  const shown = more ? questions : questions.slice(0, 6);
  return (
    <div className="relative mx-auto w-full max-w-[840px] pb-4 pt-6 text-center max-lg:pt-4">
      {/* Мягкое фиолетовое свечение в верхней трети. */}
      <div aria-hidden className="pointer-events-none absolute inset-x-0 -top-10 h-72" style={{ background: "radial-gradient(60% 60% at 50% 30%, var(--ai-violet-glow), transparent 70%)" }} />
      <div className="relative">
        <div className="ai-gradient mx-auto grid size-[54px] place-items-center rounded-[17px] text-white shadow-[0_10px_32px_rgb(109_92_255/0.45)]">
          <Sparkles className="size-6" aria-hidden />
        </div>
        <h2 className="mt-5 text-[22px] font-bold tracking-tight text-ink">{t("Чем помочь бизнесу?")}</h2>
        <p className="mx-auto mt-2 max-w-[420px] text-sm leading-relaxed text-ink-3">{t("Спросите обычным языком — отвечу по данным OneBase и отделю факты от рекомендаций.")}</p>

        {questions.length > 0 && (
          <div className="mt-7 text-left">
            <div className="mb-3 flex items-center justify-between px-1">
              <h3 className="text-[15px] font-bold text-ink">{t("Быстрые вопросы")}</h3>
              {questions.length > 6 && (
                <button type="button" onClick={() => setMore((v) => !v)} aria-expanded={more} className="text-[13px] font-semibold text-link">
                  {more ? t("Свернуть") : t("Ещё")}
                </button>
              )}
            </div>
            <ul className="grid grid-cols-2 gap-2.5 lg:grid-cols-3 lg:gap-3">
              {shown.map((q) => {
                const Icon = icons[q.icon];
                return (
                  <li key={q.text}>
                    <button
                      type="button"
                      disabled={disabled}
                      onClick={() => onAsk(t(q.text))}
                      className="flex h-full w-full flex-col items-start gap-3 rounded-2xl border border-line bg-surface p-3 text-left transition-colors hover:border-accent/50 disabled:opacity-50 lg:flex-row lg:items-center"
                    >
                      <span className={`grid size-7 shrink-0 place-items-center rounded-lg ${tones[q.tone] ?? tones.accent}`}>
                        <Icon className="size-4" aria-hidden />
                      </span>
                      <span className="line-clamp-2 text-[13px] font-medium leading-snug text-ink">{t(q.text)}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}
