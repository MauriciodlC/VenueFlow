import { apiRequest } from './client'

export type Role = 'guest' | 'host' | 'staff' | 'admin'

export interface SessionUser {
  id: string
  email: string
  displayName: string
  role: Role
}

export async function login(email: string, password: string): Promise<SessionUser> {
  return (await apiRequest<SessionUser>('/api/auth/login', { method: 'POST', body: { email, password } })).data
}

export async function logout(): Promise<void> {
  await apiRequest<void>('/api/auth/logout', { method: 'POST' })
}

export async function currentUser(): Promise<SessionUser> {
  return (await apiRequest<SessionUser>('/api/auth/me')).data
}
