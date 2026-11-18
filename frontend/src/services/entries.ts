import type { MutationOptions } from './api'
import { unexpected } from './api'
import { isEntry, isEntryPage, isEntryBalance, positiveId } from '../types/entries'
import type { Entry, EntryFilters, EntryPayload } from '../types/entries'
export type EntryRequest = (path: string, signal?: AbortSignal, options?: MutationOptions) => Promise<unknown>
export function entriesApi(request: EntryRequest, empresaId: number) {
  function id(value: number) { if (!positiveId(value)) throw unexpected(); return value }
  function entry(value: unknown, expectedId?: number): Entry {
    if (!isEntry(value) || value.empresaId !== empresaId || (expectedId !== undefined && value.id !== expectedId)) throw unexpected()
    return value
  }
  function payload(input: EntryPayload, create: boolean) {
    // Explicit whitelist: company, status and financial settlement fields never come from the client.
    return { ...(create ? { tipo: input.tipo } : {}), descricao: input.descricao, categoriaId: input.categoriaId,
      clienteId: input.clienteId, fornecedorId: input.fornecedorId, centroCustoId: input.centroCustoId,
      dataEmissao: input.dataEmissao, dataVencimento: input.dataVencimento, valor: input.valor, observacoes: input.observacoes }
  }
  return {
    async list(filters: EntryFilters, pagina: number, signal?: AbortSignal) {
      const query = new URLSearchParams({ pagina: String(id(pagina)), tamanhoPagina: filters.tamanhoPagina, ordenarPor: filters.ordenarPor, direcao: filters.direcao })
      for (const key of ['tipo', 'status', 'vencimentoInicio', 'vencimentoFim', 'categoriaId', 'clienteId', 'fornecedorId', 'centroCustoId'] as const) if (filters[key]) query.set(key, filters[key])
      const result = await request(`/lancamentos?${query}`, signal)
      if (!isEntryPage(result) || result.pagina !== pagina || result.tamanhoPagina !== Number(filters.tamanhoPagina)) throw unexpected()
      for (const item of result.itens) {
        entry(item)
        if ((filters.tipo && item.tipo !== filters.tipo) || (filters.status && item.status !== filters.status)) throw unexpected()
      }
      return result
    },
    async get(entryId: number, signal?: AbortSignal) { return entry(await request(`/lancamentos/${id(entryId)}`, signal), entryId) },
    async balance(entryId: number, signal?: AbortSignal) {
      const result = await request(`/lancamentos/${id(entryId)}/saldo`, signal)
      if (!isEntryBalance(result) || result.empresaId !== empresaId || result.lancamentoId !== entryId) throw unexpected()
      return result
    },
    async create(input: EntryPayload, signal?: AbortSignal) {
      const result = entry(await request('/lancamentos', signal, { method: 'POST', body: payload(input, true) }))
      if (result.tipo !== input.tipo) throw unexpected()
      return result
    },
    async update(entryId: number, input: EntryPayload, signal?: AbortSignal) {
      const result = entry(await request(`/lancamentos/${id(entryId)}`, signal, { method: 'PUT', body: payload(input, false) }), entryId)
      if (result.tipo !== input.tipo) throw unexpected()
      return result
    },
    async cancel(entryId: number, signal?: AbortSignal) {
      const result = entry(await request(`/lancamentos/${id(entryId)}/cancelamento`, signal, { method: 'POST', body: {} }), entryId)
      if (result.status !== 'CANCELADO') throw unexpected()
      return result
    },
  }
}
