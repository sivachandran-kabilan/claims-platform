import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';

export interface Notice {
  kind: 'success' | 'error';
  text: string;
}

/** One message banner for the whole app (success or error), auto-dismissed. */
@Injectable({ providedIn: 'root' })
export class NoticeService {
  readonly current = signal<Notice | null>(null);
  private timer?: ReturnType<typeof setTimeout>;

  success(text: string): void {
    this.show({ kind: 'success', text });
  }

  error(text: string): void {
    this.show({ kind: 'error', text });
  }

  /** Shows an API error as readable text (ProblemDetails title/detail plus field errors). */
  fromError(error: unknown): void {
    this.error(problemMessage(error));
  }

  dismiss(): void {
    this.current.set(null);
  }

  private show(notice: Notice): void {
    clearTimeout(this.timer);
    this.current.set(notice);
    this.timer = setTimeout(() => this.current.set(null), 8000);
  }
}

/** RFC 7807 ProblemDetails → one line. */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (error.status === 0) return 'The API is not reachable. Is it running?';

  const problem = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
  const fieldErrors = problem?.errors
    ? Object.entries(problem.errors).map(([field, messages]) => `${field}: ${messages.join(' ')}`)
    : [];
  return [problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`, ...fieldErrors].join(' · ');
}
