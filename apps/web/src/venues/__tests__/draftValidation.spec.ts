import { describe, expect, it } from 'vitest'
import type { VenueDraftInput } from '@/api/hostVenues'
import { limits, toInput, validateAll, validateStep } from '@/venues/draftValidation'

function input(overrides: Partial<VenueDraftInput> = {}): VenueDraftInput {
  return {
    name: 'Riverside Makers Studio',
    timeZoneId: 'Europe/Madrid',
    description: '',
    city: '',
    addressLine: '',
    accessibilityNotes: '',
    rooms: [],
    ...overrides,
  }
}

describe('validateStep', () => {
  it('requires a name and a known timezone on the basics step', () => {
    expect(validateStep('basics', input())).toEqual({})
    expect(validateStep('basics', input({ name: '  ', timeZoneId: '' }))).toEqual({
      name: 'Give the venue a name.',
      timeZoneId: 'Choose the timezone the venue is in.',
    })
    expect(validateStep('basics', input({ timeZoneId: 'Mars/Base' }), ['Europe/Madrid'])).toHaveProperty('timeZoneId')
  })

  it('lets optional fields stay empty but enforces the API length limits', () => {
    expect(validateStep('location', input())).toEqual({})
    expect(validateStep('details', input({ description: 'x'.repeat(limits.description + 1) }))).toEqual({
      description: `At most ${limits.description} characters.`,
    })
  })

  it('checks room names and capacities by index', () => {
    const errors = validateStep(
      'rooms',
      input({
        rooms: [
          { id: null, name: 'Loft', capacity: 10 },
          { id: null, name: ' loft ', capacity: 0 },
          { id: null, name: '', capacity: 2.5 },
        ],
      }),
    )

    expect(errors).toEqual({
      'rooms.1.name': 'Each room needs a different name.',
      'rooms.1.capacity': `Capacity must be a whole number from 1 to ${limits.roomCapacity}.`,
      'rooms.2.name': 'Name the room.',
      'rooms.2.capacity': `Capacity must be a whole number from 1 to ${limits.roomCapacity}.`,
    })
  })
})

describe('validateAll', () => {
  it('combines the errors of every step', () => {
    const errors = validateAll(input({ name: '', rooms: [{ id: null, name: 'A', capacity: 5000 }] }))

    expect(Object.keys(errors).sort()).toEqual(['name', 'rooms.0.capacity'])
  })
})

describe('toInput', () => {
  it('turns null API fields into empty form fields', () => {
    const form = toInput({
      id: 'v1',
      name: 'Studio',
      timeZoneId: 'UTC',
      description: null,
      city: 'Madrid',
      addressLine: null,
      accessibilityNotes: null,
      status: 'Draft',
      revision: 3,
      rooms: [{ id: 'r1', name: 'Hall', capacity: 12 }],
      missing: ['description', 'address_line'],
    })

    expect(form).toEqual({
      name: 'Studio',
      timeZoneId: 'UTC',
      description: '',
      city: 'Madrid',
      addressLine: '',
      accessibilityNotes: '',
      rooms: [{ id: 'r1', name: 'Hall', capacity: 12 }],
    })
  })
})
