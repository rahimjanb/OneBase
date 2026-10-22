import { ComingSoon } from "@/components/ComingSoon";
import { getT } from "@/i18n/server";

export default async function ReportsPage() {
  const t = await getT();
  return (
    <ComingSoon
      title={t("Отчёты")}
      subtitle={t("Отчёты по отделам, финансам и сотрудникам")}
      planned={[
        t("Каталог отчётов по продажам, финансам, отделам и сотрудникам"),
        t("Фильтры по периоду и подразделению"),
        t("Предпросмотр и экспорт"),
      ]}
    />
  );
}
