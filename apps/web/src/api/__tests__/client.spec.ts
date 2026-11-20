import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiRequest } from '@/api/client'

describe('apiRequest', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends If-Match and returns the response ETag', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: 'v1' }), { status: 200, headers: { ETag: '"4"', 'Content-Type': 'application/json' } }),
    )

    const result = await apiRequest<{ id: string }>('/api/host/venues/v1', { method: 'PUT', body: { name: 'x' }, ifMatch: '"3"' })

    const init = fetchMock.mock.calls[0]![1]!
    expect((init.headers as Record<string, string>)['If-Match']).toBe('"3"')
    expect(result).toEqual({ data: { id: 'v1' }, etag: '"4"' })
  })

  it('turns problem responses into ApiError with the stable code', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ code: 'revision_mismatch', detail: 'The listing changed.' }), { status: 412 }),
    )

    const error = await apiRequest('/api/host/venues/v1').catch((cause: unknown) => cause)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 412, code: 'revision_mismatch', message: 'The listing changed.' })
  })

  it('handles empty 204 responses and non-JSON errors', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    await expect(apiRequest('/api/auth/logout', { method: 'POST' })).resolves.toEqual({ data: undefined, etag: null })

    fetchMock.mockResolvedValueOnce(new Response('<html>Bad gateway</html>', { status: 502 }))
    await expect(apiRequest('/api/info')).rejects.toMatchObject({ status: 502, code: null, message: 'Request failed (502)' })
  })
})
