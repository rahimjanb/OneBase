/** Загрузка страницы Sales Base: скелет вместо пустого экрана (aria-busy — по нему и «Назад» ждёт данных, а не скелет). */
export default function Loading() {
  return (
    <div className="mx-auto w-full max-w-[1400px] animate-pulse px-4 pt-4 sm:px-6 lg:pt-6" aria-busy="true" aria-label="Загрузка">
      <div className="h-7 w-48 rounded-lg bg-muted" />
      <div className="mt-2 h-4 w-72 rounded bg-muted" />
      <div className="mt-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <div key={i} className="h-24 rounded-xl bg-muted" />
        ))}
      </div>
      <div className="mt-4 h-64 rounded-xl bg-muted" />
    </div>
  );
}
