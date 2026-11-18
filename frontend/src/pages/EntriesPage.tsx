import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { entriesApi } from '../services/entries'
import { emptyFilters, statuses } from '../types/entries'
import type { EntryFilters as Filters, EntryPage, EntryType } from '../types/entries'
import { EntryFilters } from '../components/EntryFilters'
import { EntryBalancePanel, entryError } from '../components/EntryBalancePanel'
import { filterErrors } from '../utils/entryForm'
import { dateLabel, money } from '../utils/format'
function readFilters(search: string, fixedType?: EntryType): Filters {
  const params = new URLSearchParams(search)
  const filters = { ...emptyFilters }
  for (const key of Object.keys(filters) as (keyof Filters)[]) if (params.has(key)) (filters[key] as string) = params.get(key)!
  filters.tipo = fixedType ?? (['RECEITA', 'DESPESA'].includes(filters.tipo) ? filters.tipo : '')
  if (!Object.hasOwn(statuses, filters.status)) filters.status = ''
  if (!['id', 'valor', 'dataEmissao', 'dataVencimento'].includes(filters.ordenarPor)) filters.ordenarPor = 'dataVencimento'
  if (!['asc', 'desc'].includes(filters.direcao)) filters.direcao = 'asc'
  if (!['10', '20', '50', '100'].includes(filters.tamanhoPagina)) filters.tamanhoPagina = '20'
  return filters
}
export function EntriesPage({ fixedType }: { fixedType?: EntryType }) {
  const { usuario, authorizedRequest } = useAuth()
  const empresaId = usuario?.empresaId
  const location = useLocation()
  const [params, setParams] = useSearchParams()
  const search = params.toString()
  const filters = useMemo(() => readFilters(search, fixedType), [search, fixedType])
  const [draft, setDraft] = useState(filters)
  const [validation, setValidation] = useState('')
  const [data, setData] = useState<EntryPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  const pageText = params.get('pagina') ?? '1'
  const page = /^[1-9]\d*$/.test(pageText) && Number.isSafeInteger(Number(pageText)) ? Number(pageText) : 1
  const title = fixedType === 'DESPESA' ? 'Contas a pagar' : fixedType === 'RECEITA' ? 'Contas a receber' : 'Lançamentos'
  useEffect(() => { setDraft(filters) }, [filters])
  useEffect(() => {
    setData(null); setError(''); setLoading(true)
    const invalid = filterErrors(filters) || ((page - 1) * Number(filters.tamanhoPagina) > 2147483647 ? 'Página fora do intervalo permitido.' : '')
    if (invalid) { setError(invalid); setLoading(false); return }
    if (!empresaId) return
    const controller = new AbortController()
    entriesApi(authorizedRequest, empresaId).list(filters, page, controller.signal)
      .then(value => { if (!controller.signal.aborted) setData(value) })
      .catch(e => { if (!controller.signal.aborted) setError(entryError(e).message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [authorizedRequest, empresaId, filters, page, revision])
  if (!usuario) return null
  function apply(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const invalid = filterErrors(draft)
    setValidation(invalid)
    if (invalid) return
    const next = new URLSearchParams()
    for (const [key, value] of Object.entries({ ...draft, ...(fixedType ? { tipo: fixedType } : {}) })) if (value) next.set(key, value)
    next.set('pagina', '1')
    if (next.toString() === search) setRevision(n => n + 1)
    else setParams(next)
  }
  function paginate(value: number) { const next = new URLSearchParams(params); next.set('pagina', String(value)); setParams(next) }
  return <>
    <div className="page-heading"><div><p className="eyebrow">ROTINA FINANCEIRA</p><h1>{title}</h1><p>{fixedType ? `Consulta de ${fixedType === 'DESPESA' ? 'despesas' : 'receitas'} da empresa #${usuario.empresaId}.` : `Receitas e despesas da empresa #${usuario.empresaId}.`}</p></div><div className="movement-actions"><button className="button button-secondary" disabled={loading} onClick={() => setRevision(n => n + 1)}>Atualizar lista</button>{usuario.perfil !== 'CONSULTA' && <Link className="button button-primary" to="/app/lancamentos/novo">Novo lançamento</Link>}</div></div>
    <EntryFilters draft={draft} setDraft={setDraft} apply={apply} clear={() => { setValidation(''); if (search) setParams({}); else setRevision(n => n + 1) }} fixedType={fixedType} error={validation} />
    <p className="finance-note">Situações oficiais da API. Valores pagos/recebidos e saldos são consultados individualmente, sem estimativas a partir do status. Cancelados não representam obrigações pendentes.</p>
    {loading && <section className="finance-panel" role="status">Carregando lançamentos…</section>}
    {error && <section className="finance-panel" role="alert"><p>{error}</p><button className="button button-primary" onClick={() => setRevision(n => n + 1)}>Tentar novamente</button></section>}
    {!loading && data && <section className="finance-panel" aria-label="Resultado da consulta"><h2>{data.total} lançamento(s) encontrado(s)</h2>
      {!data.itens.length ? <p className="empty-state">Nenhum lançamento nesta página para os filtros selecionados.</p> : <div className="table-scroll"><table className="entry-table"><thead><tr><th scope="col">ID / descrição</th><th scope="col">Tipo</th><th scope="col">Vencimento</th><th scope="col">Situação</th><th scope="col">Valor original</th><th scope="col">Valores pagos/recebidos e saldo</th></tr></thead><tbody>{data.itens.map(item => <tr key={`${item.id}-${revision}-${search}`}><th scope="row"><Link to={`/app/lancamentos/${item.id}`} state={{ returnTo: location.pathname + location.search }}>#{item.id} · {item.descricao}</Link></th><td>{item.tipo === 'RECEITA' ? 'Receita' : 'Despesa'}</td><td>{dateLabel(item.dataVencimento)}</td><td><span className={`entry-status status-${item.status.toLowerCase()}`}>{statuses[item.status]}</span></td><td>{money(item.valor)}</td><td><EntryBalancePanel id={item.id} /></td></tr>)}</tbody></table></div>}
      <nav className="movement-actions" aria-label="Paginação de lançamentos"><button className="button button-secondary" disabled={page <= 1} onClick={() => paginate(page - 1)}>Anterior</button><span>Página {page} de {Math.max(1, data.totalPaginas)}</span><button className="button button-secondary" disabled={page >= data.totalPaginas} onClick={() => paginate(page + 1)}>Próxima</button></nav>
    </section>}
  </>
}
