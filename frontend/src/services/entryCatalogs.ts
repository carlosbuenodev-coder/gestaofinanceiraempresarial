import type { EntryRequest } from './entries'
import { unexpected } from './api'
import { positiveId } from '../types/entries'
import type { EntryType } from '../types/entries'

export interface CatalogOption { id: number; nome: string }
interface CategoryOption extends CatalogOption { tipo: EntryType }
interface ActiveOption extends CatalogOption { ativo: boolean }

interface EntryCatalogs {
  empresaId: number
  categorias: CategoryOption[]
  clientes: ActiveOption[]
  fornecedores: ActiveOption[]
  centrosCusto: CatalogOption[]
}

function isOption(value: unknown): value is CatalogOption {
  return typeof value === 'object' && value !== null && 'id' in value && positiveId(value.id)
    && 'nome' in value && typeof value.nome === 'string'
}

function isCategory(value: unknown): value is CategoryOption {
  return isOption(value) && 'tipo' in value && (value.tipo === 'RECEITA' || value.tipo === 'DESPESA')
}

function isActiveOption(value: unknown): value is ActiveOption {
  return isOption(value) && 'ativo' in value && typeof value.ativo === 'boolean'
}

export async function loadEntryCatalogs(request: EntryRequest, companyId: number, signal: AbortSignal): Promise<EntryCatalogs> {
  const value = await request('/cadastros-financeiros', signal)
  if (typeof value !== 'object' || value === null
    || !('empresaId' in value) || value.empresaId !== companyId
    || !('categorias' in value) || !Array.isArray(value.categorias) || !value.categorias.every(isCategory)
    || !('clientes' in value) || !Array.isArray(value.clientes) || !value.clientes.every(isActiveOption)
    || !('fornecedores' in value) || !Array.isArray(value.fornecedores) || !value.fornecedores.every(isActiveOption)
    || !('centrosCusto' in value) || !Array.isArray(value.centrosCusto) || !value.centrosCusto.every(isOption)) {
    throw unexpected()
  }
  return value as EntryCatalogs
}
