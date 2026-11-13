"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ChevronLeft } from "lucide-react";
import SalesError from "@/app/(app)/sales/error";
import { useT } from "@/i18n/client";

/**
 * Категория не загрузилась: та же плашка, что во всём разделе («Попробовать снова»), а на телефоне и планшете — своя шапка с «назад»
 * (общая на этом адресе скрыта, TopbarFrame), чтобы со страницы ошибки можно было уйти к категориям того же периода.
 */
export default function CategoryError(props: { error: Error & { digest?: string }; reset: () => void }) {
  const t = useT();
  const params = useSearchParams();
  const query = params.toString();
  return (
    <div data-palette="sales-secondary" data-sales-own-header className="min-h-full">
      <header className="flex h-14 items-center gap-2.5 px-3 lg:hidden">
        <Link
          href={query ? `/sales/assortment?${query}` : "/sales/assortment"}
          aria-label={t("Назад")}
          className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink-2 outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <ChevronLeft aria-hidden className="size-[18px]" />
        </Link>
        <span className="min-w-0 flex-1 truncate text-center text-[15px] font-bold text-ink">{t("Ассортимент")}</span>
        <span aria-hidden className="size-10 shrink-0" />
      </header>
      <SalesError {...props} />
    </div>
  );
}
