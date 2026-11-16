export type PerfilEmpresa = 'ADMIN' | 'FINANCEIRO' | 'CONSULTA'
export interface Usuario {
  id: number
  empresaId: number
  nome: string
  email: string
  perfil: PerfilEmpresa
  ativo: boolean
}
export interface Credenciais {
  empresaId: number
  email: string
  senha: string
}
export interface LoginResponse {
  token: string
  tipo: 'Bearer'
  expiraEmUtc: string
  usuario: Usuario
}
export const perfis: Record<PerfilEmpresa, string> = {
  ADMIN: 'Administrador', FINANCEIRO: 'Financeiro', CONSULTA: 'Consulta',
}
export function isUsuario(value: unknown): value is Usuario {
  if (!value || typeof value !== 'object') return false
  const u = value as Record<string, unknown>
  return Number.isSafeInteger(u.id) && Number(u.id) > 0 && Number.isSafeInteger(u.empresaId) && Number(u.empresaId) > 0 &&
    typeof u.nome === 'string' && !!u.nome.trim() && typeof u.email === 'string' &&
    (u.perfil === 'ADMIN' || u.perfil === 'FINANCEIRO' || u.perfil === 'CONSULTA') && u.ativo === true
}
export function isLoginResponse(value: unknown): value is LoginResponse {
  if (!value || typeof value !== 'object') return false
  const r = value as Record<string, unknown>
  return typeof r.token === 'string' && !!r.token.trim() && r.tipo === 'Bearer' &&
    typeof r.expiraEmUtc === 'string' && Number.isFinite(Date.parse(r.expiraEmUtc)) && isUsuario(r.usuario)
}
