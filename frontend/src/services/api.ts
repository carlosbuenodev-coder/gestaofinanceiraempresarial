import { isLoginResponse, isUsuario } from '../types/auth'
import type { Credenciais, LoginResponse, Usuario } from '../types/auth'

export type ErrorCode = 'credentials' | 'expired' | 'forbidden' | 'network' | 'server' | 'unexpected' | 'cancelled'
export class ApiError extends Error {
  constructor(public readonly code: ErrorCode, message: string) { super(message); this.name = 'ApiError' }
}
export const unexpected = () => new ApiError('unexpected', 'Não foi possível confirmar a resposta do sistema. Tente novamente.')

const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5216').replace(/\/+$/, '')

async function request(path: string, options: { body?: Credenciais; token?: string; signal?: AbortSignal } = {}): Promise<unknown> {
  const controller = new AbortController()
  const cancel = () => controller.abort()
  options.signal?.addEventListener('abort', cancel, { once: true })
  if (options.signal?.aborted) controller.abort()
  const timeout = window.setTimeout(cancel, 15_000)
  try {
    const response = await fetch(baseUrl + path, {
      method: options.body ? 'POST' : 'GET',
      headers: {
        Accept: 'application/json',
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
      },
      ...(options.body ? { body: JSON.stringify(options.body) } : {}),
      signal: controller.signal,
      credentials: 'omit',
      cache: 'no-store',
      redirect: 'error',
      referrerPolicy: 'no-referrer',
    })
    if (response.status === 401) throw options.body
      ? new ApiError('credentials', 'Não foi possível entrar. Confira a empresa, o e-mail e a senha.')
      : new ApiError('expired', 'Sua sessão expirou. Entre novamente para continuar.')
    if (response.status === 403) throw new ApiError('forbidden', 'Seu acesso não está autorizado. Verifique sua situação com o administrador da empresa.')
    if (response.status >= 500) throw new ApiError('server', 'O sistema está temporariamente indisponível. Tente novamente em instantes.')
    if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw unexpected()
    try { return await response.json() }
    catch { throw unexpected() }
  } catch (error) {
    if (options.signal?.aborted) throw new ApiError('cancelled', 'Solicitação cancelada.')
    if (error instanceof ApiError) throw error
    throw new ApiError('network', 'Não foi possível conectar ao sistema. Verifique sua conexão e tente novamente.')
  } finally {
    window.clearTimeout(timeout)
    options.signal?.removeEventListener('abort', cancel)
  }
}
export const authApi = {
  async login(credentials: Credenciais, signal?: AbortSignal): Promise<LoginResponse> {
    const result = await request('/auth/usuarios/login', { body: credentials, signal })
    if (!isLoginResponse(result)) throw unexpected()
    return result
  },
  async me(token: string, signal?: AbortSignal): Promise<Usuario> {
    const result = await request('/usuarios/me', { token, signal })
    if (!isUsuario(result)) throw unexpected()
    return result
  },
}
