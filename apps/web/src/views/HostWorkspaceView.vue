<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { listHostVenues, type VenueStatus, type VenueSummary } from '@/api/hostVenues'

const venues = ref<VenueSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const statusLabels: Record<VenueStatus, string> = {
  Draft: 'Draft',
  InReview: 'In review',
  Published: 'Published',
}

onMounted(async () => {
  try {
    venues.value = await listHostVenues()
  } catch {
    error.value = 'Could not load your venues. Try again in a moment.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <section>
    <div class="page-header">
      <h1>Host workspace</h1>
      <RouterLink
        class="button"
        :to="{ name: 'host-venue-new' }"
      >
        New venue
      </RouterLink>
    </div>

    <p v-if="loading">
      Loading your venues…
    </p>
    <p
      v-else-if="error"
      class="error"
      role="alert"
    >
      {{ error }}
    </p>
    <p v-else-if="venues.length === 0">
      You have no venues yet. Start a draft to list your first space.
    </p>
    <table
      v-else
      class="venue-table"
    >
      <thead>
        <tr>
          <th scope="col">
            Venue
          </th>
          <th scope="col">
            City
          </th>
          <th scope="col">
            Rooms
          </th>
          <th scope="col">
            Status
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="venue in venues"
          :key="venue.id"
        >
          <td>
            <RouterLink :to="{ name: 'host-venue-edit', params: { id: venue.id } }">
              {{ venue.name }}
            </RouterLink>
          </td>
          <td>{{ venue.city ?? '—' }}</td>
          <td>{{ venue.roomCount }}</td>
          <td>
            <span :class="['status', `status-${venue.status.toLowerCase()}`]">
              {{ statusLabels[venue.status] }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
