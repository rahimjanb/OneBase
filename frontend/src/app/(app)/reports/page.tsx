import { ComingSoon } from "@/components/ComingSoon";

export default function ReportsPage() {
  return (
    <ComingSoon
      title="Отчёты"
      subtitle="Отчёты по отделам, финансам и сотрудникам"
      planned={[
        "Каталог отчётов по продажам, финансам, отделам и сотрудникам",
        "Фильтры по периоду и подразделению",
        "Предпросмотр и экспорт",
      ]}
    />
  );
}
