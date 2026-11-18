import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { ApiError, unexpected } from '../services/api'
import { entriesApi } from '../services/entries'
import type { EntryBalance } from '../types/entries'
import { money } from '../utils/format'
export const entryError = (error: unknown) => error instanceof ApiError ? error : unexpected()
export function EntryBalancePanel({ id, automatic = false, revision = 0 }: { id: number; automatic?: boolean; revision?: number }) {
  const { usuario, authorizedRequest } = useAuth()
  const empresaId = usuario?.empresaId
  const [requested, setRequested] = useState(automatic)
  const [attempt, setAttempt] = useState(0)
  const [balance, setBalance] = useState<EntryBalance | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    setBalance(null); setError('')
    if (!requested || !empresaId) return
    const controller = new AbortController()
    entriesApi(authorizedRequest, empresaId).balance(id, controller.signal)
      .then(value => { if (!controller.signal.aborted) setBalance(value) })
      .catch(e => { if (!controller.signal.aborted) setError(entryError(e).message) })
    return () => controller.abort()
  }, [id, empresaId, authorizedRequest, requested, attempt, revision])
  if (!requested) return <button className="button button-secondary" onClick={() => setRequested(true)}>Consultar valores de #{id}</button>
  if (error) return <div role="alert"><p>{error}</p><button className="button button-secondary" onClick={() => setAttempt(n => n + 1)}>Tentar valores de #{id} novamente</button></div>
  if (!balance) return <p role="status">Consultando valores de #{id}…</p>
  return <dl className="entry-balance" aria-label={`Valores do lançamento ${id}`}><div><dt>Valor original</dt><dd>{money(balance.valorOriginal)}</dd></div><div><dt>Principal baixado</dt><dd>{money(balance.totalBaixado)}</dd></div><div><dt>Saldo restante</dt><dd>{money(balance.saldoRestante)}</dd></div></dl>
}
