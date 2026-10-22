"use client";

import { useT } from "@/i18n/client";
import type { AgentFlag } from "@/lib/sales/types";

/** Пилюля флага агента: подпись и подсказка (заголовок флага) приходят с сервера — переводятся по словарю. */
export function FlagPill({ flag, className }: { flag: AgentFlag; className: string }) {
  const t = useT();
  return (
    <span title={t(flag.title)} className={className}>
      <span className="size-1.5 rounded-full bg-current" />
      {t(flag.label)}
    </span>
  );
}
