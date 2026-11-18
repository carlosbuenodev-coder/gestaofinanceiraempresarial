import { validDate } from '../utils/format'
export type EntryType = 'RECEITA' | 'DESPESA'
export const statuses = { ABERTO: 'Aberto', PARCIAL: 'Parcial', QUITADO: 'Quitado', CANCELADO: 'Cancelado' } as const
export type EntryStatus = keyof typeof statuses
export interface Entry {
  id: number; empresaId: number; tipo: EntryType; descricao: string; clienteId: number | null
  fornecedorId: number | null; categoriaId: number; centroCustoId: number | null
  dataEmissao: string; dataVencimento: string; valor: number; status: EntryStatus; observacoes: string | null; criadoEm: string | null
}
export interface EntryPage { itens: Entry[]; total: number; pagina: number; tamanhoPagina: number; totalPaginas: number }
export interface EntryBalance { lancamentoId: number; empresaId: number; valorOriginal: number; totalBaixado: number; saldoRestante: number; status: EntryStatus }
export interface EntryPayload {
  tipo: EntryType; descricao: string; categoriaId: number; clienteId: number | null; fornecedorId: number | null
  centroCustoId: number | null; dataEmissao: string; dataVencimento: string; valor: number; observacoes: string | null
}
export interface EntryFilters {
  tipo: EntryType | ''; status: EntryStatus | ''; vencimentoInicio: string; vencimentoFim: string
  categoriaId: string; clienteId: string; fornecedorId: string; centroCustoId: string
  ordenarPor: 'id' | 'dataVencimento' | 'dataEmissao' | 'valor'; direcao: 'asc' | 'desc'; tamanhoPagina: string
}
export const emptyFilters: EntryFilters = { tipo: '', status: '', vencimentoInicio: '', vencimentoFim: '', categoriaId: '', clienteId: '', fornecedorId: '', centroCustoId: '', ordenarPor: 'dataVencimento', direcao: 'asc', tamanhoPagina: '20' }
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object'
export const positiveId = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) > 0
const count = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) >= 0
const idOrNull = (v: unknown) => v === null || positiveId(v)
const amount = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v)
const status = (v: unknown): v is EntryStatus => typeof v === 'string' && Object.hasOwn(statuses, v)
const date = (v: unknown): v is string => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?$/.test(v) && validDate(v.slice(0, 10)) && Number.isFinite(Date.parse(v))
export function isEntry(v: unknown): v is Entry {
  return obj(v) && positiveId(v.id) && positiveId(v.empresaId) && (v.tipo === 'RECEITA' || v.tipo === 'DESPESA') && typeof v.descricao === 'string' && idOrNull(v.clienteId) && idOrNull(v.fornecedorId) && positiveId(v.categoriaId) && idOrNull(v.centroCustoId) && date(v.dataEmissao) && date(v.dataVencimento) && amount(v.valor) && status(v.status) && (v.observacoes === null || typeof v.observacoes === 'string') && (v.criadoEm === null || date(v.criadoEm))
}
export function isEntryPage(v: unknown): v is EntryPage {
  return obj(v) && count(v.total) && positiveId(v.pagina) && positiveId(v.tamanhoPagina) && v.tamanhoPagina <= 100 && count(v.totalPaginas) && v.totalPaginas === Math.ceil(v.total / v.tamanhoPagina) && Array.isArray(v.itens) && v.itens.every(isEntry) && v.itens.length === Math.min(v.tamanhoPagina, Math.max(0, v.total - (v.pagina - 1) * v.tamanhoPagina))
}
export function isEntryBalance(v: unknown): v is EntryBalance {
  return obj(v) && positiveId(v.lancamentoId) && positiveId(v.empresaId) && amount(v.valorOriginal) && amount(v.totalBaixado) && amount(v.saldoRestante) && status(v.status)
}
