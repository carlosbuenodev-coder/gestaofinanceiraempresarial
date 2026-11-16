import { createContext } from 'react'
import type { Credenciais, Usuario } from '../types/auth'
import type { ErrorCode } from '../services/api'

export interface AuthState {
  status: 'anonymous' | 'authenticating' | 'authenticated'
  usuario: Usuario | null
  notice: { code: ErrorCode; message: string } | null
}
export interface AuthContextValue extends AuthState {
  login: (credentials: Credenciais) => Promise<void>
  logout: () => void
  refreshSession: () => Promise<void>
}
export const AuthContext = createContext<AuthContextValue | null>(null)
