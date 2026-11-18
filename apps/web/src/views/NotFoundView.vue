<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section>
    <h1>Page not found</h1>
    <p>
      <RouterLink :to="{ name: 'search' }">
        Back to search
      </RouterLink>
    </p>
  </section>
</template>
