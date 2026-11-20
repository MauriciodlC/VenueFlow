import { limits } from '@/venues/draftValidation'

/**
 * Suggested wording for common access features. Accessibility notes stay free
 * text (guests see exactly what the host wrote); these labels only make the
 * usual cases quick to add and consistent between listings. The meaning of
 * each label is described in docs/hosts/onboarding.md.
 */
export const accessibilityLabels = [
  'Step-free entrance',
  'Lift to all floors',
  'Accessible toilet',
  'Hearing loop',
  'Seating with back and arm rests',
  'Quiet room available',
  'Assistance dogs welcome',
  'Accessible parking nearby',
] as const

export function hasAccessibilityLabel(notes: string, label: string): boolean {
  return notes.toLocaleLowerCase().includes(label.toLocaleLowerCase())
}

/**
 * Appends `label` as its own sentence. Returns `notes` unchanged when the
 * label is already there or the result would exceed the field limit.
 */
export function appendAccessibilityLabel(notes: string, label: string, max: number = limits.accessibilityNotes): string {
  if (hasAccessibilityLabel(notes, label)) return notes

  const trimmed = notes.trim()
  const sentence = `${label}.`
  const next = trimmed === '' ? sentence : `${trimmed}${/[.!?]$/.test(trimmed) ? '' : '.'} ${sentence}`
  return next.length > max ? notes : next
}
