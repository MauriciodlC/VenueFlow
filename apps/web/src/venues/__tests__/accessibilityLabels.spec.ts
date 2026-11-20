import { describe, expect, it } from 'vitest'
import { accessibilityLabels, appendAccessibilityLabel, hasAccessibilityLabel } from '@/venues/accessibilityLabels'

describe('appendAccessibilityLabel', () => {
  it('starts empty notes with the label as a sentence', () => {
    expect(appendAccessibilityLabel('', 'Step-free entrance')).toBe('Step-free entrance.')
    expect(appendAccessibilityLabel('   ', 'Hearing loop')).toBe('Hearing loop.')
  })

  it('adds a full stop to the existing text when it lacks one', () => {
    expect(appendAccessibilityLabel('Ramp at the side door', 'Accessible toilet')).toBe(
      'Ramp at the side door. Accessible toilet.',
    )
    expect(appendAccessibilityLabel('Ramp at the side door! ', 'Accessible toilet')).toBe(
      'Ramp at the side door! Accessible toilet.',
    )
  })

  it('does not add a label twice, whatever the case', () => {
    const notes = 'step-free entrance from the street.'
    expect(hasAccessibilityLabel(notes, 'Step-free entrance')).toBe(true)
    expect(appendAccessibilityLabel(notes, 'Step-free entrance')).toBe(notes)
  })

  it('keeps the notes unchanged when the label would not fit', () => {
    const notes = 'x'.repeat(995)
    expect(appendAccessibilityLabel(notes, 'Hearing loop')).toBe(notes)
    expect(appendAccessibilityLabel('Lift', 'Hearing loop', 20)).toBe('Lift. Hearing loop.')
  })

  it('offers distinct labels', () => {
    const lower = accessibilityLabels.map((label) => label.toLowerCase())
    expect(new Set(lower).size).toBe(accessibilityLabels.length)
  })
})
