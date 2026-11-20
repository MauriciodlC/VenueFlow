import { apiRequest, type ApiResponse } from './client'

export type VenueStatus = 'Draft' | 'InReview' | 'Published'

export interface Room {
  id: string
  name: string
  capacity: number
}

export interface VenueSummary {
  id: string
  name: string
  city: string | null
  status: VenueStatus
  roomCount: number
  revision: number
}

export interface VenueDraft {
  id: string
  name: string
  timeZoneId: string
  description: string | null
  city: string | null
  addressLine: string | null
  accessibilityNotes: string | null
  status: VenueStatus
  revision: number
  rooms: Room[]
  /** Field codes still needed before submission: description, city, address_line, rooms. */
  missing: string[]
}

export interface VenuePreview {
  id: string
  name: string
  description: string | null
  city: string | null
  timeZoneId: string
  accessibilityNotes: string | null
  largestRoomCapacity: number
  rooms: Room[]
  status: VenueStatus
  readyToSubmit: boolean
  missing: string[]
}

export interface RoomInput {
  id: string | null
  name: string
  capacity: number
}

export interface VenueDraftInput {
  name: string
  timeZoneId: string
  description: string
  city: string
  addressLine: string
  accessibilityNotes: string
  rooms: RoomInput[]
}

const base = '/api/host/venues'

export async function listHostVenues(): Promise<VenueSummary[]> {
  return (await apiRequest<VenueSummary[]>(base)).data
}

export function createVenueDraft(name: string, timeZoneId: string): Promise<ApiResponse<VenueDraft>> {
  return apiRequest<VenueDraft>(base, { method: 'POST', body: { name, timeZoneId } })
}

export function getVenueDraft(id: string): Promise<ApiResponse<VenueDraft>> {
  return apiRequest<VenueDraft>(`${base}/${id}`)
}

/** Saves the whole draft. `etag` must be the one from the last read or save. */
export function updateVenueDraft(id: string, input: VenueDraftInput, etag: string): Promise<ApiResponse<VenueDraft>> {
  return apiRequest<VenueDraft>(`${base}/${id}`, { method: 'PUT', body: input, ifMatch: etag })
}

export async function previewVenue(id: string): Promise<VenuePreview> {
  return (await apiRequest<VenuePreview>(`${base}/${id}/preview`)).data
}

export function submitVenue(id: string, etag: string): Promise<ApiResponse<VenueDraft>> {
  return apiRequest<VenueDraft>(`${base}/${id}/submit`, { method: 'POST', ifMatch: etag })
}
