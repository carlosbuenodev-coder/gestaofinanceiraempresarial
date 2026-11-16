import { useCallback, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi, ApiError, unexpected } from '../services/api'
import type { Credenciais, Usuario } from '../types/auth'
import { AuthContext } from './auth-context'
import type { AuthState } from './auth-context'

const initial: AuthState = { status: 'anonymous', usuario: null, notice: null }
interface Session { token: string; expiresAt: number; usuario: Usuario }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(initial)
  const session = useRef<Session | null>(null)
  const generation = useRef(0)
  const pending = useRef<AbortController | null>(null)
  const mounted = useRef(true)
  const clear = useCallback((notice: AuthState['notice'] = null) => {
    generation.current++
    pending.current?.abort()
    pending.current = null
    session.current = null
    if (mounted.current) setState({ ...initial, notice })
  }, [])
  const logout = useCallback(() => clear(), [clear])
  const login = useCallback(async (credentials: Credenciais) => {
    clear()
    const attempt = generation.current
    const controller = new AbortController()
    pending.current = controller
    setState({ ...initial, status: 'authenticating' })
    try {
      const response = await authApi.login(credentials, controller.signal)
      const expiresAt = Date.parse(response.expiraEmUtc)
      if (expiresAt <= Date.now()) throw new ApiError('expired', 'Sua sessão expirou. Entre novamente para continuar.')
      if (attempt !== generation.current || !mounted.current) return
      const usuario = await authApi.me(response.token, controller.signal)
      if (expiresAt <= Date.now()) throw new ApiError('expired', 'Sua sessão expirou. Entre novamente para continuar.')
      if (usuario.id !== response.usuario.id || usuario.empresaId !== credentials.empresaId ||
          usuario.empresaId !== response.usuario.empresaId) throw unexpected()
      if (attempt !== generation.current || !mounted.current) return
      session.current = { token: response.token, expiresAt, usuario }
      pending.current = null
      setState({ status: 'authenticated', usuario, notice: null })
    } catch (error) {
      if (attempt !== generation.current || !mounted.current) return
      const failure = error instanceof ApiError ? error : unexpected()
      clear({ code: failure.code, message: failure.message })
      throw failure
    }
  }, [clear])
  const refreshSession = useCallback(async () => {
    const current = session.current
    if (!current) return
    if (current.expiresAt <= Date.now()) {
      clear({ code: 'expired', message: 'Sua sessão expirou. Entre novamente para continuar.' })
      return
    }
    const attempt = generation.current
    try {
      const usuario = await authApi.me(current.token)
      if (attempt !== generation.current || !mounted.current) return
      if (usuario.id !== current.usuario.id || usuario.empresaId !== current.usuario.empresaId) throw unexpected()
      session.current = { ...current, usuario }
      setState({ status: 'authenticated', usuario, notice: null })
    } catch (error) {
      if (attempt !== generation.current || !mounted.current) return
      const failure = error instanceof ApiError ? error : unexpected()
      // Failed identity verification never leaves protected user details visible.
      clear({ code: failure.code, message: failure.message })
    }
  }, [clear])
  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false; generation.current++; pending.current?.abort(); session.current = null }
  }, [])
  useEffect(() => {
    if (state.status !== 'authenticated') return
    const expire = () => clear({ code: 'expired', message: 'Sua sessão expirou. Entre novamente para continuar.' })
    const timer = window.setTimeout(expire, Math.min(Math.max(0, (session.current?.expiresAt ?? 0) - Date.now()), 2_147_483_647))
    const onVisibility = () => { if (document.visibilityState === 'visible') void refreshSession() }
    window.addEventListener('focus', refreshSession)
    document.addEventListener('visibilitychange', onVisibility)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('focus', refreshSession)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [state.status, clear, refreshSession])
  return <AuthContext.Provider value={{ ...state, login, logout, refreshSession }}>{children}</AuthContext.Provider>
}
