import { unexpected } from './api'
import { isDashboard, isResumo, isPagina, isPeriodo, isConta, isMovimento } from '../types/cashflow'
import type { Pagina, Periodo, Conta, Movimento, Dashboard } from '../types/cashflow'
import type { DateRange } from '../utils/format'
export type AuthorizedRequest = (path: string, signal?: AbortSignal) => Promise<unknown>
export interface CashflowFilters extends DateRange { tipo?: 'RECEITA' | 'DESPESA' }

export function cashflowApi(request: AuthorizedRequest, empresaId: number, range: CashflowFilters, signal?: AbortSignal) {
  const query = new URLSearchParams({ dataInicial: range.dataInicial, dataFinal: range.dataFinal, tamanhoPagina: '100' })
  if (range.tipo) query.set('tipo', range.tipo)
  const get = (endpoint: string, pagina = 1) => request(`/fluxo-caixa/${endpoint}?${query}&pagina=${pagina}`, signal)
  const scope = (empresa: number) => { if (empresa !== empresaId) throw unexpected() }
  async function page<T>(endpoint: string, guard: (v: unknown) => v is T, pagina = 1): Promise<Pagina<T>> {
    const result = await get(endpoint, pagina)
    if (!isPagina(result, guard) || result.pagina !== pagina || result.tamanhoPagina !== 100) throw unexpected()
    scope(result.empresaId)
    return result
  }
  async function complete<T>(endpoint: string, first: Pagina<T>, guard: (v: unknown) => v is T): Promise<Pagina<T>> {
    scope(first.empresaId)
    if (first.pagina !== 1 || first.tamanhoPagina !== 100) throw unexpected()
    const itens = [...first.itens]
    // Sequential pagination bounds concurrency and preserves the backend's official aggregates.
    for (let n = 2; n <= Math.ceil(first.total / 100); n++) {
      const next = await page(endpoint, guard, n)
      if (next.total !== first.total) throw unexpected()
      itens.push(...next.itens)
    }
    return { ...first, itens }
  }
  return {
    async dashboard(): Promise<Dashboard> {
      const result = await get('dashboard')
      if (!isDashboard(result)) throw unexpected()
      scope(result.resumo.empresaId)
      if (result.resumo.dataInicial !== range.dataInicial || result.resumo.dataFinal !== range.dataFinal) throw unexpected()
      const porPeriodo = await complete<Periodo>('por-periodo', result.porPeriodo, isPeriodo)
      const porConta = await complete<Conta>('por-conta', result.porConta, isConta)
      if (porPeriodo.itens.some(p => p.data.slice(0, 10) < range.dataInicial || p.data.slice(0, 10) > range.dataFinal)) throw unexpected()
      return { resumo: result.resumo, porPeriodo, porConta }
    },
    async resumo() {
      const result = await get('resumo')
      if (!isResumo(result)) throw unexpected()
      scope(result.empresaId)
      if (result.dataInicial !== range.dataInicial || result.dataFinal !== range.dataFinal) throw unexpected()
      return result
    },
    movimentos: (tipo: 'previsto' | 'realizado', pagina = 1) => page<Movimento>(tipo, isMovimento, pagina),
    periodos: (pagina = 1) => page<Periodo>('por-periodo', isPeriodo, pagina),
    contas: (pagina = 1) => page<Conta>('por-conta', isConta, pagina),
    async realizadoPorConta(): Promise<Conta[]> {
      const first = await page('por-conta', isConta)
      return (await complete('por-conta', first, isConta)).itens
    },
  }
}
