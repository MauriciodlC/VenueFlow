import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import SignInView from '@/views/SignInView.vue'
import { useSessionStore } from '@/stores/session'

const Stub = { template: '<div />' }

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

async function choose(wrapper: Awaited<ReturnType<typeof mountAt>>['wrapper'], label: string) {
  const button = wrapper.findAll('button').find((candidate) => candidate.text() === label)
  expect(button, `button "${label}"`).toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

describe('SignInView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('lists one demo account per role', async () => {
    const { wrapper } = await mountAt('/sign-in')

    expect(wrapper.findAll('button').map((button) => button.text())).toEqual([
      'Demo guest',
      'Demo host',
      'Demo admin',
    ])
  })

  it('signs in with the chosen role and follows an in-app redirect', async () => {
    const { router, wrapper } = await mountAt('/sign-in?redirect=/host')

    await choose(wrapper, 'Demo host')

    const session = useSessionStore()
    expect(session.user).toEqual({ id: 'demo-host', displayName: 'Demo host', role: 'host' })
    expect(router.currentRoute.value.fullPath).toBe('/host')
  })

  it.each(['https://evil.example/', '//evil.example/path'])(
    'ignores the external redirect %s',
    async (redirect) => {
      const { router, wrapper } = await mountAt(`/sign-in?redirect=${encodeURIComponent(redirect)}`)

      await choose(wrapper, 'Demo guest')

      expect(router.currentRoute.value.fullPath).toBe('/')
    },
  )
})
