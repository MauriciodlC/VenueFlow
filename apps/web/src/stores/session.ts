import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type Role = 'guest' | 'host' | 'staff' | 'admin'

export interface SessionUser {
  id: string
  displayName: string
  role: Role
}

/**
 * Holds the signed-in user. Until the API login exists, a user can only be
 * set explicitly (e.g. from a demo account picker); nothing is persisted.
 */
export const useSessionStore = defineStore('session', () => {
  const user = ref<SessionUser | null>(null)

  const isSignedIn = computed(() => user.value !== null)
  const role = computed<Role | null>(() => user.value?.role ?? null)

  function signIn(next: SessionUser) {
    user.value = next
  }

  function signOut() {
    user.value = null
  }

  function hasRole(...roles: Role[]) {
    return role.value !== null && roles.includes(role.value)
  }

  return { user, isSignedIn, role, signIn, signOut, hasRole }
})
