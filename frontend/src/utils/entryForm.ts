import { localDate, validDate } from './format'
import type { Entry, EntryFilters, EntryPayload, EntryType } from '../types/entries'
export interface EntryDraft { tipo: EntryType; descricao: string; categoriaId: string; clienteId: string; fornecedorId: string; centroCustoId: string; dataEmissao: string; dataVencimento: string; valor: string; observacoes: string }
export const draftEntry = (entry?: Entry): EntryDraft => entry ? { tipo: entry.tipo, descricao: entry.descricao, categoriaId: String(entry.categoriaId), clienteId: entry.clienteId === null ? '' : String(entry.clienteId), fornecedorId: entry.fornecedorId === null ? '' : String(entry.fornecedorId), centroCustoId: entry.centroCustoId === null ? '' : String(entry.centroCustoId), dataEmissao: entry.dataEmissao.slice(0, 10), dataVencimento: entry.dataVencimento.slice(0, 10), valor: String(entry.valor).replace('.', ','), observacoes: entry.observacoes ?? '' } : { tipo: 'RECEITA', descricao: '', categoriaId: '', clienteId: '', fornecedorId: '', centroCustoId: '', dataEmissao: localDate(new Date()), dataVencimento: localDate(new Date()), valor: '', observacoes: '' }
export const validIdText = (text: string) => /^[1-9]\d*$/.test(text) && Number.isSafeInteger(Number(text))
export function filterErrors(f: EntryFilters) {
  if ((f.vencimentoInicio && !validDate(f.vencimentoInicio)) || (f.vencimentoFim && !validDate(f.vencimentoFim)) || (f.vencimentoInicio && f.vencimentoFim && f.vencimentoInicio > f.vencimentoFim)) return 'Informe um intervalo de vencimento válido, em ordem crescente.'
  if (['categoriaId', 'clienteId', 'fornecedorId', 'centroCustoId'].some(k => { const value = f[k as keyof EntryFilters]; return value && !validIdText(value) })) return 'Os códigos de referência devem ser inteiros positivos válidos.'
  return ''
}
export function validateEntry(draft: EntryDraft): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!draft.descricao.trim() || draft.descricao.trim().length > 255) errors.descricao = 'Informe uma descrição com até 255 caracteres.'
  if (!validIdText(draft.categoriaId)) errors.categoriaId = 'Informe um código de categoria positivo.'
  for (const k of ['clienteId', 'fornecedorId', 'centroCustoId'] as const) if (draft[k] && !validIdText(draft[k])) errors[k] = 'Informe um código positivo ou deixe vazio.'
  for (const k of ['dataEmissao', 'dataVencimento'] as const) if (!validDate(draft[k]) || draft[k] < '1000-01-01') errors[k] = 'Informe uma data válida entre 01/01/1000 e 31/12/9999.'
  const value = Number(draft.valor.replace(',', '.'))
  if (!/^\d+(?:[,.]\d{1,2})?$/.test(draft.valor) || !Number.isFinite(value) || value <= 0 || value > 9999999999999.99) errors.valor = 'Informe um valor positivo, até 9999999999999,99, com no máximo duas casas decimais, sem separador de milhar.'
  if (new TextEncoder().encode(draft.observacoes).length > 65535) errors.observacoes = 'As observações excedem o limite de 65535 bytes UTF-8.'
  return errors
}
export function entryPayload(draft: EntryDraft): EntryPayload {
  return { tipo: draft.tipo, descricao: draft.descricao.trim(), categoriaId: Number(draft.categoriaId), clienteId: draft.clienteId ? Number(draft.clienteId) : null, fornecedorId: draft.fornecedorId ? Number(draft.fornecedorId) : null, centroCustoId: draft.centroCustoId ? Number(draft.centroCustoId) : null, dataEmissao: draft.dataEmissao, dataVencimento: draft.dataVencimento, valor: Number(draft.valor.replace(',', '.')), observacoes: draft.observacoes || null }
}
