"use client";

import { useState } from "react";
import { Loader2, Play, RotateCcw } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { modelLabel, type AiAgentSettings, type AiModelView, type AiProviderView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import type { AgentResult } from "@/lib/consultant";
import { dateTime, num } from "@/lib/sales/format";
import { AgentCard } from "./Consultant";
import { button, field, primary } from "./form";

type ToolState = { enabled: boolean; requiresApproval: boolean };

export function AgentSettings({ initial, models, providers }: { initial: AiAgentSettings; models: AiModelView[]; providers: AiProviderView[] }) {
  const [data, setData] = useState(initial);
  const [name, setName] = useState(initial.name);
  const [role, setRole] = useState(initial.role ?? "");
  const [description, setDescription] = useState(initial.description ?? "");
  const [prompt, setPrompt] = useState(initial.systemPrompt);
  const [model, setModel] = useState(initial.model ? `${initial.model.provider}|${initial.model.model}` : "");
  const [temperature, setTemperature] = useState(initial.temperature?.toString() ?? "");
  const [maxTokens, setMaxTokens] = useState(initial.maxOutputTokens?.toString() ?? "");
  const [enabled, setEnabled] = useState(initial.enabled);
  const [permission, setPermission] = useState(initial.requiredPermission ?? "");
  const [sources, setSources] = useState<string[]>(initial.sources.filter((s) => s.enabled).map((s) => s.code));
  const [tools, setTools] = useState<Record<string, ToolState>>(Object.fromEntries(initial.tools.map((t) => [t.name, { enabled: t.enabled, requiresApproval: t.requiresApproval }])));
  const [busy, setBusy] = useState<"save" | "run" | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const [task, setTask] = useState("");
  const [run, setRun] = useState<{ result: AgentResult; usage: { inputTokens: number; outputTokens: number }; model: string | null } | null>(null);

  const providerName = (code: string) => providers.find((p) => p.code === code)?.name ?? code;
  const enabledModels = models.filter((m) => m.enabled);

  const save = async () => {
    const t = temperature.trim().replace(",", ".");
    const [provider, ...rest] = model.split("|");
    setBusy("save");
    setMessage(null);
    try {
      const saved = await bff<AiAgentSettings>(`ai/agents/${data.code}`, {
        method: "PUT",
        body: JSON.stringify({
          name,
          role: role || null,
          description: description || null,
          systemPrompt: prompt,
          model: model ? { provider, model: rest.join("|") } : null,
          temperature: t === "" ? null : Number(t),
          maxOutputTokens: maxTokens.trim() === "" ? null : Number(maxTokens),
          enabled,
          requiredPermission: permission || null,
          sources,
          tools: Object.entries(tools).map(([n, s]) => ({ name: n, ...s })),
        }),
      });
      setData(saved);
      setPrompt(saved.systemPrompt);
      setMessage({ tone: "ok", text: "Настройки AI-сотрудника сохранены." });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const execute = async () => {
    if (!task.trim()) return;
    setBusy("run");
    setRun(null);
    setMessage(null);
    try {
      setRun(await bff(`ai/agents/${data.code}/execute`, { method: "POST", body: JSON.stringify({ task }) }));
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const toggleSource = (code: string, on: boolean) => setSources((prev) => (on ? [...prev, code] : prev.filter((s) => s !== code)));
  const sourceName = (code: string) => data.sources.find((s) => s.code === code)?.name ?? code;
  const permissionLabel = (code: string | null) => (code ? data.permissions.find((p) => p.code === code)?.label ?? code : null);

  return (
    <>
      {message && (
        <div className={`mb-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title="Основное" hint={data.updatedAt ? `изменено ${dateTime(data.updatedAt)}` : undefined}>
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Название</span>
            <input className={field} value={name} onChange={(e) => setName(e.target.value)} maxLength={100} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Роль</span>
            <input className={field} value={role} onChange={(e) => setRole(e.target.value)} maxLength={200} />
          </label>
          <label className="block lg:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-ink">Описание</span>
            <input className={field} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={1000} />
          </label>
          {!data.isConsultant && (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">Кому доступен</span>
              <select className={field} value={permission} onChange={(e) => setPermission(e.target.value)}>
                <option value="">Всем, кто пользуется консультантом</option>
                {data.permissions.map((p) => (
                  <option key={p.code} value={p.code}>
                    Только с правом «{p.label}»
                  </option>
                ))}
              </select>
              <span className="mt-1 block text-xs text-ink-3">Без этого права консультант не привлекает сотрудника к вопросу пользователя.</span>
            </label>
          )}
        </div>
        <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          {data.isConsultant ? "Консультант включён" : "AI-сотрудник включён"}
        </label>
      </Section>

      <Section title="Модель">
        <div className="grid gap-5 lg:grid-cols-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Модель</span>
            <select className={field} value={model} onChange={(e) => setModel(e.target.value)}>
              <option value="">Основная из общих настроек</option>
              {providers.map((p) => {
                const list = enabledModels.filter((m) => m.provider === p.code);
                return list.length === 0 ? null : (
                  <optgroup key={p.code} label={p.name}>
                    {list.map((m) => (
                      <option key={m.id} value={`${m.provider}|${m.model}`}>
                        {modelLabel(m)}
                      </option>
                    ))}
                  </optgroup>
                );
              })}
              {data.model && !enabledModels.some((m) => m.provider === data.model!.provider && m.model === data.model!.model) && (
                <option value={`${data.model.provider}|${data.model.model}`}>{`${providerName(data.model.provider)} · ${data.model.model} (не разрешена)`}</option>
              )}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Температура</span>
            <input className={field} value={temperature} onChange={(e) => setTemperature(e.target.value)} placeholder="из общих настроек" inputMode="decimal" />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Максимум токенов ответа</span>
            <input className={field} value={maxTokens} onChange={(e) => setMaxTokens(e.target.value)} placeholder="из общих настроек" inputMode="numeric" />
          </label>
        </div>
      </Section>

      <Section
        title="Инструкция (System Prompt)"
        hint={data.promptIsDefault ? "по умолчанию" : "изменена"}
        actions={
          data.defaultPrompt && prompt.trim() !== data.defaultPrompt ? (
            <button className={button} onClick={() => setPrompt(data.defaultPrompt!)}>
              <RotateCcw className="size-4" />
              Вернуть по умолчанию
            </button>
          ) : undefined
        }
      >
        <textarea
          className={`${field} h-auto min-h-[180px] py-2 font-mono text-[13px] leading-relaxed`}
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          spellCheck={false}
        />
        <p className="mt-2 text-xs text-ink-3">
          К инструкции всегда добавляются правила качества (не придумывать данные, разделять факты и рекомендации, указывать источники), дата и формат итога —
          их изменить нельзя. Пользователи инструкцию не видят.
        </p>
      </Section>

      <Section title="Источники знаний" hint="какие данные OneBase открыты сотруднику">
        <div className="space-y-2">
          {data.sources.map((s) => (
            <label key={s.code} className="flex items-start gap-3 rounded-lg border border-line px-3 py-2.5 hover:bg-muted/50">
              <input
                type="checkbox"
                checked={sources.includes(s.code)}
                onChange={(e) => toggleSource(s.code, e.target.checked)}
                className="mt-0.5 size-4 accent-[var(--color-accent)]"
              />
              <span className="min-w-0">
                <span className="block text-sm font-medium text-ink">{s.name}</span>
                <span className="block text-sm text-ink-2">{s.description}</span>
                <span className="mt-0.5 block text-xs text-ink-3">
                  Таблицы: {s.tables} · Отчёты: {s.reports}
                  {s.requiredPermission ? ` · право пользователя: «${permissionLabel(s.requiredPermission)}»` : ""}
                </span>
              </span>
            </label>
          ))}
        </div>
      </Section>

      {!data.isConsultant && (
        <Section title="Инструменты" hint="через них сотрудник получает данные">
          {data.tools.length === 0 ? (
            <p className="text-sm text-ink-2">Инструментов с данными пока нет.</p>
          ) : (
            <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
              <table className="w-full min-w-max text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">Разрешён</th>
                    <th className="py-2 pr-3 font-semibold">Инструмент</th>
                    <th className="py-2 pr-3 font-semibold">Источник</th>
                    <th className="py-2 font-semibold" title="Каждый вызов ждёт одобрения человека">Подтверждение</th>
                  </tr>
                </thead>
                <tbody>
                  {data.tools.map((t) => {
                    const state = tools[t.name];
                    const open = sources.includes(t.source);
                    return (
                      <tr key={t.name} className="border-b border-line last:border-0">
                        <td className="py-2 pr-3">
                          <input
                            type="checkbox"
                            checked={state.enabled}
                            onChange={(e) => setTools((prev) => ({ ...prev, [t.name]: { ...prev[t.name], enabled: e.target.checked } }))}
                            aria-label={`Разрешить ${t.title}`}
                            className="size-4 accent-[var(--color-accent)]"
                          />
                        </td>
                        <td className="py-2 pr-3">
                          <div className="font-medium text-ink">{t.title}</div>
                          <div className="max-w-xl text-xs text-ink-3">{t.description}</div>
                        </td>
                        <td className={`py-2 pr-3 text-xs ${open ? "text-ink-2" : "text-warn"}`}>
                          {sourceName(t.source)}
                          {!open && " — источник закрыт"}
                        </td>
                        <td className="py-2">
                          <input
                            type="checkbox"
                            checked={state.requiresApproval}
                            onChange={(e) => setTools((prev) => ({ ...prev, [t.name]: { ...prev[t.name], requiresApproval: e.target.checked } }))}
                            aria-label={`Подтверждение для ${t.title}`}
                            className="size-4 accent-[var(--color-accent)]"
                          />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          <p className="mt-3 text-xs text-ink-3">
            Инструмент работает, только если он разрешён, его источник открыт и у пользователя есть право на эти данные. Все инструменты только читают данные.
          </p>
        </Section>
      )}

      <div className="mb-6 flex items-center gap-3">
        <button className={primary} onClick={save} disabled={busy !== null || name.trim() === ""}>
          {busy === "save" && <Loader2 className="size-4 animate-spin" />}
          Сохранить
        </button>
      </div>

      {!data.isConsultant && (
        <Section title="Проверить сотрудника" hint="задача одному сотруднику, без консультанта">
          <textarea
            className={`${field} h-auto min-h-[80px] py-2`}
            value={task}
            onChange={(e) => setTask(e.target.value)}
            placeholder="Например: какие регионы отстают от плана в этом месяце?"
            maxLength={4000}
          />
          <div className="mt-3 flex items-center gap-3">
            <button className={button} onClick={execute} disabled={busy !== null || !task.trim()}>
              {busy === "run" ? <Loader2 className="size-4 animate-spin" /> : <Play className="size-4" />}
              Запустить
            </button>
            <span className="text-xs text-ink-3">Работает с сохранёнными настройками и вашими правами.</span>
          </div>
          {run && (
            <div className="mt-4">
              <AgentCard result={run.result} />
              <p className="mt-1.5 text-xs text-ink-3">
                Модель {run.model ?? "—"} · токенов {num(run.usage.inputTokens)} + {num(run.usage.outputTokens)}
              </p>
            </div>
          )}
        </Section>
      )}

      <Note>
        Пустые поля модели, температуры и токенов — берутся из общих настроек AI. Выключенный сотрудник не привлекается к вопросам, консультант сообщает об
        этом в ответе.
      </Note>
    </>
  );
}
