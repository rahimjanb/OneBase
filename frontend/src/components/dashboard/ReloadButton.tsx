"use client";

/** Кнопка «Обновить» в состоянии ошибки блока: перечитывает страницу (данные приходят с сервера). */
export function ReloadButton({ className, children }: { className?: string; children: React.ReactNode }) {
  return (
    <button type="button" className={className} onClick={() => window.location.reload()}>
      {children}
    </button>
  );
}
