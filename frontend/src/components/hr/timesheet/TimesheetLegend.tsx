"use client";

import { useT } from "@/i18n/client";
import { DayCodeChip } from "./DayCodeChip";

const items: [code: string, label: string][] = [
  ["Я", "Явка"],
  ["ОП", "Опоздание"],
  ["РУ", "Ранний уход"],
  ["ОТС", "Отсутствие"],
  ["РВ", "Работа в выходной"],
  ["?", "Нет отметки"],
  ["В", "Выходной"],
];

/** Легенда кодов в шапке табеля; «По документу» — только если такие дни есть в периоде (отпуск, больничный, командировка, отгул). */
export function TimesheetLegend({ documents = [] }: { documents?: string[] }) {
  const t = useT();
  return (
    <ul className="flex flex-wrap items-center gap-x-3.5 gap-y-1.5">
      {items.map(([code, label]) => (
        <li key={code} className="flex items-center gap-1.5 text-[12.5px] text-(--ts-muted)">
          <DayCodeChip code={code} legend />
          {t(label)}
        </li>
      ))}
      {documents.length > 0 && (
        <li className="flex items-center gap-1.5 text-[12.5px] text-(--ts-muted)">
          <DayCodeChip code={documents[0]} legend />
          {t("По документу")}
          {documents.length > 1 && <span className="text-(--ts-subtle)">({documents.join(", ")})</span>}
        </li>
      )}
    </ul>
  );
}
