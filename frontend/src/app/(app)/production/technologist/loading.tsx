import { TechnologistDashboard } from "@/components/production/TechnologistDashboard";

/** Пока приходят данные — тот же экран со скелетонами: кнопки «Техкарты / Смены / Отчёты» видны сразу, счётчики в них — заготовки. */
export default function Loading() {
  return <TechnologistDashboard data={null} period={null} loading />;
}
