// Зеркало OneBase.AI.Agents.AgentProfiles — для отображения до входа в систему.
export type AgentProfile = {
  code: string;
  name: string;
  scope: string;
};

export const director: AgentProfile = {
  code: "director",
  name: "AI Director",
  scope: "Разбирает задачу, распределяет по отделам и собирает итог",
};

export const departmentAgents: AgentProfile[] = [
  { code: "hr", name: "HR AI", scope: "Найм, адаптация, кадровые документы, отпуска" },
  { code: "sales", name: "Sales AI", scope: "Лиды, сделки, коммерческие предложения" },
  { code: "production", name: "Production AI", scope: "Планирование, загрузка мощностей, выпуск" },
  { code: "finance", name: "Finance AI", scope: "Бюджет, платежи, счета, отчётность" },
  { code: "supply", name: "Supply AI", scope: "Закупки, поставщики, складские остатки" },
  { code: "marketing", name: "Marketing AI", scope: "Кампании, контент, аналитика каналов" },
];
