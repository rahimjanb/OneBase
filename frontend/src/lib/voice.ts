/**
 * Голос AI-консультанта: вопрос голосом (запись → распознавание на сервере) и озвучка ответа (mp3 частями с сервера).
 * Если на сервере голос не настроен (нет ключа OpenAI) — голос самого браузера: Web Speech API, где он есть.
 */

export type VoiceStatus = { ready: boolean; message: string | null; speechModel: string | null; transcribeModel: string | null };

/** Самая длинная запись вопроса. */
export const MaxRecordSeconds = 120;

const recordTypes = ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", "audio/ogg;codecs=opus"];

export const canRecord = () =>
  typeof window !== "undefined" && typeof MediaRecorder !== "undefined" && !!navigator.mediaDevices?.getUserMedia;

type BrowserRecognition = {
  lang: string;
  interimResults: boolean;
  continuous: boolean;
  onresult: ((e: { results: ArrayLike<ArrayLike<{ transcript: string }>> }) => void) | null;
  onerror: ((e: { error: string }) => void) | null;
  onend: (() => void) | null;
  start: () => void;
  stop: () => void;
  abort: () => void;
};

function recognitionClass(): (new () => BrowserRecognition) | null {
  if (typeof window === "undefined") return null;
  const w = window as unknown as { SpeechRecognition?: new () => BrowserRecognition; webkitSpeechRecognition?: new () => BrowserRecognition };
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null;
}

export const canRecognizeInBrowser = () => recognitionClass() !== null;
export const canSpeakInBrowser = () => typeof window !== "undefined" && "speechSynthesis" in window;

/** Запись вопроса с микрофона (MediaRecorder): start → stop даёт файл записи. */
export class VoiceRecorder {
  private recorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private stream: MediaStream | null = null;

  async start(): Promise<void> {
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
    } catch (e) {
      const name = (e as DOMException).name;
      throw new Error(
        name === "NotAllowedError" || name === "SecurityError"
          ? "Нет доступа к микрофону — разрешите его в настройках браузера для этого сайта."
          : name === "NotFoundError"
            ? "Микрофон не найден."
            : "Не удалось включить микрофон.",
      );
    }

    const mimeType = recordTypes.find((t) => MediaRecorder.isTypeSupported(t));
    this.recorder = new MediaRecorder(this.stream, mimeType ? { mimeType } : undefined);
    this.chunks = [];
    this.recorder.ondataavailable = (e) => e.data.size > 0 && this.chunks.push(e.data);
    this.recorder.start();
  }

  /** Остановить и вернуть запись; null — запись пустая. */
  stop(): Promise<Blob | null> {
    const recorder = this.recorder;
    if (!recorder) return Promise.resolve(null);
    return new Promise((resolve) => {
      recorder.onstop = () => {
        this.release();
        const blob = new Blob(this.chunks, { type: recorder.mimeType || "audio/webm" });
        resolve(blob.size > 0 ? blob : null);
      };
      recorder.stop();
    });
  }

  cancel() {
    if (this.recorder && this.recorder.state !== "inactive") {
      this.recorder.onstop = null;
      this.recorder.stop();
    }

    this.release();
  }

  private release() {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    this.recorder = null;
  }
}

const extensionOf = (type: string) => (type.includes("mp4") ? "mp4" : type.includes("ogg") ? "ogg" : type.includes("wav") ? "wav" : "webm");

async function errorOf(response: Response): Promise<Error> {
  const json = await response.json().catch(() => ({}) as { error?: string });
  return new Error(
    response.status === 403
      ? "Нет права пользоваться консультантом."
      : response.status === 429
        ? "Слишком много голосовых запросов подряд — подождите минуту."
        : (json.error ?? `Ошибка ${response.status}`),
  );
}

/** Распознать запись на сервере (OpenAI): текст вопроса. */
export async function transcribe(blob: Blob): Promise<string> {
  const form = new FormData();
  form.append("audio", blob, `question.${extensionOf(blob.type)}`);
  const response = await fetch("/bff/api/ai/voice/transcribe", { method: "POST", body: form });
  if (!response.ok) throw await errorOf(response);
  return ((await response.json()) as { text: string }).text;
}

/** Распознавание голосом браузера (Chrome, Edge, Safari): слушает до паузы и отдаёт текст. */
export function recognizeInBrowser(onDone: (text: string) => void, onError: (message: string) => void): { stop: () => void } {
  const Recognition = recognitionClass();
  if (!Recognition) {
    onError("Этот браузер не распознаёт речь.");
    return { stop: () => {} };
  }

  const recognition = new Recognition();
  recognition.lang = "ru-RU";
  recognition.interimResults = false;
  recognition.continuous = false;
  let text = "";
  recognition.onresult = (e) => {
    text = Array.from(e.results)
      .map((r) => r[0].transcript)
      .join(" ");
  };
  recognition.onerror = (e) => onError(e.error === "not-allowed" ? "Нет доступа к микрофону." : "Речь не распознана — попробуйте ещё раз.");
  recognition.onend = () => onDone(text.trim());
  recognition.start();
  return { stop: () => recognition.stop() };
}

export type SpeechState = "loading" | "playing" | "idle";

/**
 * Тишина (0,1 с WAV) для «разблокировки» звука на iPhone: первое воспроизведение должно начаться от нажатия пользователя,
 * после этого автоозвучка ответа уже разрешена.
 */
function silentWav(): string {
  const rate = 8000;
  const samples = 800;
  const buffer = new ArrayBuffer(44 + samples);
  const view = new DataView(buffer);
  const text = (offset: number, s: string) => [...s].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)));
  text(0, "RIFF");
  view.setUint32(4, 36 + samples, true);
  text(8, "WAVE");
  text(12, "fmt ");
  view.setUint32(16, 16, true); // размер блока fmt
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, 1, true); // моно
  view.setUint32(24, rate, true);
  view.setUint32(28, rate, true); // байт в секунду
  view.setUint16(32, 1, true);
  view.setUint16(34, 8, true); // 8 бит
  text(36, "data");
  view.setUint32(40, samples, true);
  for (let i = 0; i < samples; i++) view.setUint8(44 + i, 128); // 8-битная тишина
  return URL.createObjectURL(new Blob([buffer], { type: "audio/wav" }));
}

/**
 * Озвучка ответа: части с сервера по очереди, следующая загружается, пока играет текущая.
 * Один проигрыватель на страницу — новая озвучка останавливает предыдущую.
 */
export class SpeechPlayer {
  private audio: HTMLAudioElement | null = null;
  private run = 0;
  private finish: (() => void) | null = null;

  /** Вызвать из обработчика нажатия: на iPhone потом звук можно включить и без нажатия (автоозвучка ответа). */
  unlock() {
    const audio = this.element();
    if (audio.src) return;
    audio.src = silentWav();
    void audio.play().catch(() => {});
  }

  stop() {
    this.run++;
    this.audio?.pause();
    if (canSpeakInBrowser()) window.speechSynthesis.cancel();
    this.finish?.();
    this.finish = null;
  }

  /** server — озвучка OpenAI; иначе голос браузера по тексту частей. */
  async speak(messageId: string, server: boolean, onState: (state: SpeechState) => void): Promise<void> {
    this.stop();
    const run = this.run;
    const alive = () => run === this.run;
    onState("loading");
    try {
      const textResponse = await fetch(`/bff/api/ai/messages/${messageId}/speech-text`);
      if (!textResponse.ok) throw await errorOf(textResponse);
      const { parts } = (await textResponse.json()) as { parts: string[] };
      if (!alive()) return;
      if (server) await this.speakServer(messageId, parts.length, alive, onState);
      else await this.speakBrowser(parts, alive, onState);
    } finally {
      if (alive()) onState("idle");
    }
  }

  private element(): HTMLAudioElement {
    this.audio ??= new Audio();
    return this.audio;
  }

  private async speakServer(messageId: string, count: number, alive: () => boolean, onState: (state: SpeechState) => void) {
    const load = async (part: number) => {
      const response = await fetch(`/bff/api/ai/messages/${messageId}/speech?part=${part}`);
      if (!response.ok) throw await errorOf(response);
      return URL.createObjectURL(await response.blob());
    };

    let next = load(0);
    for (let part = 0; part < count; part++) {
      const url = await next;
      if (!alive()) {
        URL.revokeObjectURL(url);
        return;
      }

      if (part + 1 < count) next = load(part + 1);
      try {
        await this.play(url, onState);
      } finally {
        URL.revokeObjectURL(url);
      }

      if (!alive()) return;
    }
  }

  private play(url: string, onState: (state: SpeechState) => void): Promise<void> {
    const audio = this.element();
    return new Promise((resolve, reject) => {
      this.finish = resolve;
      audio.onended = () => resolve();
      audio.onerror = () => reject(new Error("Не удалось воспроизвести звук."));
      audio.src = url;
      audio
        .play()
        .then(() => onState("playing"))
        .catch((e: DOMException) =>
          reject(new Error(e.name === "NotAllowedError" ? "Браузер не дал включить звук — нажмите «Озвучить»." : "Не удалось воспроизвести звук.")),
        );
    });
  }

  private async speakBrowser(parts: string[], alive: () => boolean, onState: (state: SpeechState) => void) {
    if (!canSpeakInBrowser()) throw new Error("Этот браузер не умеет читать вслух.");
    const synth = window.speechSynthesis;
    const voice = synth.getVoices().find((v) => v.lang.toLowerCase().startsWith("ru"));
    for (const text of parts) {
      if (!alive()) return;
      await new Promise<void>((resolve) => {
        this.finish = resolve;
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = "ru-RU";
        if (voice) utterance.voice = voice;
        utterance.onstart = () => onState("playing");
        utterance.onend = () => resolve();
        utterance.onerror = () => resolve();
        synth.speak(utterance);
      });
    }
  }
}
