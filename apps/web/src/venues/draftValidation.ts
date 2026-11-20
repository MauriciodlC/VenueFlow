import type { VenueDraft, VenueDraftInput } from '@/api/hostVenues'

/** Same limits as the API domain model (services/api/src/VenueFlow.Domain/Venues). */
export const limits = {
  name: 200,
  description: 2000,
  city: 120,
  addressLine: 200,
  accessibilityNotes: 1000,
  roomName: 120,
  roomCapacity: 1000,
  rooms: 50,
} as const

export type WizardStep = 'basics' | 'location' | 'details' | 'rooms'

export const wizardSteps: { id: WizardStep; label: string }[] = [
  { id: 'basics', label: 'Basics' },
  { id: 'location', label: 'Location' },
  { id: 'details', label: 'Description & access' },
  { id: 'rooms', label: 'Rooms' },
]

/** Field key → message. Room fields use `rooms.<index>.name` / `rooms.<index>.capacity`. */
export type FieldErrors = Record<string, string>

/** Labels for the API's "missing before submission" codes. */
export const missingLabels: Record<string, string> = {
  description: 'Description',
  city: 'City',
  address_line: 'Street address',
  rooms: 'At least one room',
}

export function toInput(draft: VenueDraft): VenueDraftInput {
  return {
    name: draft.name,
    timeZoneId: draft.timeZoneId,
    description: draft.description ?? '',
    city: draft.city ?? '',
    addressLine: draft.addressLine ?? '',
    accessibilityNotes: draft.accessibilityNotes ?? '',
    rooms: draft.rooms.map((room) => ({ id: room.id, name: room.name, capacity: room.capacity })),
  }
}

function tooLong(value: string, max: number) {
  return value.trim().length > max ? `At most ${max} characters.` : null
}

/**
 * Checks one wizard step. Optional fields may stay empty while drafting; the
 * API reports what is still missing before the listing can be submitted.
 */
export function validateStep(step: WizardStep, input: VenueDraftInput, knownTimeZones?: readonly string[]): FieldErrors {
  const errors: FieldErrors = {}
  const set = (field: string, message: string | null) => {
    if (message) errors[field] = message
  }

  switch (step) {
    case 'basics':
      set('name', input.name.trim() === '' ? 'Give the venue a name.' : tooLong(input.name, limits.name))
      if (input.timeZoneId.trim() === '') set('timeZoneId', 'Choose the timezone the venue is in.')
      else if (knownTimeZones && !knownTimeZones.includes(input.timeZoneId)) set('timeZoneId', 'Choose a timezone from the list.')
      break
    case 'location':
      set('city', tooLong(input.city, limits.city))
      set('addressLine', tooLong(input.addressLine, limits.addressLine))
      break
    case 'details':
      set('description', tooLong(input.description, limits.description))
      set('accessibilityNotes', tooLong(input.accessibilityNotes, limits.accessibilityNotes))
      break
    case 'rooms': {
      if (input.rooms.length > limits.rooms) set('rooms', `At most ${limits.rooms} rooms.`)
      const seen = new Set<string>()
      input.rooms.forEach((room, index) => {
        const name = room.name.trim()
        const key = name.toLowerCase()
        if (name === '') set(`rooms.${index}.name`, 'Name the room.')
        else if (seen.has(key)) set(`rooms.${index}.name`, 'Each room needs a different name.')
        else set(`rooms.${index}.name`, tooLong(name, limits.roomName))
        seen.add(key)
        if (!Number.isInteger(room.capacity) || room.capacity < 1 || room.capacity > limits.roomCapacity) {
          set(`rooms.${index}.capacity`, `Capacity must be a whole number from 1 to ${limits.roomCapacity}.`)
        }
      })
      break
    }
  }

  return errors
}

export function validateAll(input: VenueDraftInput, knownTimeZones?: readonly string[]): FieldErrors {
  return Object.assign({}, ...wizardSteps.map((step) => validateStep(step.id, input, knownTimeZones)))
}

/** IANA zones the browser knows, or a short fallback list on older engines. */
export function availableTimeZones(): string[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: 'timeZone') => string[] }
  const zones = intl.supportedValuesOf?.('timeZone') ?? []
  return zones.length > 0 ? zones : ['America/Bogota', 'America/New_York', 'Europe/London', 'Europe/Madrid', 'UTC']
}
