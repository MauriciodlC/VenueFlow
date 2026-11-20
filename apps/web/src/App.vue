<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'

const session = useSessionStore()
const router = useRouter()

async function signOut() {
  await session.signOut()
  await router.push({ name: 'search' })
}

// Show the right navigation on first load if a session cookie already exists.
void session.restore()
</script>

<template>
  <header class="topbar">
    <RouterLink
      class="brand"
      :to="{ name: 'search' }"
    >
      VenueFlow
    </RouterLink>
    <nav aria-label="Main">
      <RouterLink :to="{ name: 'search' }">
        Find a space
      </RouterLink>
      <RouterLink
        v-if="session.hasRole('guest')"
        :to="{ name: 'my-bookings' }"
      >
        My bookings
      </RouterLink>
      <RouterLink
        v-if="session.hasRole('host')"
        :to="{ name: 'host' }"
      >
        Host workspace
      </RouterLink>
      <RouterLink
        v-if="session.hasRole('admin')"
        :to="{ name: 'admin' }"
      >
        Admin
      </RouterLink>
    </nav>
    <div class="account">
      <template v-if="session.user">
        <span>{{ session.user.displayName }} ({{ session.user.role }})</span>
        <button
          type="button"
          @click="signOut"
        >
          Sign out
        </button>
      </template>
      <RouterLink
        v-else
        :to="{ name: 'sign-in' }"
      >
        Sign in
      </RouterLink>
    </div>
  </header>
  <main class="content">
    <RouterView />
  </main>
</template>
