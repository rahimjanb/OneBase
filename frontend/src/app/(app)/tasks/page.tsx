import { ComingSoon } from "@/components/ComingSoon";

export default function TasksPage() {
  return (
    <ComingSoon
      title="Задачи"
      subtitle="Задачи сотрудников и AI-агентов"
      planned={[
        "Список задач с ответственным, сроком, приоритетом и статусом",
        "Фильтры по статусу, отделу и ответственному",
        "Статусы «К выполнению», «В работе», «Готово», «Просрочено»",
      ]}
    />
  );
}
