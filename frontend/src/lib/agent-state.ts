/**
 * Подпись карточки AI-сотрудника под ответом консультанта — по фактической причине (status + reason с сервера), как AnswerOutcome на backend:
 * «нет данных» — только для старых ответов без причины; «не найдено» — искали и не нашли; «нет в источниках OneBase» — поиска не было;
 * «период недоступен», «источник не подключён», «нет доступа», «сбой источника» — каждое своё. У сбоя не показываются ни слова модели
 * («данных нет»), ни текст ошибки сервера (коды инструментов) — только понятное пояснение. Без зависимостей: проверяется node --test.
 */

export type AgentTone = "ok" | "warn" | "muted" | "bad";

/**
 * label — подпись (ключ перевода); note — пояснение вместо текста сервера (ключ перевода), null — показать summary и error как есть;
 * showSummary — итог модели сотрудника можно показывать (у сбоя и «период недоступен» он мог сказать «данных нет»).
 */
export type AgentState = { label: string; tone: AgentTone; note: string | null; showSummary: boolean };

const failure = "Технический сбой источника данных — данные не получены.";

export function agentState(status: string, reason?: string | null): AgentState {
  switch (status) {
    case "completed":
      return { label: "готово", tone: "ok", note: null, showSummary: true };
    case "partial":
      return reason === "interrupted"
        ? { label: "частично: прерван", tone: "warn", note: null, showSummary: true }
        : { label: "частично", tone: "warn", note: null, showSummary: true };
    case "forbidden":
      return { label: "нет доступа", tone: "muted", note: null, showSummary: true };
    case "disabled":
      return { label: "выключен", tone: "muted", note: null, showSummary: true };
    case "no_data":
      switch (reason) {
        case "not_found":
          return { label: "не найдено", tone: "warn", note: null, showSummary: true };
        case "not_in_onebase":
          return { label: "нет в источниках OneBase", tone: "muted", note: null, showSummary: true };
        case "out_of_range":
          return { label: "период недоступен", tone: "warn", note: null, showSummary: false };
        case "source_not_connected":
        case "source_not_loaded":
          return { label: "источник не подключён", tone: "muted", note: null, showSummary: true };
        case "no_access":
          return { label: "нет доступа", tone: "muted", note: null, showSummary: true };
        default:
          // Ответы до разделения причин: причины нет — прежняя подпись.
          return { label: "нет данных", tone: "warn", note: null, showSummary: true };
      }
    default:
      switch (reason) {
        case "model_unavailable":
          return { label: "модель недоступна", tone: "bad", note: "Модель AI не ответила — повторите запрос позже.", showSummary: false };
        case "interrupted":
          return { label: "прерван", tone: "bad", note: failure, showSummary: false };
        case "tool_error":
          return { label: "сбой источника", tone: "bad", note: failure, showSummary: false };
        default:
          return { label: "ошибка", tone: "bad", note: failure, showSummary: false };
      }
  }
}
