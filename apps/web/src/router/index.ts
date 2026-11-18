import { createRouter, createWebHistory } from 'vue-router'
import type { Role } from '@/stores/session'
import { useSessionStore } from '@/stores/session'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    /** Roles allowed to open the route. Omitted = public. */
    roles?: Role[]
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'search',
      component: () => import('@/views/SearchView.vue'),
      meta: { title: 'Find a space' },
    },
    {
      path: '/me/bookings',
      name: 'my-bookings',
      component: () => import('@/views/MyBookingsView.vue'),
      meta: { title: 'My bookings', roles: ['guest'] },
    },
    {
      path: '/host',
      name: 'host',
      component: () => import('@/views/HostWorkspaceView.vue'),
      meta: { title: 'Host workspace', roles: ['host'] },
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/views/AdminView.vue'),
      meta: { title: 'Admin', roles: ['admin'] },
    },
    {
      path: '/sign-in',
      name: 'sign-in',
      component: () => import('@/views/SignInView.vue'),
      meta: { title: 'Sign in' },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
      meta: { title: 'Not found' },
    },
  ],
})

router.beforeEach((to) => {
  const roles = to.meta.roles
  if (!roles) return true

  const session = useSessionStore()
  if (!session.isSignedIn) {
    return { name: 'sign-in', query: { redirect: to.fullPath } }
  }
  return session.hasRole(...roles) ? true : { name: 'search' }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · VenueFlow` : 'VenueFlow'
})
