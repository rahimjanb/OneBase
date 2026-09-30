"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { FileText, Loader2, RefreshCw, Trash2, Upload } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { permissionLabels, type KnowledgeDocumentView, type KnowledgeList } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button, field, primary } from "./form";

function size(bytes: number) {
  return bytes < 1024 * 1024 ? `${num(Math.ceil(bytes / 1024))} КБ` : `${num(bytes / 1024 / 1024, 1)} МБ`;
}

export function KnowledgeSettings({ initial, embeddingModel }: { initial: KnowledgeList; embeddingModel: string | null }) {
  const [docs, setDocs] = useState(initial.documents);
  const [title, setTitle] = useState("");
  const [department, setDepartment] = useState("");
  const [permission, setPermission] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const input = useRef<HTMLInputElement>(null);

  const departmentName = (code: string | null) => (code ? initial.departments.find((d) => d.code === code)?.name ?? code : "Вся компания");

  const upload = async () => {
    const file = input.current?.files?.[0];
    if (!file) {
      setMessage({ tone: "bad", text: "Выберите файл." });
      return;
    }
    if (file.size > initial.maxBytes) {
      setMessage({ tone: "bad", text: `Файл больше ${num(initial.maxBytes / 1024 / 1024)} МБ.` });
      return;
    }
    const form = new FormData();
    form.append("file", file);
    if (title.trim()) form.append("title", title.trim());
    if (department) form.append("departmentCode", department);
    if (permission) form.append("requiredPermission", permission);
    setBusy("upload");
    setMessage(null);
    try {
      // multipart — без Content-Type: браузер сам ставит границу.
      const response = await fetch("/bff/api/ai/knowledge/documents", { method: "POST", body: form });
      const json = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(json.error ?? json.detail ?? json.title ?? `Ошибка ${response.status}`);
      const doc = json as KnowledgeDocumentView;
      setDocs((prev) => [doc, ...prev]);
      setTitle("");
      if (input.current) input.current.value = "";
      setMessage({
        tone: doc.error ? "bad" : "ok",
        text: `«${doc.title}» загружен: ${num(doc.chunks)} фрагм.${doc.error ? ` ${doc.error}` : doc.embeddingModel ? ", семантический поиск готов." : ", работает полнотекстовый поиск."}`,
      });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const archive = async (doc: KnowledgeDocumentView) => {
    if (!confirm(`Убрать «${doc.title}» из базы знаний? AI перестанет его находить; файл сохранится в хранилище.`)) return;
    setBusy(doc.id);
    try {
      await bff(`ai/knowledge/documents/${doc.id}`, { method: "DELETE" });
      setDocs((prev) => prev.filter((d) => d.id !== doc.id));
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const changeAccess = async (doc: KnowledgeDocumentView, patch: Partial<Pick<KnowledgeDocumentView, "departmentCode" | "requiredPermission">>) => {
    setBusy(doc.id);
    try {
      const next = { ...doc, ...patch };
      const saved = await bff<KnowledgeDocumentView>(`ai/knowledge/documents/${doc.id}`, {
        method: "PUT",
        body: JSON.stringify({ title: next.title, departmentCode: next.departmentCode, requiredPermission: next.requiredPermission }),
      });
      setDocs((prev) => prev.map((d) => (d.id === doc.id ? saved : d)));
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const reindex = async () => {
    setBusy("reindex");
    setMessage(null);
    try {
      const result = await bff<{ count: number }>("ai/knowledge/reindex", { method: "POST" });
      setDocs((await bff<KnowledgeList>("ai/knowledge/documents")).documents);
      setMessage({ tone: "ok", text: `Эмбеддинги построены для документов: ${result.count}.` });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const pending = docs.filter((d) => d.embeddingModel !== embeddingModel).length;

  return (
    <>
      {message && (
        <div className={`mb-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title="Загрузить документ" hint={`${initial.extensions.join(", ")} · до ${num(initial.maxBytes / 1024 / 1024)} МБ`}>
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Файл</span>
            <input
              ref={input}
              type="file"
              accept={initial.extensions.join(",")}
              className="block w-full text-sm text-ink-2 file:mr-3 file:h-10 file:rounded-lg file:border file:border-line file:bg-surface file:px-4 file:text-sm file:font-medium file:text-ink hover:file:bg-muted"
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Название</span>
            <input className={field} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="по имени файла" maxLength={300} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Кто видит</span>
            <select className={field} value={department} onChange={(e) => setDepartment(e.target.value)}>
              <option value="">Вся компания</option>
              {initial.departments.map((d) => (
                <option key={d.code} value={d.code}>
                  Отдел «{d.name}»
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Дополнительное право</span>
            <select className={field} value={permission} onChange={(e) => setPermission(e.target.value)}>
              <option value="">Не требуется</option>
              {initial.permissions.map((p) => (
                <option key={p} value={p}>
                  {permissionLabels[p] ?? p}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="mt-5">
          <button className={primary} onClick={upload} disabled={busy !== null}>
            {busy === "upload" ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
            Загрузить
          </button>
        </div>
      </Section>

      <Section
        title="Документы"
        hint={`${docs.length} в базе знаний`}
        actions={
          embeddingModel && pending > 0 ? (
            <button className={button} onClick={reindex} disabled={busy !== null}>
              <RefreshCw className={`size-4 ${busy === "reindex" ? "animate-spin" : ""}`} />
              Построить эмбеддинги ({pending})
            </button>
          ) : undefined
        }
      >
        {docs.length === 0 ? (
          <p className="text-sm text-ink-2">Документов пока нет. AI-сотрудники ищут в базе знаний регламенты, отчёты и выгрузки, которые вы загрузите.</p>
        ) : (
          <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
            <table className="w-full min-w-max text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-2 pr-3 font-semibold">Документ</th>
                  <th className="py-2 pr-3 font-semibold">Кто видит</th>
                  <th className="py-2 pr-3 font-semibold">Право</th>
                  <th className="py-2 pr-3 font-semibold">Поиск</th>
                  <th className="py-2 pr-3 font-semibold">Загружен</th>
                  <th className="py-2 font-semibold" />
                </tr>
              </thead>
              <tbody>
                {docs.map((d) => (
                  <tr key={d.id} className="border-b border-line align-top last:border-0">
                    <td className="py-2 pr-3">
                      <Link href={`/knowledge/${d.id}`} className="flex items-center gap-2 font-medium text-ink hover:text-accent-strong">
                        <FileText className="size-4 shrink-0 text-ink-3" />
                        {d.title}
                      </Link>
                      <div className="pl-6 text-xs text-ink-3">
                        {d.fileName} · {size(d.sizeBytes)} · {num(d.chunks)} фрагм.
                      </div>
                      {d.error && <div className="pl-6 text-xs text-warn">{d.error}</div>}
                    </td>
                    <td className="py-2 pr-3">
                      <select
                        className="h-8 rounded-md border border-line bg-surface px-2 text-sm"
                        value={d.departmentCode ?? ""}
                        disabled={busy !== null}
                        onChange={(e) => changeAccess(d, { departmentCode: e.target.value || null })}
                        aria-label="Кто видит"
                      >
                        <option value="">Вся компания</option>
                        {initial.departments.map((x) => (
                          <option key={x.code} value={x.code}>
                            {x.name}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="py-2 pr-3">
                      <select
                        className="h-8 rounded-md border border-line bg-surface px-2 text-sm"
                        value={d.requiredPermission ?? ""}
                        disabled={busy !== null}
                        onChange={(e) => changeAccess(d, { requiredPermission: e.target.value || null })}
                        aria-label="Дополнительное право"
                      >
                        <option value="">—</option>
                        {initial.permissions.map((p) => (
                          <option key={p} value={p}>
                            {permissionLabels[p] ?? p}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="py-2 pr-3 text-xs text-ink-2">{d.embeddingModel ? "текст + смысл" : "по тексту"}</td>
                    <td className="py-2 pr-3 text-xs tabular-nums text-ink-2">{dateTime(d.createdAt)}</td>
                    <td className="py-2 text-right">
                      <button
                        type="button"
                        onClick={() => archive(d)}
                        disabled={busy !== null}
                        aria-label={`Убрать ${d.title}`}
                        className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-bad-soft hover:text-bad"
                      >
                        {busy === d.id ? <Loader2 className="size-4 animate-spin" /> : <Trash2 className="size-4" />}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="mt-3 text-xs text-ink-3">«{departmentName(null)}» видят все пользователи с правом «Просмотр файлов»; документ отдела — только сотрудники этого отдела.</p>
      </Section>

      <Note>
        Документ хранится на сервере OneBase и разбивается на фрагменты. Поиск по тексту работает всегда; поиск по смыслу — если в «Общих» выбрана модель
        эмбеддингов (тогда фрагменты отправляются провайдеру этой модели). PDF и старые форматы Office пока не читаются — сохраните их как .docx, .xlsx
        или .txt.
      </Note>
    </>
  );
}
