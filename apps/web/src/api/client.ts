/**
 * Thin fetch wrapper for the VenueFlow API. Requests go to the same origin
 * (the Vite dev server proxies /api), so the HttpOnly session cookie is sent
 * automatically and never touched by JavaScript.
 */

export class ApiError extends Error {
  constructor(
    readonly status: number,
    /** Stable machine-readable code from the problem response, e.g. `revision_mismatch`. */
    readonly code: string | null,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export interface ApiResponse<T> {
  data: T
  etag: string | null
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  ifMatch?: string | null
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<ApiResponse<T>> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  if (options.ifMatch) headers['If-Match'] = options.ifMatch

  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    credentials: 'same-origin',
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  const data = response.status === 204 ? (undefined as T) : ((await response.json()) as T)
  return { data, etag: response.headers.get('ETag') }
}

async function toApiError(response: Response): Promise<ApiError> {
  let code: string | null = null
  let detail = `Request failed (${response.status})`
  try {
    const problem = (await response.json()) as { code?: unknown; detail?: unknown; title?: unknown }
    if (typeof problem.code === 'string') code = problem.code
    if (typeof problem.detail === 'string') detail = problem.detail
    else if (typeof problem.title === 'string') detail = problem.title
  } catch {
    // Not JSON (e.g. a proxy error page); keep the generic message.
  }
  return new ApiError(response.status, code, detail)
}
