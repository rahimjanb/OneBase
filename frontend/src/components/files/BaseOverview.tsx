"use client";

import { useState } from "react";
import { Clock, Files } from "lucide-react";
import { DepartmentWorkspaceCard } from "@/components/departments";
import { Button, EmptyState, SearchInput, SectionTitle } from "@/components/ui";
import type { Department } from "@/lib/demo-data";

export function BaseOverview({ departments }: { departments: Department[] }) {
  const [query, setQuery] = useState("");
  const q = query.trim().toLowerCase();
  const visible = departments.filter((d) => d.name.toLowerCase().includes(q));

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <SearchInput
          className="min-w-[240px] flex-1 sm:max-w-xl"
          placeholder="Поиск по файлам, папкам и отделам"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <div className="flex gap-2 sm:ml-auto">
          <Button>
            <Clock className="size-4" />
            Недавние
          </Button>
          <Button>
            <Files className="size-4" />
            Мои файлы
          </Button>
        </div>
      </div>

      <div className="mt-8">
        <SectionTitle title="Отделы" subtitle="Документы сгруппированы по рабочим пространствам" />
      </div>
      <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {visible.map((d) => (
          <DepartmentWorkspaceCard key={d.code} department={d} />
        ))}
      </div>
      {visible.length === 0 && <EmptyState>Ничего не найдено</EmptyState>}
    </>
  );
}
