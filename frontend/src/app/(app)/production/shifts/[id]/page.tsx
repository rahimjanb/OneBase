import { notFound } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { ShiftView } from "@/components/production/ShiftView";
import { getT } from "@/i18n/server";
import type { ShiftDetail, ShiftForm } from "@/lib/production";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Смена · OneBase") };
}

/** Сменный отчёт: ввод оператора с автосохранением, закрытие, утверждение, корректировка, расчёт по результатам. */
export default async function ShiftPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/shifts/${id}`;
  await requireProduction(path);
  const shift = await apiGetOrNull<ShiftDetail>(`/api/production/shifts/${id}`, path);
  if (!shift) notFound();
  const form = shift.can.edit ? await apiTry<ShiftForm>("/api/production/shifts/form") : null;
  return <ShiftView shift={shift} people={form?.people ?? []} />;
}
