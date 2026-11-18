import { isLoginResponse, isUsuario } from '../types/auth'
import type { Credenciais, LoginResponse, Usuario } from '../types/auth'

export type ErrorCode = 'credentials' | 'expired' | 'forbidden' | 'network' | 'server' | 'unexpected' | 'cancelled' | 'validation' | 'conflict' | 'missing'
export interface MutationOptions { method?: 'POST' | 'PUT'; body?: unknown }
export class ApiError extends Error {
  constructor(public readonly code: ErrorCode, message: string, public readonly fields: Record<string, string> = {}) { super(message); this.name = 'ApiError' }
}
export const unexpected = () => new ApiError('unexpected', 'Não foi possível confirmar a resposta do sistema. Tente novamente.')

const testing = import.meta.env.MODE === 'testing'
const baseUrl = testing ? '' : (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5216').replace(/\/+$/, '')

export async function request(path: string, options: MutationOptions & { token?: string; signal?: AbortSignal } = {}): Promise<unknown> {
  const controller = new AbortController()
  const cancel = () => controller.abort()
  options.signal?.addEventListener('abort', cancel, { once: true })
  if (options.signal?.aborted) controller.abort()
  const timeout = window.setTimeout(cancel, 15_000)
  const method = options.method ?? (options.body ? 'POST' : 'GET')
  try {
    if (testing && (!path.startsWith('/') || new URL(path, window.location.origin).origin !== window.location.origin)) {
      throw unexpected()
    }
    const url = import.meta.env.DEV && path.startsWith('/lancamentos') ? path : baseUrl + path
    const response = await fetch(url, {
      method,
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
    if (response.status === 401) throw path === '/auth/usuarios/login'
      ? new ApiError('credentials', 'Não foi possível entrar. Confira a empresa, o e-mail e a senha.')
      : new ApiError('expired', 'Sua sessão expirou. Entre novamente para continuar.')
    if (response.status === 403) throw path === '/usuarios/me'
      ? new ApiError('forbidden', 'Seu acesso não está autorizado. Verifique sua situação com o administrador da empresa.')
      : new ApiError('forbidden', 'Você não tem permissão para esta operação. Verifique seu acesso com o administrador da empresa.')
    if (response.status >= 500) throw new ApiError('server', 'O sistema está temporariamente indisponível. Tente novamente em instantes.')
    if (response.status === 404 && path.startsWith('/lancamentos')) throw new ApiError('missing', 'Lançamento não encontrado ou indisponível para sua empresa.')
    if (response.status === 409 && path.split('?', 1)[0] === '/fluxo-caixa/resumo') {
      throw new ApiError('conflict', 'Há uma inconsistência nos valores previstos deste período. Verifique os lançamentos e as baixas antes de consultar novamente.')
    }
    if (response.status === 409 && path.startsWith('/lancamentos')) {
      const endpoint = path.split('?', 1)[0]
      if (method === 'GET' && /^\/lancamentos\/\d+\/saldo$/.test(endpoint)) {
        throw new ApiError('conflict', 'Não foi possível exibir o saldo. Os dados financeiros deste lançamento apresentam uma inconsistência e precisam de verificação.')
      }
      if (method === 'PUT' && /^\/lancamentos\/\d+$/.test(endpoint)) {
        throw new ApiError('conflict', 'A situação atual impede a edição. Atualize os detalhes. A edição exige um lançamento aberto sem baixas, inclusive estornadas.')
      }
      if (method === 'POST' && /^\/lancamentos\/\d+\/cancelamento$/.test(endpoint)) {
        throw new ApiError('conflict', 'A situação atual impede o cancelamento. O cancelamento exige um lançamento aberto sem baixas, inclusive estornadas.')
      }
      throw new ApiError('conflict', 'A situação atual impede esta operação. Consulte os dados atualizados antes de tentar novamente.')
    }
    if (response.status === 400 && path.startsWith('/lancamentos')) {
      const fields: Record<string, string> = {}
      try {
        const problem = await response.json()
        const allowed = ['tipo', 'descricao', 'categoriaId', 'clienteId', 'fornecedorId', 'centroCustoId', 'dataEmissao', 'dataVencimento', 'valor', 'observacoes']
        if (problem?.errors && typeof problem.errors === 'object') for (const key of Object.keys(problem.errors)) {
          const field = key.charAt(0).toLowerCase() + key.slice(1)
          if (allowed.includes(field)) fields[field] = 'Confira o valor informado e sua validade para a empresa e o tipo de lançamento.'
        }
      } catch { /* Never display untrusted server bodies. */ }
      throw new ApiError('validation', 'Revise os campos informados. As referências devem pertencer à empresa autenticada.', fields)
    }
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
