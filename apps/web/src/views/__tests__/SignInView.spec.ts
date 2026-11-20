import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import SignInView from '@/views/SignInView.vue'
import { useSessionStore } from '@/stores/session'

const Stub = { template: '<div />' }

const hostUser = { id: 'u-host', email: 'host@example.test', displayName: 'Hugo Host', role: 'host' }

function makeRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'search', component: Stub },
      { path: '/host', name: 'host', component: Stub },
      { path: '/sign-in', name: 'sign-in', component: SignInView },
    ],
  })
}

async function mountAt(path: string) {
  const router = makeRouter()
  await router.push(path)
  await router.isReady()
  const wrapper = mount(SignInView, { global: { plugins: [router] } })
  return { router, wrapper }
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

async function fillAndSubmit(wrapper: Awaited<ReturnType<typeof mountAt>>['wrapper'], email: string, password: string) {
  await wrapper.get('input[name="email"]').setValue(email)
  await wrapper.get('input[name="password"]').setValue(password)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('SignInView', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the credentials, stores the user and follows an in-app redirect', async () => {
    fetchMock.mockResolvedValueOnce(json(200, hostUser))
    const { router, wrapper } = await mountAt('/sign-in?redirect=/host')

    await fillAndSubmit(wrapper, ' host@example.test ', 'secret-demo-password')

    const [url, init] = fetchMock.mock.calls[0]!
    expect(url).toBe('/api/auth/login')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual({ email: 'host@example.test', password: 'secret-demo-password' })
    expect(useSessionStore().user).toEqual(hostUser)
    expect(router.currentRoute.value.fullPath).toBe('/host')
  })

  it('shows one message for wrong credentials and clears the password', async () => {
    fetchMock.mockResolvedValueOnce(json(401, { code: 'invalid_credentials', detail: 'Email or password is incorrect.' }))
    const { router, wrapper } = await mountAt('/sign-in')

    await fillAndSubmit(wrapper, 'host@example.test', 'wrong')

    expect(wrapper.get('[role="alert"]').text()).toBe('Email or password is incorrect.')
    expect((wrapper.get('input[name="password"]').element as HTMLInputElement).value).toBe('')
    expect(useSessionStore().isSignedIn).toBe(false)
    expect(router.currentRoute.value.name).toBe('sign-in')
  })

  it('does not call the API when a field is empty', async () => {
    const { wrapper } = await mountAt('/sign-in')

    await fillAndSubmit(wrapper, 'host@example.test', '')

    expect(fetchMock).not.toHaveBeenCalled()
    expect(wrapper.get('[role="alert"]').text()).toBe('Enter your email and password.')
  })

  it('fills the email of a demo account', async () => {
    const { wrapper } = await mountAt('/sign-in')

    const host = wrapper.findAll('button').find((button) => button.text() === 'Host')
    await host!.trigger('click')

    expect((wrapper.get('input[name="email"]').element as HTMLInputElement).value).toBe('host@example.test')
  })

  it.each(['https://evil.example/', '//evil.example/path'])('ignores the external redirect %s', async (redirect) => {
    fetchMock.mockResolvedValueOnce(json(200, hostUser))
    const { router, wrapper } = await mountAt(`/sign-in?redirect=${encodeURIComponent(redirect)}`)

    await fillAndSubmit(wrapper, 'host@example.test', 'secret-demo-password')

    expect(router.currentRoute.value.fullPath).toBe('/')
  })
})
