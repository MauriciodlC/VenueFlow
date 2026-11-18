<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore, type Role } from '@/stores/session'

// Local demo sign-in until the API login lands. Nothing is sent to a server.
const demoAccounts: { role: Role; displayName: string }[] = [
  { role: 'guest', displayName: 'Demo guest' },
  { role: 'host', displayName: 'Demo host' },
  { role: 'admin', displayName: 'Demo admin' },
]

const session = useSessionStore()
const route = useRoute()
const router = useRouter()

function signInAs(role: Role, displayName: string) {
  session.signIn({ id: `demo-${role}`, displayName, role })
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
  // Only follow in-app paths to avoid open redirects.
  router.push(redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/')
}
</script>

<template>
  <section>
    <h1>Sign in</h1>
    <p>Choose a demo account. All data is synthetic.</p>
    <ul class="demo-accounts">
      <li
        v-for="account in demoAccounts"
        :key="account.role"
      >
        <button
          type="button"
          @click="signInAs(account.role, account.displayName)"
        >
          {{ account.displayName }}
        </button>
      </li>
    </ul>
  </section>
</template>
