"use client";

import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { RecruitingNav } from "@/components/recruitment/RecruitingNav";
import { NotificationBell } from "@/components/shell/NotificationBell";
import { useT } from "@/i18n/client";

export const RECRUITMENT_DESCRIPTION = "Подбор персонала: вакансии, кандидаты из HH и Telegram, собеседования";

/**
 * Рамка страниц «Подбора» (макеты AItTc — компьютер, xH9vJ — телефон): палитра макета и фон с двумя свечениями (data-palette, data-glow).
 * Компьютер (от 1024): крошки, «Подбор» 28/700, описание, ряд кнопок разделов с линией снизу. Телефон и планшет: своя шапка (назад, «Подбор»,
 * «HR · раздел», действие справа — или колокольчик), общая шапка приложения на этих адресах скрыта (TopbarFrame). Телефон (уже 768):
 * кнопки разделов — сеткой 3 × 3 в начале содержимого; на страницах вакансии и кандидата сетки нет — «назад» ведёт в раздел.
 * nav = false — «Заявки на сотрудника»: своя вкладка HR, без кнопок подбора.
 */
export function RecruitmentShell({
  title,
  subtitle,
  canManage,
  nav = true,
  parent,
  mobileAction,
  children,
}: {
  title: string;
  subtitle?: string;
  canManage: boolean;
  nav?: boolean;
  /** Страница внутри раздела (вакансия, кандидат): раздел в крошках и куда ведёт «назад» на телефоне. */
  parent?: { label: string; href: string };
  /** Кнопка справа в шапке телефона (например, «Новая вакансия»); нет — колокольчик уведомлений. */
  mobileAction?: React.ReactNode;
  children: React.ReactNode;
}) {
  const t = useT();
  const crumbs: [string, string | null][] = [
    ["OneBase", "/"],
    ["Отделы", "/departments"],
    ["HR", "/hr"],
    ...(nav ? ([["Подбор", "/hr/recruitment"]] as [string, string][]) : []),
    ...(parent ? ([[parent.label, parent.href]] as [string, string][]) : []),
    [title, null],
  ];
  const description = subtitle ?? (nav ? RECRUITMENT_DESCRIPTION : undefined);

  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      {/* Телефон и планшет: своя шапка. */}
      <header className="flex h-14 items-center gap-2.5 px-3 lg:hidden">
        <Link
          href={parent?.href ?? "/hr"}
          aria-label={t("Назад")}
          className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <ChevronLeft aria-hidden className="size-5" />
        </Link>
        <div className="min-w-0 flex-1 text-center">
          <h1 className="truncate text-[15px] font-bold leading-tight text-ink">{t(nav ? "Подбор" : title)}</h1>
          <p className="truncate text-[11px] font-medium text-ink-3">{nav ? `HR · ${t(title)}` : "HR"}</p>
        </div>
        {mobileAction ?? (
          <span className="grid size-10 shrink-0 place-items-center rounded-full bg-surface [&_button]:size-10">
            <NotificationBell />
          </span>
        )}
      </header>

      {/* Компьютер: крошки, название, описание. */}
      <header className="max-lg:hidden">
        <div className="mx-auto flex max-w-[1800px] flex-col gap-2 px-6 py-[18px]">
          <nav aria-label={t("Навигация")} className="flex flex-wrap gap-1.5 text-[12px] font-medium text-ink-4">
            {crumbs.map(([label, href], i) => (
              <span key={`${label}-${i}`} className="flex gap-1.5">
                {i > 0 && <span aria-hidden>/</span>}
                {href ? (
                  <Link href={href} className="hover:text-ink">
                    {t(label)}
                  </Link>
                ) : (
                  <span className="text-ink-3">{t(label)}</span>
                )}
              </span>
            ))}
          </nav>
          <h1 className="text-[28px] font-bold leading-tight tracking-[-0.4px] text-ink">{t(nav ? "Подбор" : title)}</h1>
          {description && <p className="text-[14px] font-medium text-ink-3">{t(description)}</p>}
        </div>
      </header>

      {nav && (
        <div className="border-line md:border-b">
          <div className="mx-auto max-w-[1800px] px-4 pt-1 md:pb-[18px] md:pt-0 lg:px-6">
            <RecruitingNav canManage={canManage} className={parent ? "max-md:hidden" : undefined} />
          </div>
        </div>
      )}

      <div className="mx-auto max-w-[1800px] px-4 pb-10 pt-3.5 md:pt-5 lg:px-6">{children}</div>
    </div>
  );
}
