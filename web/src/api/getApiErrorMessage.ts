/**
 * Pulls a human-readable message out of an RTK Query error, preferring the
 * backend's own `message` (the shape most of this project's controllers
 * return for their own thrown exceptions, e.g. `{"message": "..."}`), then
 * falling back to ASP.NET Core's default ProblemDetails `title`, then a
 * caller-supplied fallback if neither is present.
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === "object" && error !== null && "data" in error) {
    const data = (error as { data?: unknown }).data;

    if (typeof data === "object" && data !== null) {
      const message = (data as { message?: unknown }).message;
      if (typeof message === "string" && message.trim()) {
        return message;
      }

      const title = (data as { title?: unknown }).title;
      if (typeof title === "string" && title.trim()) {
        return title;
      }
    }
  }

  return fallback;
}
