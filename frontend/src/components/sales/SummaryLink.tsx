"use client";

import Link from "next/link";
import { useT } from "@/i18n/client";

/** Ссылка-обёртка карточки-сводки (SummaryCard): «Открыть «…»» — на языке интерфейса; сама карточка остаётся общей (сервер и браузер). */
export function SummaryLink({ href, title, children }: { href: string; title: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <Link href={href} aria-label={t("Открыть «{title}»", { title: t(title) })} className="group block max-lg:active:opacity-75">
      {children}
    </Link>
  );
}
