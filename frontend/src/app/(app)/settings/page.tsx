import { ComingSoon } from "@/components/ComingSoon";

export default function SettingsPage() {
  return (
    <ComingSoon
      title="Настройки"
      subtitle="Компания, пользователи и доступы"
      planned={[
        "Пользователи, роли и права доступа",
        "Отделы и руководители",
        "Доступ AI-агентов к инструментам",
      ]}
    />
  );
}
