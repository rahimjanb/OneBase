"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { FileChartColumn, FileDown, FileSpreadsheet, FileText, Loader2, Plus, Sparkles, type LucideIcon } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { startNavProgress } from "@/components/shell/NavProgress";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { fmt, PERIODS, type HomeFilters, type MobileDepartment, type MobileKpi, type MobileTasks } from "@/lib/dashboard-mobile";
import type { DepartmentTasks, WorkProject } from "@/lib/tasks";
import { useHomeFilters } from "./Filters";
import { focusRing, press } from "./ui";

// Форма задачи — тяжёлая, грузится по первому нажатию «Задача» (вместе со списком отделов).
const loadTaskForm = () => import("@/components/tasks/TaskForm").then((mod) => mod.TaskForm);
const TaskForm = dynamic(loadTaskForm, { ssr: false });

const tile = `flex min-h-[44px] flex-col items-center gap-[8px] rounded-[16px] pb-[12px] pt-[14px] text-[12px] font-semibold leading-tight ${press} ${focusRing}`;
const plain = `${tile} border border-(--ts-border) bg-(--ts-surface) text-(--ts-text)`;
const circle = "grid size-[36px] place-items-center rounded-full";

/** Период фильтра словами — для вопроса консультанту и файла выгрузки. */
export function periodText(f: HomeFilters, t: (s: string) => string) {
  if (f.period === "custom") return `${f.from ?? "…"} – ${f.to ?? "…"}`;
  return t(PERIODS.find((p) => p.key === f.period)?.label ?? "30 дней");
}

/**
 * Быстрые действия — четыре кнопки в ряд: «Задача» (создание задачи прямо здесь), «Спросить AI» (консультант с вопросом по текущим
 * фильтрам), «Экспорт» (PDF или таблица) и «Отчёт AI» (находки консультанта). Нажатие видно сразу: кнопка сжимается, а «Задача», пока
 * грузит отделы для формы, показывает вращающийся значок.
 */
export function QuickActions({ kpi, tasks, departments }: { kpi: MobileKpi; tasks: MobileTasks; departments: MobileDepartment[] }) {
  const t = useT();
  const router = useRouter();
  const [filters] = useHomeFilters();
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState<{ departments: DepartmentTasks[]; projects: WorkProject[] } | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);

  const openTasks = () => {
    startNavProgress();
    router.push("/tasks");
  };

  const newTask = async () => {
    if (loading) return;
    if (form) return setFormOpen(true);
    setLoading(true);
    try {
      const [list, projects] = await Promise.all([
        bff<DepartmentTasks[]>("work/departments"),
        bff<WorkProject[]>("work/projects").catch(() => [] as WorkProject[]),
        loadTaskForm(),
      ]);
      // Ставить задачи негде (нет отделов с задачами и проектов) — раздел «Задачи» сам объяснит.
      if (!list.some((d) => d.hasTasks) && projects.length === 0) return openTasks();
      setForm({ departments: list, projects });
      setFormOpen(true);
    } catch {
      openTasks();
    } finally {
      setLoading(false);
    }
  };

  const scope = filters.departments.length
    ? ` (${filters.departments.map((code) => t(departments.find((d) => d.code === code)?.name ?? code)).join(", ")})`
    : "";
  const question = t("Разбери показатели компании за период «{period}»{scope}: что сейчас требует внимания и что сделать в первую очередь?", {
    period: periodText(filters, t),
    scope,
  });

  return (
    <>
      <div className="grid grid-cols-4 gap-[8px]">
        <button type="button" onClick={newTask} aria-busy={loading} className={`${tile} bg-(--ts-accent) text-white`}>
          <span className={`${circle} bg-[#ffffff26]`}>
            {loading ? <Loader2 aria-hidden className="size-[18px] animate-spin" /> : <Plus aria-hidden className="size-[18px]" strokeWidth={2.4} />}
          </span>
          {t("Задача")}
        </button>
        <Link href={`/consultant?ask=${encodeURIComponent(question)}`} className={plain}>
          <span className={`${circle} bg-(--ts-surface-2) text-(--ts-violet)`}>
            <Sparkles aria-hidden className="size-[18px]" strokeWidth={2} />
          </span>
          {t("Спросить AI")}
        </Link>
        <button type="button" onClick={() => setExportOpen(true)} aria-haspopup="dialog" className={plain}>
          <span className={`${circle} bg-(--ts-surface-2) text-(--ts-text-2)`}>
            <FileDown aria-hidden className="size-[18px]" strokeWidth={2} />
          </span>
          {t("Экспорт")}
        </button>
        <Link href="/ai" className={plain}>
          <span className={`${circle} bg-(--ts-surface-2) text-(--ts-text-2)`}>
            <FileChartColumn aria-hidden className="size-[18px]" strokeWidth={2} />
          </span>
          {t("Отчёт AI")}
        </Link>
      </div>

      {form && <TaskForm open={formOpen} onClose={() => setFormOpen(false)} departments={form.departments} projects={form.projects} />}

      <Sheet open={exportOpen} onClose={() => setExportOpen(false)} title={t("Экспорт обзора")} subtitle={periodText(filters, t)}>
        <div className="flex flex-col gap-2">
          <ExportOption
            icon={FileText}
            title={t("PDF")}
            hint={t("Печать или сохранение в PDF")}
            onClick={() => {
              setExportOpen(false);
              // Шторка успевает закрыться — в PDF попадает страница без неё.
              window.setTimeout(() => window.print(), 300);
            }}
          />
          <ExportOption
            icon={FileSpreadsheet}
            title={t("Excel (CSV)")}
            hint={t("Показатели, задачи и отделы таблицей")}
            onClick={() => {
              download(csvOf(kpi, tasks, departments, periodText(filters, t), t));
              setExportOpen(false);
            }}
          />
        </div>
      </Sheet>
    </>
  );
}

function ExportOption({ icon: Icon, title, hint, onClick }: { icon: LucideIcon; title: string; hint: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className={`flex min-h-[56px] w-full items-center gap-3 rounded-[14px] border border-line px-3 py-2.5 text-left ${press} hover:bg-muted`}>
      <span className="grid size-10 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
        <Icon aria-hidden className="size-5" />
      </span>
      <span className="min-w-0">
        <span className="block text-[15px] font-semibold text-ink">{title}</span>
        <span className="block text-xs text-ink-3">{hint}</span>
      </span>
    </button>
  );
}

/** Таблица для Excel: разделитель «;» и запятая в дробях — как ждёт русский Excel; BOM — чтобы кириллица открылась без настроек. */
function csvOf(kpi: MobileKpi, tasks: MobileTasks, departments: MobileDepartment[], period: string, t: (s: string, v?: Record<string, string | number>) => string) {
  const cell = (v: string | number) => {
    const s = typeof v === "number" ? fmt(v, Number.isInteger(v) ? 0 : 1) : v;
    return /[;"\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const rows: (string | number)[][] = [
    [t("Обзор OneBase"), period],
    [],
    [t("Показатель"), t("Значение")],
    [t("Выполнение планов, %"), kpi.plan.value],
    [t("Цель, %"), kpi.plan.target],
    [t("Общий KPI, %"), kpi.kpi.value],
    [t("Активные задачи"), kpi.tasks.active],
    [t("Просрочено"), kpi.tasks.overdue],
    [],
    [t("Задачи"), t("Количество")],
    ...tasks.slices.map((s) => [t(s.label), s.count]),
    [],
    [t("Отдел"), t("Ключевая метрика"), t("Значение, %"), t("За неделю"), t("Статус")],
    ...departments.map((d) => [t(d.name), t(d.metric), d.value, d.trendWeek, d.status === "attention" ? t("Внимание") : t("В норме")]),
  ];
  return rows.map((r) => r.map(cell).join(";")).join("\r\n");
}

function download(csv: string) {
  const url = URL.createObjectURL(new Blob(["﻿", csv], { type: "text/csv;charset=utf-8" }));
  const a = document.createElement("a");
  a.href = url;
  a.download = `onebase-obzor-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.append(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
