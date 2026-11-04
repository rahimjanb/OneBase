/** Обзор «Первички»: сразу каркас — итог и две карточки направлений; цифры приходят следом. */
export default function Loading() {
  const bar = "rounded-md bg-muted animate-pulse";
  return (
    <div className="mx-auto w-full max-w-[1800px] px-4 py-5 sm:px-6 lg:py-6" aria-busy="true">
      <div className={`${bar} h-4 w-48`} />
      <div className={`${bar} mt-3 h-8 w-40`} />
      <div className={`${bar} mt-2 h-4 w-80 max-w-full`} />
      <div className="mt-6 rounded-[18px] border border-line bg-surface p-5 lg:flex lg:items-center lg:gap-8 lg:px-6">
        <div className="lg:w-[340px]">
          <div className={`${bar} h-4 w-44`} />
          <div className={`${bar} mt-3 h-10 w-56`} />
          <div className={`${bar} mt-3 h-3 w-64 max-w-full`} />
        </div>
        <div className={`${bar} mt-4 h-3.5 flex-1 rounded-full lg:mt-0`} />
      </div>
      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        {[0, 1].map((i) => (
          <div key={i} className="space-y-4 rounded-[18px] border border-line bg-surface p-[22px]">
            <div className="flex items-center gap-3">
              <div className={`${bar} size-[42px] rounded-xl`} />
              <div className="flex-1 space-y-2">
                <div className={`${bar} h-4 w-32`} />
                <div className={`${bar} h-3 w-48`} />
              </div>
            </div>
            <div className={`${bar} h-9 w-40`} />
            <div className={`${bar} h-2 w-full rounded-full`} />
            <div className={`${bar} h-12 w-full`} />
            <div className={`${bar} h-11 w-full rounded-[11px]`} />
          </div>
        ))}
      </div>
    </div>
  );
}
