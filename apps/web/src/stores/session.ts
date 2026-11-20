import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import * as auth from '@/api/auth'
import { ApiError } from '@/api/client'
import type { Role, SessionUser } from '@/api/auth'

export type { Role, SessionUser }

/**
 * The signed-in user, mirrored from the API session cookie. `restore()` asks
 * the API once per page load; after that, sign-in and sign-out keep it current.
 */
export const useSessionStore = defineStore('session', () => {
  const user = ref<SessionUser | null>(null)
  const restored = ref(false)

  const isSignedIn = computed(() => user.value !== null)
  const role = computed<Role | null>(() => user.value?.role ?? null)

  async function restore() {
    if (restored.value) return
    try {
      user.value = await auth.currentUser()
    } catch (error) {
      // 401 just means "not signed in"; anything else is still treated as signed out.
      if (!(error instanceof ApiError)) console.warn('Could not restore the session', error)
      user.value = null
    } finally {
      restored.value = true
    }
  }

  async function signIn(email: string, password: string) {
    user.value = await auth.login(email, password)
    restored.value = true
  }

  async function signOut() {
    try {
      await auth.logout()
    } finally {
      user.value = null
    }
  }

  function hasRole(...roles: Role[]) {
    return role.value !== null && roles.includes(role.value)
  }

  return { user, restored, isSignedIn, role, restore, signIn, signOut, hasRole }
})
