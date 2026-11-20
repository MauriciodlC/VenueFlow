<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { ApiError } from '@/api/client'
import {
  createVenueDraft,
  getVenueDraft,
  previewVenue,
  submitVenue,
  updateVenueDraft,
  type VenueDraft,
  type VenueDraftInput,
  type VenuePreview,
} from '@/api/hostVenues'
import {
  availableTimeZones,
  limits,
  missingLabels,
  toInput,
  validateAll,
  validateStep,
  wizardSteps,
  type FieldErrors,
  type WizardStep,
} from '@/venues/draftValidation'
import { accessibilityLabels, appendAccessibilityLabel, hasAccessibilityLabel } from '@/venues/accessibilityLabels'

const route = useRoute()
const router = useRouter()
const routeId = computed(() => (typeof route.params.id === 'string' ? route.params.id : undefined))

const zones = availableTimeZones()
const browserZone = Intl.DateTimeFormat().resolvedOptions().timeZone

const draft = ref<VenueDraft | null>(null)
const etag = ref<string | null>(null)
const form = ref<VenueDraftInput>(emptyInput())
const step = ref<WizardStep | 'preview'>('basics')
const errors = ref<FieldErrors>({})
const message = ref<string | null>(null)
const failure = ref<string | null>(null)
const preview = ref<VenuePreview | null>(null)
const loading = ref(false)
const saving = ref(false)

const isNew = computed(() => !routeId.value && !draft.value)
const editable = computed(() => isNew.value || draft.value?.status === 'Draft')
const zoneOptions = computed(() =>
  zones.includes(form.value.timeZoneId) || form.value.timeZoneId === '' ? zones : [form.value.timeZoneId, ...zones],
)

function emptyInput(): VenueDraftInput {
  return {
    name: '',
    timeZoneId: zones.includes(browserZone) ? browserZone : '',
    description: '',
    city: '',
    addressLine: '',
    accessibilityNotes: '',
    rooms: [],
  }
}

function apply(next: VenueDraft, nextEtag: string | null) {
  draft.value = next
  etag.value = nextEtag
  form.value = toInput(next)
}

async function load(id: string) {
  loading.value = true
  failure.value = null
  try {
    const response = await getVenueDraft(id)
    apply(response.data, response.etag)
  } catch (cause) {
    failure.value =
      cause instanceof ApiError && cause.status === 404
        ? 'This venue does not exist or belongs to another host.'
        : 'Could not load the venue. Try again in a moment.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (routeId.value) void load(routeId.value)
})

watch(
  routeId,
  (id, previous) => {
    if (!id) {
      // "New venue" opened while editing another one: start from a blank form.
      draft.value = null
      etag.value = null
      form.value = emptyInput()
      preview.value = null
      step.value = 'basics'
    } else if (id !== previous && id !== draft.value?.id) {
      void load(id)
    }
  },
)

function describe(cause: unknown): string {
  if (cause instanceof ApiError) {
    if (cause.code === 'revision_mismatch') {
      return 'Someone else changed this venue since you opened it. Reload to see their changes, then edit again.'
    }
    return cause.message
  }
  return 'Could not reach the server. Your changes are still here; try saving again.'
}

/** Validates the current step, then creates the draft (first save) or saves it with If-Match. */
async function save(): Promise<boolean> {
  message.value = null
  failure.value = null
  errors.value = step.value === 'preview' ? validateAll(form.value, zoneOptions.value) : validateStep(step.value, form.value, zoneOptions.value)
  if (Object.keys(errors.value).length > 0) return false

  saving.value = true
  try {
    if (!draft.value) {
      const created = await createVenueDraft(form.value.name, form.value.timeZoneId)
      apply(created.data, created.etag)
      await router.replace({ name: 'host-venue-edit', params: { id: created.data.id } })
    } else {
      const saved = await updateVenueDraft(draft.value.id, form.value, etag.value ?? '')
      apply(saved.data, saved.etag)
    }
    message.value = 'Saved.'
    return true
  } catch (cause) {
    failure.value = describe(cause)
    return false
  } finally {
    saving.value = false
  }
}

async function goTo(target: WizardStep | 'preview') {
  if (editable.value && !(await save())) return
  step.value = target
  if (target === 'preview' && draft.value) {
    preview.value = await previewVenue(draft.value.id).catch(() => null)
  }
}

function addAccessibilityLabel(label: string) {
  form.value.accessibilityNotes = appendAccessibilityLabel(form.value.accessibilityNotes, label)
}

function addRoom() {
  form.value.rooms.push({ id: null, name: '', capacity: 1 })
}

function removeRoom(index: number) {
  form.value.rooms.splice(index, 1)
}

async function submitForReview() {
  if (!draft.value || !etag.value) return
  failure.value = null
  saving.value = true
  try {
    const submitted = await submitVenue(draft.value.id, etag.value)
    apply(submitted.data, submitted.etag)
    message.value = 'Submitted for review. An admin will check the listing before guests can see it.'
  } catch (cause) {
    failure.value = describe(cause)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section>
    <p>
      <RouterLink :to="{ name: 'host' }">
        ← Back to your venues
      </RouterLink>
    </p>
    <h1>{{ isNew ? 'New venue' : form.name || 'Venue' }}</h1>

    <p v-if="loading">
      Loading…
    </p>
    <template v-else>
      <p
        v-if="draft && !editable"
        class="notice"
      >
        This venue is {{ draft.status === 'InReview' ? 'in review' : 'published' }}, so it can no longer be edited here.
      </p>

      <nav
        class="wizard-steps"
        aria-label="Draft steps"
      >
        <button
          v-for="(item, index) in wizardSteps"
          :key="item.id"
          type="button"
          :aria-current="step === item.id ? 'step' : undefined"
          :disabled="saving || (isNew && index > 0)"
          @click="goTo(item.id)"
        >
          {{ index + 1 }}. {{ item.label }}
        </button>
        <button
          type="button"
          :aria-current="step === 'preview' ? 'step' : undefined"
          :disabled="saving || isNew"
          @click="goTo('preview')"
        >
          {{ wizardSteps.length + 1 }}. Preview & submit
        </button>
      </nav>

      <form
        novalidate
        @submit.prevent="save"
      >
        <fieldset :disabled="!editable || saving">
          <template v-if="step === 'basics'">
            <label>
              Venue name
              <input
                v-model="form.name"
                name="name"
                :maxlength="limits.name"
                :aria-invalid="!!errors.name"
              >
            </label>
            <p
              v-if="errors.name"
              class="field-error"
            >
              {{ errors.name }}
            </p>
            <label>
              Timezone
              <select
                v-model="form.timeZoneId"
                name="timeZoneId"
                :aria-invalid="!!errors.timeZoneId"
              >
                <option
                  disabled
                  value=""
                >
                  Choose a timezone
                </option>
                <option
                  v-for="zone in zoneOptions"
                  :key="zone"
                  :value="zone"
                >
                  {{ zone }}
                </option>
              </select>
            </label>
            <p class="hint">
              All opening hours and slots for this venue are shown in this timezone.
            </p>
            <p
              v-if="errors.timeZoneId"
              class="field-error"
            >
              {{ errors.timeZoneId }}
            </p>
          </template>

          <template v-else-if="step === 'location'">
            <label>
              City
              <input
                v-model="form.city"
                name="city"
                :maxlength="limits.city"
              >
            </label>
            <p
              v-if="errors.city"
              class="field-error"
            >
              {{ errors.city }}
            </p>
            <label>
              Street address
              <input
                v-model="form.addressLine"
                name="addressLine"
                :maxlength="limits.addressLine"
              >
            </label>
            <p class="hint">
              Guests see the city when browsing; the street address is only sent with their booking confirmation.
            </p>
            <p
              v-if="errors.addressLine"
              class="field-error"
            >
              {{ errors.addressLine }}
            </p>
          </template>

          <template v-else-if="step === 'details'">
            <label>
              Description
              <textarea
                v-model="form.description"
                name="description"
                rows="5"
                :maxlength="limits.description"
              />
            </label>
            <p
              v-if="errors.description"
              class="field-error"
            >
              {{ errors.description }}
            </p>
            <label>
              Accessibility
              <textarea
                v-model="form.accessibilityNotes"
                name="accessibilityNotes"
                rows="3"
                :maxlength="limits.accessibilityNotes"
              />
            </label>
            <p class="hint">
              Describe entrances, lifts, toilets and anything else guests should know. It is shown to guests exactly as you write it.
            </p>
            <div
              class="label-suggestions"
              role="group"
              aria-label="Common access features"
            >
              <button
                v-for="label in accessibilityLabels"
                :key="label"
                type="button"
                class="secondary small"
                :disabled="hasAccessibilityLabel(form.accessibilityNotes, label)"
                :aria-label="`Add ${label} to accessibility notes`"
                @click="addAccessibilityLabel(label)"
              >
                + {{ label }}
              </button>
            </div>
            <p
              v-if="errors.accessibilityNotes"
              class="field-error"
            >
              {{ errors.accessibilityNotes }}
            </p>
          </template>

          <template v-else-if="step === 'rooms'">
            <p v-if="form.rooms.length === 0">
              Add each bookable room with the most people it can hold.
            </p>
            <div
              v-for="(room, index) in form.rooms"
              :key="room.id ?? `new-${index}`"
              class="room-row"
            >
              <label>
                Room name
                <input
                  v-model="room.name"
                  :name="`rooms.${index}.name`"
                  :maxlength="limits.roomName"
                >
              </label>
              <label>
                Capacity
                <input
                  v-model.number="room.capacity"
                  type="number"
                  min="1"
                  :max="limits.roomCapacity"
                  :name="`rooms.${index}.capacity`"
                >
              </label>
              <button
                type="button"
                class="secondary"
                @click="removeRoom(index)"
              >
                Remove
              </button>
              <p
                v-if="errors[`rooms.${index}.name`] || errors[`rooms.${index}.capacity`]"
                class="field-error"
              >
                {{ errors[`rooms.${index}.name`] ?? errors[`rooms.${index}.capacity`] }}
              </p>
            </div>
            <button
              type="button"
              class="secondary"
              :disabled="form.rooms.length >= limits.rooms"
              @click="addRoom"
            >
              Add room
            </button>
          </template>

          <template v-else-if="step === 'preview'">
            <article
              v-if="preview"
              class="listing-preview"
            >
              <h2>{{ preview.name }}</h2>
              <p class="hint">
                {{ preview.city ?? 'City not set' }} · {{ preview.timeZoneId }} · up to {{ preview.largestRoomCapacity }} people
              </p>
              <p>{{ preview.description ?? 'No description yet.' }}</p>
              <h3>Accessibility</h3>
              <p>{{ preview.accessibilityNotes ?? 'Not specified.' }}</p>
              <h3>Rooms</h3>
              <ul>
                <li
                  v-for="room in preview.rooms"
                  :key="room.id"
                >
                  {{ room.name }} — {{ room.capacity }} people
                </li>
              </ul>
            </article>
            <div
              v-if="preview && preview.missing.length > 0"
              class="notice"
            >
              Before submitting, add:
              <ul>
                <li
                  v-for="code in preview.missing"
                  :key="code"
                >
                  {{ missingLabels[code] ?? code }}
                </li>
              </ul>
            </div>
            <button
              type="button"
              :disabled="!preview?.readyToSubmit || saving"
              @click="submitForReview"
            >
              Submit for review
            </button>
          </template>
        </fieldset>

        <p
          v-if="failure"
          class="error"
          role="alert"
        >
          {{ failure }}
        </p>
        <p
          v-if="message"
          class="success"
          role="status"
        >
          {{ message }}
        </p>
        <button
          v-if="editable && step !== 'preview'"
          type="submit"
          :disabled="saving"
        >
          {{ saving ? 'Saving…' : isNew ? 'Create draft' : 'Save draft' }}
        </button>
      </form>
    </template>
  </section>
</template>
