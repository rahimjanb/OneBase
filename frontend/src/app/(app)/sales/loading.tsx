function Block({ className }: { className: string }) {
  return <div className={`animate-pulse rounded-xl bg-muted ${className}`} />;
}

/** Скелетон, пока сервер считает уровень. */
export default function SalesLoading() {
  return (
    <div className="mx-auto max-w-[1240px] px-4 py-6 sm:px-8" aria-busy="true" aria-label="Загрузка">
      <Block className="h-8 w-56" />
      <Block className="mt-3 h-4 w-80" />
      <div className="mt-8 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
        {Array.from({ length: 6 }, (_, i) => (
          <Block key={i} className="h-28" />
        ))}
      </div>
      <Block className="mt-6 h-64" />
      <Block className="mt-6 h-64" />
    </div>
  );
}
