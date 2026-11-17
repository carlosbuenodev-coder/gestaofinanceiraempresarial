import { validDate } from '../utils/format'
export interface Totals {
  receitasPrevistas: number; despesasPrevistas: number; receitasRealizadas: number; despesasRealizadas: number
  saldoPrevisto: number; saldoRealizado: number
}
export interface Resumo extends Totals {
  empresaId: number; dataInicial: string; dataFinal: string; valoresPendentes: number
  quantidadeLancamentos: number; quantidadeBaixasValidas: number
}
export interface Periodo extends Totals { data: string }
export interface Conta { contaBancariaId: number; receitasRealizadas: number; despesasRealizadas: number; saldoRealizado: number; saldoBancarioAcumulado: number | null }
export interface Movimento { lancamentoId: number; baixaId: number | null; contaBancariaId: number | null; tipo: 'RECEITA' | 'DESPESA'; descricao: string; categoriaId: number; centroCustoId: number | null; data: string; principal: number; juros: number; multa: number; desconto: number }
export interface Pagina<T> { empresaId: number; pagina: number; tamanhoPagina: number; total: number; itens: T[] }
export interface Dashboard { resumo: Resumo; porPeriodo: Pagina<Periodo>; porConta: Pagina<Conta> }
type Guard<T> = (value: unknown) => value is T
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object'
const numeric = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v)
const count = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) >= 0
const id = (v: unknown): v is number => count(v) && v > 0
const nullableId = (v: unknown) => v === null || id(v)
const fields = (v: Record<string, unknown>, keys: string[]) => keys.every(k => numeric(v[k]))
const totalKeys = ['receitasPrevistas', 'despesasPrevistas', 'receitasRealizadas', 'despesasRealizadas', 'saldoPrevisto', 'saldoRealizado']
export const isResumo: Guard<Resumo> = (v): v is Resumo => obj(v) && id(v.empresaId) && typeof v.dataInicial === 'string' && validDate(v.dataInicial) && typeof v.dataFinal === 'string' && validDate(v.dataFinal) && fields(v, [...totalKeys, 'valoresPendentes']) && count(v.quantidadeLancamentos) && count(v.quantidadeBaixasValidas)
const isData = (v: unknown) => typeof v === 'string' && validDate(v.slice(0, 10)) && /^\d{4}-\d{2}-\d{2}T/.test(v)
export const isPeriodo: Guard<Periodo> = (v): v is Periodo => obj(v) && isData(v.data) && fields(v, totalKeys)
export const isConta: Guard<Conta> = (v): v is Conta => obj(v) && id(v.contaBancariaId) && fields(v, ['receitasRealizadas', 'despesasRealizadas', 'saldoRealizado']) && (v.saldoBancarioAcumulado === null || numeric(v.saldoBancarioAcumulado))
export const isMovimento: Guard<Movimento> = (v): v is Movimento => obj(v) && id(v.lancamentoId) && nullableId(v.baixaId) && nullableId(v.contaBancariaId) && id(v.categoriaId) && nullableId(v.centroCustoId) && (v.tipo === 'RECEITA' || v.tipo === 'DESPESA') && typeof v.descricao === 'string' && isData(v.data) && fields(v, ['principal', 'juros', 'multa', 'desconto'])
export function isPagina<T>(v: unknown, guard: Guard<T>): v is Pagina<T> {
  return obj(v) && id(v.empresaId) && id(v.pagina) && id(v.tamanhoPagina) && v.tamanhoPagina <= 100 && count(v.total) && Array.isArray(v.itens) && v.itens.every(guard) && v.itens.length === Math.min(v.tamanhoPagina, Math.max(0, v.total - (v.pagina - 1) * v.tamanhoPagina))
}
export const isDashboard: Guard<Dashboard> = (v): v is Dashboard => obj(v) && isResumo(v.resumo) && isPagina(v.porPeriodo, isPeriodo) && isPagina(v.porConta, isConta)
