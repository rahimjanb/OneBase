import Link from "next/link";
import { Fragment } from "react";

/**
 * Небольшой рендер Markdown для ответов консультанта: заголовки, абзацы, списки, таблицы, цитаты,
 * **жирный**, *курсив*, `код`, ссылки. Строит React-элементы — HTML из ответа модели не вставляется.
 */
export function Markdown({ text }: { text: string }) {
  return <div className="space-y-3 text-sm leading-relaxed text-ink">{blocks(text)}</div>;
}

type Block =
  | { kind: "heading"; level: number; text: string }
  | { kind: "paragraph"; text: string }
  | { kind: "list"; ordered: boolean; items: string[] }
  | { kind: "table"; header: string[]; rows: string[][] }
  | { kind: "quote"; text: string }
  | { kind: "code"; text: string }
  | { kind: "rule" };

const bullet = /^\s*[-*•]\s+(.*)$/;
const numbered = /^\s*\d+[.)]\s+(.*)$/;
const tableRow = /^\s*\|.*\|\s*$/;
const tableDivider = /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/;

function cells(line: string): string[] {
  return line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map((c) => c.trim());
}

function parse(text: string): Block[] {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const out: Block[] = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (line.trim() === "") {
      i++;
      continue;
    }
    if (line.trim().startsWith("```")) {
      const code: string[] = [];
      i++;
      while (i < lines.length && !lines[i].trim().startsWith("```")) code.push(lines[i++]);
      i++;
      out.push({ kind: "code", text: code.join("\n") });
      continue;
    }
    const heading = /^(#{1,6})\s+(.*)$/.exec(line);
    if (heading) {
      out.push({ kind: "heading", level: heading[1].length, text: heading[2] });
      i++;
      continue;
    }
    if (/^\s*(-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      out.push({ kind: "rule" });
      i++;
      continue;
    }
    if (tableRow.test(line) && i + 1 < lines.length && tableDivider.test(lines[i + 1])) {
      const header = cells(line);
      const rows: string[][] = [];
      i += 2;
      while (i < lines.length && tableRow.test(lines[i])) rows.push(cells(lines[i++]));
      out.push({ kind: "table", header, rows });
      continue;
    }
    if (bullet.test(line) || numbered.test(line)) {
      const ordered = !bullet.test(line);
      const pattern = ordered ? numbered : bullet;
      const items: string[] = [];
      while (i < lines.length && (pattern.test(lines[i]) || (/^\s{2,}\S/.test(lines[i]) && items.length > 0))) {
        const m = pattern.exec(lines[i]);
        if (m) items.push(m[1]);
        else items[items.length - 1] += " " + lines[i].trim();
        i++;
      }
      out.push({ kind: "list", ordered, items });
      continue;
    }
    if (line.trim().startsWith(">")) {
      const quote: string[] = [];
      while (i < lines.length && lines[i].trim().startsWith(">")) quote.push(lines[i++].trim().replace(/^>\s?/, ""));
      out.push({ kind: "quote", text: quote.join(" ") });
      continue;
    }
    const paragraph: string[] = [];
    while (
      i < lines.length &&
      lines[i].trim() !== "" &&
      !/^(#{1,6})\s/.test(lines[i]) &&
      !bullet.test(lines[i]) &&
      !numbered.test(lines[i]) &&
      !lines[i].trim().startsWith("```") &&
      !(tableRow.test(lines[i]) && i + 1 < lines.length && tableDivider.test(lines[i + 1]))
    ) {
      paragraph.push(lines[i++]);
    }
    out.push({ kind: "paragraph", text: paragraph.join("\n") });
  }
  return out;
}

const headingClass = ["", "text-lg font-semibold", "text-base font-semibold", "text-sm font-semibold", "text-sm font-semibold", "text-sm font-semibold", "text-sm font-semibold"];

function blocks(text: string) {
  return parse(text).map((b, i) => {
    switch (b.kind) {
      case "heading":
        return (
          <p key={i} className={`${headingClass[b.level]} pt-1 text-ink`}>
            {inline(b.text)}
          </p>
        );
      case "list": {
        const Tag = b.ordered ? "ol" : "ul";
        return (
          <Tag key={i} className={`${b.ordered ? "list-decimal" : "list-disc"} space-y-1 pl-5 marker:text-ink-3`}>
            {b.items.map((item, j) => (
              <li key={j}>{inline(item)}</li>
            ))}
          </Tag>
        );
      }
      case "table":
        return (
          <div key={i} className="overflow-x-auto rounded-lg border border-line">
            <table className="w-full min-w-max text-sm">
              <thead className="bg-muted/60">
                <tr>
                  {b.header.map((h, j) => (
                    <th key={j} className="px-3 py-2 text-left text-xs font-semibold text-ink-2">
                      {inline(h)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {b.rows.map((row, r) => (
                  <tr key={r} className="border-t border-line">
                    {row.map((c, j) => (
                      <td key={j} className={`px-3 py-1.5 ${/^[-+−]?[\d\s.,]+%?$/.test(c) ? "text-right tabular-nums" : ""}`}>
                        {inline(c)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        );
      case "quote":
        return (
          <blockquote key={i} className="border-l-2 border-accent/40 pl-3 text-ink-2">
            {inline(b.text)}
          </blockquote>
        );
      case "code":
        return (
          <pre key={i} className="overflow-x-auto rounded-lg bg-muted px-3 py-2 font-mono text-xs text-ink">
            {b.text}
          </pre>
        );
      case "rule":
        return <hr key={i} className="border-line" />;
      default:
        return (
          <p key={i} className="whitespace-pre-line">
            {inline(b.text)}
          </p>
        );
    }
  });
}

const tokens = /(\*\*[^*]+\*\*|__[^_]+__|`[^`]+`|\[[^\]]+\]\([^)\s]+\)|\*[^*\s][^*]*\*)/g;

/** Внутристрочная разметка. Ссылки — только на страницы OneBase (/...) и https. */
function inline(text: string): React.ReactNode {
  return text.split(tokens).map((part, i) => {
    if (!part) return null;
    if ((part.startsWith("**") && part.endsWith("**")) || (part.startsWith("__") && part.endsWith("__")))
      return <strong key={i} className="font-semibold text-ink">{part.slice(2, -2)}</strong>;
    if (part.startsWith("`") && part.endsWith("`"))
      return <code key={i} className="rounded bg-muted px-1 py-0.5 font-mono text-[12px]">{part.slice(1, -1)}</code>;
    const link = /^\[([^\]]+)\]\(([^)\s]+)\)$/.exec(part);
    if (link) {
      const [, label, href] = link;
      if (href.startsWith("/") && !href.startsWith("//"))
        return <Link key={i} href={href} className="font-medium text-accent-strong hover:underline">{label}</Link>;
      if (href.startsWith("https://"))
        return <a key={i} href={href} target="_blank" rel="noopener noreferrer" className="font-medium text-accent-strong hover:underline">{label}</a>;
      return <Fragment key={i}>{label}</Fragment>;
    }
    if (part.startsWith("*") && part.endsWith("*") && part.length > 2) return <em key={i}>{part.slice(1, -1)}</em>;
    return <Fragment key={i}>{part}</Fragment>;
  });
}
