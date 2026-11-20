<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '@/api/client'
import { useSessionStore } from '@/stores/session'

// Seeded demo accounts. Their password is DEMO_PASSWORD in infra/compose/.env.
const demoAccounts = [
  { label: 'Guest', email: 'guest@example.test' },
  { label: 'Host', email: 'host@example.test' },
  { label: 'Staff', email: 'staff@example.test' },
  { label: 'Admin', email: 'admin@example.test' },
]

const session = useSessionStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const busy = ref(false)

function safeRedirect(): string {
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
  // Only follow in-app paths to avoid open redirects.
  return redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/'
}

async function submit() {
  error.value = null
  if (email.value.trim() === '' || password.value === '') {
    error.value = 'Enter your email and password.'
    return
  }

  busy.value = true
  try {
    await session.signIn(email.value.trim(), password.value)
    await router.push(safeRedirect())
  } catch (cause) {
    error.value =
      cause instanceof ApiError && cause.status === 401
        ? 'Email or password is incorrect.'
        : 'Could not sign in right now. Try again in a moment.'
  } finally {
    password.value = ''
    busy.value = false
  }
}
</script>

<template>
  <section class="narrow">
    <h1>Sign in</h1>
    <form
      novalidate
      @submit.prevent="submit"
    >
      <label>
        Email
        <input
          v-model="email"
          type="email"
          name="email"
          autocomplete="username"
          required
        >
      </label>
      <label>
        Password
        <input
          v-model="password"
          type="password"
          name="password"
          autocomplete="current-password"
          required
        >
      </label>
      <p
        v-if="error"
        class="error"
        role="alert"
      >
        {{ error }}
      </p>
      <button
        type="submit"
        :disabled="busy"
      >
        {{ busy ? 'Signing in…' : 'Sign in' }}
      </button>
    </form>

    <h2>Demo accounts</h2>
    <p>All data is synthetic. Pick an account to fill in its email; the password is <code>DEMO_PASSWORD</code> from your local <code>infra/compose/.env</code>.</p>
    <ul class="demo-accounts">
      <li
        v-for="account in demoAccounts"
        :key="account.email"
      >
        <button
          type="button"
          class="secondary"
          @click="email = account.email"
        >
          {{ account.label }}
        </button>
      </li>
    </ul>
  </section>
</template>
