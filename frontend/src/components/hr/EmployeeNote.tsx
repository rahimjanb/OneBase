"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Loader2 } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";

/** Заметка HR — локальное поле OneBase: синхронизация с Verifix её не меняет. Правит только право «HR». */
export function EmployeeNote({ id, note, canEdit }: { id: string; note: string | null; canEdit: boolean }) {
  const t = useT();
  const router = useRouter();
  const [text, setText] = useState(note ?? "");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  if (!canEdit) return <p className="whitespace-pre-wrap text-sm text-ink-2">{note || t("Заметок нет.")}</p>;

  const save = async () => {
    setBusy(true);
    setMessage(null);
    try {
      await bff(`hr/employees/${id}/note`, { method: "PUT", body: JSON.stringify({ note: text }) });
      setMessage({ ok: true, text: t("Сохранено.") });
      router.refresh();
    } catch (e) {
      setMessage({ ok: false, text: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        maxLength={2000}
        rows={3}
        placeholder={t("Например: выдан пропуск, работает по индивидуальному графику")}
        className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
      />
      <div className="mt-2 flex items-center gap-3">
        <button
          type="button"
          onClick={save}
          disabled={busy || text === (note ?? "")}
          className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
        >
          {busy && <Loader2 className="size-4 animate-spin" />} {t("Сохранить заметку")}
        </button>
        {message && <span className={`text-sm ${message.ok ? "text-ok" : "text-bad"}`}>{message.text}</span>}
      </div>
    </div>
  );
}
