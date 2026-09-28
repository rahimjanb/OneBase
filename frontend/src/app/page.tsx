import { departmentAgents, director } from "@/lib/agents";

const nav = [
  { label: "Обзор", active: true },
  { label: "AI-сотрудники" },
  { label: "Файлы" },
  { label: "Подтверждения" },
  { label: "Журнал аудита" },
  { label: "Настройки" },
];

const principles = [
  { title: "Tool Registry", text: "Агенты действуют только через зарегистрированные инструменты — не напрямую через базу." },
  { title: "Права доступа", text: "У каждого агента свой набор разрешённых инструментов, у людей — роли и права на отделы и папки." },
  { title: "Human Approval", text: "Критические действия ждут подтверждения человеком перед выполнением." },
  { title: "Аудит", text: "Каждый вызов, отказ и решение записывается в журнал." },
];

export default function Home() {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-60 shrink-0 border-r border-zinc-200 bg-white px-4 py-6 md:block dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mb-8 flex items-center gap-2 px-2">
          <div className="grid size-8 place-items-center rounded-lg bg-indigo-600 text-sm font-bold text-white">1B</div>
          <span className="text-lg font-semibold">OneBase</span>
        </div>
        <nav className="space-y-1">
          {nav.map((item) => (
            <a
              key={item.label}
              href="#"
              className={
                item.active
                  ? "block rounded-lg bg-indigo-50 px-3 py-2 text-sm font-medium text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300"
                  : "block rounded-lg px-3 py-2 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-zinc-800"
              }
            >
              {item.label}
            </a>
          ))}
        </nav>
      </aside>

      <main className="flex-1 px-4 py-8 sm:px-8">
        <header className="mb-8">
          <h1 className="text-2xl font-semibold tracking-tight">AI-команда компании</h1>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            Поставьте задачу AI Director — он распределит её между отделами.
          </p>
        </header>

        <section className="mb-6 rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50 to-white p-6 dark:border-indigo-500/30 dark:from-indigo-500/10 dark:to-zinc-900">
          <div className="text-xs font-medium uppercase tracking-wide text-indigo-600 dark:text-indigo-300">Руководитель</div>
          <div className="mt-1 text-lg font-semibold">{director.name}</div>
          <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{director.scope}</p>
        </section>

        <section className="mb-10 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {departmentAgents.map((agent) => (
            <div
              key={agent.code}
              className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <div className="flex items-center justify-between">
                <span className="font-medium">{agent.name}</span>
                <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
                  {agent.code}
                </span>
              </div>
              <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">{agent.scope}</p>
            </div>
          ))}
        </section>

        <section>
          <h2 className="mb-4 text-lg font-semibold">Как AI работает с данными</h2>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {principles.map((p) => (
              <div key={p.title} className="rounded-2xl border border-zinc-200 p-5 dark:border-zinc-800">
                <div className="font-medium">{p.title}</div>
                <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{p.text}</p>
              </div>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}
