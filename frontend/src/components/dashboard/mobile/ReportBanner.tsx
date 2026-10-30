"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ArrowRight, FileChartColumn } from "lucide-react";
import { useT } from "@/i18n/client";
import type { WeeklyReport } from "@/lib/dashboard-mobile";
import { plural } from "@/lib/format";
import m from "./mobile.module.css";
import { focusRing, press } from "./ui";

const READ_KEY = "onebase-ai-report-read";

/**
 * «Еженедельный AI-отчёт»: риски и рекомендации консультанта за неделю. Виден, пока отчёт этой недели не открыт; открыли — скрыт до
 * следующей недели (отметка — на этом устройстве). Пока не прочитано хранилище, баннера нет: не мигает у тех, кто уже открыл.
 */
export function ReportBanner({ report }: { report: WeeklyReport | null }) {
  const t = useT();
  const [unread, setUnread] = useState(false);

  useEffect(() => {
    if (!report) return;
    try {
      setUnread(localStorage.getItem(READ_KEY) !== report.id);
    } catch {
      setUnread(true);
    }
  }, [report]);

  if (!report || !unread) return null;
  const markRead = () => {
    try {
      localStorage.setItem(READ_KEY, report.id);
    } catch {
      // хранилище недоступно — баннер покажется снова
    }
  };

  return (
    <Link href="/ai" onClick={markRead} className={`flex items-center gap-[12px] rounded-[20px] border p-[16px] ${m.report} ${press} ${focusRing}`}>
      <span className="grid size-[44px] shrink-0 place-items-center rounded-[12px] bg-(--mh-tile) text-(--ts-text)">
        <FileChartColumn aria-hidden className="size-[20px]" strokeWidth={2} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-[14px] font-bold text-(--ts-text)">{t("Еженедельный AI-отчёт")}</span>
        <span className="mt-[2px] block text-[12px] leading-[1.35] text-(--ts-text-2)">
          {t("{risks} и {recs} по отделам", {
            risks: `${report.risks} ${t(plural(report.risks, ["риск", "риска", "рисков"]))}`,
            recs: `${report.recommendations} ${t(plural(report.recommendations, ["рекомендация", "рекомендации", "рекомендаций"]))}`,
          })}
        </span>
      </span>
      <span aria-hidden className="grid size-[36px] shrink-0 place-items-center rounded-full bg-(--mh-arrow-bg) text-(--mh-arrow)">
        <ArrowRight className="size-[18px]" strokeWidth={2.2} />
      </span>
    </Link>
  );
}
