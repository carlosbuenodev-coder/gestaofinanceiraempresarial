import { createContext } from 'react'
import type { Credenciais, Usuario } from '../types/auth'
import type { ErrorCode, MutationOptions } from '../services/api'

export interface AuthState {
  status: 'anonymous' | 'authenticating' | 'authenticated'
  usuario: Usuario | null
  notice: { code: ErrorCode; message: string } | null
}
export interface AuthContextValue extends AuthState {
  login: (credentials: Credenciais) => Promise<void>
  logout: () => void
  refreshSession: () => Promise<void>
  authorizedRequest: (path: string, signal?: AbortSignal, options?: MutationOptions) => Promise<unknown>
}
export const AuthContext = createContext<AuthContextValue | null>(null)
