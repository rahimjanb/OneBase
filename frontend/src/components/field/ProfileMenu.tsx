"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { ChevronUp, LogOut } from "lucide-react";
import { FIELD_LOGOUT_PATH } from "@/lib/field/host";
import { roleLabel } from "@/lib/field/labels";
import type { FieldMe } from "@/lib/field/types";

/** Инициалы по словам, которые начинаются с буквы: «539 ТП6 Музаффар Учтепа» → «ТМ». */
function initials(name: string): string {
  const letters = name
    .split(/\s+/)
    .filter((w) => /^\p{L}/u.test(w))
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join("");
  return letters || name.trim().charAt(0).toUpperCase() || "?";
}

function Avatar({ name, className = "" }: { name: string; className?: string }) {
  return <span className={`grid shrink-0 place-items-center rounded-full bg-accent font-semibold text-white ${className}`}>{initials(name)}</span>;
}

/**
 * Профиль: по нажатию — карточка (имя, роль, команда, супервайзер) и «Выйти». header — кружок в шапке,
 * sidebar — строка внизу панели. Закрывается повторным нажатием, кликом мимо, Esc и переходом на другую страницу.
 */
export function ProfileMenu({ me, variant }: { me: FieldMe; variant: "header" | "sidebar" }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  useEffect(() => setOpen(false), [pathname]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const details = [roleLabel[me.role], me.teamName, me.supervisorName ? `супервайзер ${me.supervisorName}` : null].filter(Boolean).join(" · ");

  return (
    <div ref={ref} className="relative">
      {variant === "header" ? (
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-haspopup="menu"
          aria-expanded={open}
          aria-label={`Профиль: ${me.name}`}
          className="grid size-10 place-items-center rounded-full hover:bg-white/10 lg:size-9 lg:hover:bg-muted"
        >
          <Avatar name={me.name} className="size-8 text-xs lg:size-7 lg:text-[11px]" />
        </button>
      ) : (
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-haspopup="menu"
          aria-expanded={open}
          className="flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors hover:bg-white/5"
        >
          <Avatar name={me.name} className="size-8 text-xs" />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm text-sidebar-text">{me.name}</span>
            <span className="block truncate text-xs text-sidebar-muted">{me.teamName ?? roleLabel[me.role]}</span>
          </span>
          <ChevronUp className={`size-4 shrink-0 text-sidebar-muted transition-transform ${open ? "" : "rotate-180"}`} />
        </button>
      )}

      {open && (
        <div
          role="menu"
          className={`absolute z-50 overflow-hidden rounded-xl border border-line bg-surface text-ink shadow-xl ${
            variant === "header" ? "right-0 top-full mt-2 w-72 max-w-[calc(100vw-2rem)]" : "bottom-full left-0 mb-2 w-full"
          }`}
        >
          <div className="flex items-center gap-3 px-4 py-3.5">
            <Avatar name={me.name} className="size-10 text-sm" />
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-ink">{me.name}</div>
              <div className="text-xs leading-snug text-ink-3">{details}</div>
            </div>
          </div>
          <div className="border-t border-line p-1.5">
            <a role="menuitem" href={FIELD_LOGOUT_PATH} className="flex items-center gap-2.5 rounded-lg px-2.5 py-2.5 text-sm font-medium text-bad hover:bg-bad-soft">
              <LogOut className="size-4" strokeWidth={1.75} />
              Выйти
            </a>
          </div>
        </div>
      )}
    </div>
  );
}
