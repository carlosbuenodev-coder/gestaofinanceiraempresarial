import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { ApiError, unexpected } from '../services/api'
import { cashflowApi } from '../services/cashflow'
import type { AuthorizedRequest, CashflowFilters } from '../services/cashflow'
import type { Conta, Movimento, Pagina, Resumo } from '../types/cashflow'
import { dateLabel, money, presetRange, validDate } from '../utils/format'

type PartialCashflow = { conflict: string; contas?: Conta[]; error?: string }
type CashflowResult = { resumo: Resumo } | { error: string } | PartialCashflow

function errorMessage(error: unknown) {
  return (error instanceof ApiError ? error : unexpected()).message
}

function RealizedMovements({ empresaId, filters, authorizedRequest }: {
  empresaId: number
  filters: CashflowFilters
  authorizedRequest: AuthorizedRequest
}) {
  const [query, setQuery] = useState({ page: 1 })
  const [loaded, setLoaded] = useState<{
    query: typeof query
    result: Pagina<Movimento> | { error: string }
  } | null>(null)
  const result = loaded?.query === query ? loaded.result : null

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const result = await cashflowApi(authorizedRequest, empresaId, filters, controller.signal).movimentos('realizado', query.page)
        if (result.itens.some(item => {
          const date = item.data.slice(0, 10)
          return item.baixaId === null || item.contaBancariaId === null ||
            date < filters.dataInicial || date > filters.dataFinal || (filters.tipo && item.tipo !== filters.tipo)
        })) throw unexpected()
        if (!controller.signal.aborted) setLoaded({ query, result })
      } catch (error) {
        if (!controller.signal.aborted) setLoaded({ query, result: { error: errorMessage(error) } })
      }
    }
    void load()
    return () => controller.abort()
  }, [authorizedRequest, empresaId, filters, query])

  if (!result) return <p role="status">Carregando movimentações realizadas…</p>
  if ('error' in result) return <>
    <p role="alert">Não foi possível carregar as movimentações realizadas. {result.error}</p>
    <button className="button button-secondary" onClick={() => setQuery({ ...query })}>Tentar novamente o detalhamento</button>
  </>

  const totalPages = Math.max(1, Math.ceil(result.total / result.tamanhoPagina))
  return <>
    {result.itens.length === 0 ? <p className="empty-state" role="status">
      {result.total === 0 ? 'Nenhuma movimentação realizada para os filtros aplicados.' : 'Nenhuma movimentação realizada nesta página.'}
    </p> : <div className="table-scroll"><table>
      <caption>Movimentações realizadas</caption>
      <thead><tr><th scope="col">Data de pagamento</th><th scope="col">Descrição</th><th scope="col">Tipo</th><th scope="col">Conta</th><th scope="col">Principal realizado</th></tr></thead>
      <tbody>{result.itens.map(item => <tr key={item.baixaId}>
        <td>{dateLabel(item.data)}</td>
        <th scope="row">{item.descricao}</th>
        <td>{item.tipo === 'RECEITA' ? 'Receita' : 'Despesa'}</td>
        <td>Conta #{item.contaBancariaId}</td>
        <td>{money(item.principal)}</td>
      </tr>)}</tbody>
    </table></div>}
    {(totalPages > 1 || query.page > 1) && <nav className="movement-actions" aria-label="Paginação das movimentações realizadas">
      <button className="button button-secondary" disabled={query.page === 1} onClick={() => setQuery({ page: Math.min(query.page - 1, totalPages) })}>Anterior</button>
      <span>Página {query.page}{query.page <= totalPages && ` de ${totalPages}`}</span>
      <button className="button button-secondary" disabled={query.page >= totalPages} onClick={() => setQuery({ page: query.page + 1 })}>Próxima</button>
    </nav>}
  </>
}

export function CashflowPage() {
  const { usuario, authorizedRequest } = useAuth()
  const empresaId = usuario?.empresaId
  const [draft, setDraft] = useState<CashflowFilters>(() => presetRange('current'))
  const [filters, setFilters] = useState(draft)
  const [validation, setValidation] = useState('')
  const [showMovements, setShowMovements] = useState(false)
  const [loaded, setLoaded] = useState<{ empresaId: number; filters: CashflowFilters; result: CashflowResult } | null>(null)
  const result = loaded && loaded.empresaId === empresaId && loaded.filters === filters ? loaded.result : null

  useEffect(() => {
    if (!empresaId) return
    const controller = new AbortController()
    const finish = (result: CashflowResult) => {
      if (!controller.signal.aborted) setLoaded({ empresaId, filters, result })
    }
    const api = cashflowApi(authorizedRequest, empresaId, filters, controller.signal)
    async function load() {
      try {
        finish({ resumo: await api.resumo() })
      } catch (error) {
        if (controller.signal.aborted) return
        if (!(error instanceof ApiError) || error.code !== 'conflict') {
          finish({ error: errorMessage(error) })
          return
        }
        const conflict = error.message
        finish({ conflict })
        try {
          finish({ conflict, contas: await api.realizadoPorConta() })
        } catch (realizedError) {
          finish({ conflict, error: errorMessage(realizedError) })
        }
      }
    }
    void load()
    return () => controller.abort()
  }, [authorizedRequest, empresaId, filters])

  function apply(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!validDate(draft.dataInicial) || !validDate(draft.dataFinal) || draft.dataInicial > draft.dataFinal) {
      setValidation('Informe datas válidas, com a data inicial anterior ou igual à final.')
      return
    }
    setValidation('')
    setFilters({ ...draft })
  }

  const resumo = result && 'resumo' in result ? result.resumo : null
  const partial = result && 'conflict' in result ? result : null
  const period = `${dateLabel(filters.dataInicial)} a ${dateLabel(filters.dataFinal)} · ${filters.tipo === 'RECEITA' ? 'Receitas' : filters.tipo === 'DESPESA' ? 'Despesas' : 'Receitas e despesas'}`
  const retry = () => setFilters({ ...filters })
  return <>
    <div className="page-heading">
      <div>
        <h1>Fluxo de caixa</h1>
        <p>Acompanhe as entradas e saídas previstas e realizadas da sua empresa.</p>
      </div>
    </div>
    <form className="finance-filters finance-panel" aria-label="Filtros de fluxo de caixa" onSubmit={apply} noValidate>
      <label>Data inicial<input type="date" value={draft.dataInicial} required
        aria-invalid={!!validation} aria-describedby={validation ? 'cashflow-filter-error' : undefined}
        onChange={e => setDraft({ ...draft, dataInicial: e.target.value })} /></label>
      <label>Data final<input type="date" value={draft.dataFinal} required
        aria-invalid={!!validation} aria-describedby={validation ? 'cashflow-filter-error' : undefined}
        onChange={e => setDraft({ ...draft, dataFinal: e.target.value })} /></label>
      <label>Tipo de movimentação<select value={draft.tipo ?? ''} onChange={e => {
        const tipo = e.target.value
        setDraft({ ...draft, tipo: tipo === 'RECEITA' || tipo === 'DESPESA' ? tipo : undefined })
      }}>
        <option value="">Todos</option>
        <option value="RECEITA">Receitas</option>
        <option value="DESPESA">Despesas</option>
      </select></label>
      <button className="button button-primary" type="submit">Aplicar filtros</button>
      {validation && <p id="cashflow-filter-error" className="field-error" role="alert">{validation}</p>}
    </form>

    {!result && <p className="finance-panel" role="status">Carregando fluxo de caixa…</p>}
    {result && 'error' in result && !partial && <section className="finance-panel" role="alert">
      <h2>Não foi possível carregar o fluxo de caixa</h2>
      <p>{result.error}</p>
      <button className="button button-secondary" onClick={retry}>Tentar novamente</button>
    </section>}
    {partial && <section className="finance-panel" aria-labelledby="cashflow-partial-title">
      <h2 id="cashflow-partial-title">Resultado parcial do período</h2>
      <p className="finance-note">{period}</p>
      <p role="alert"><strong>Valores previstos indisponíveis.</strong> {partial.conflict}</p>
      {!partial.contas && !partial.error && <p role="status">Carregando valores realizados…</p>}
      {partial.error && <p role="alert">Não foi possível carregar os valores realizados. {partial.error}</p>}
      {partial.contas && (partial.contas.length === 0
        ? <p className="empty-state" role="status">Sem movimentações realizadas no período selecionado.</p>
        : <div className="table-scroll"><table>
          <caption>Realizado por conta</caption>
          <thead><tr><th scope="col">Conta</th><th scope="col">Receitas realizadas</th><th scope="col">Despesas realizadas</th><th scope="col">Resultado líquido realizado</th></tr></thead>
          <tbody>{partial.contas.map(conta => <tr key={conta.contaBancariaId}>
            <th scope="row">Conta #{conta.contaBancariaId}</th>
            <td>{money(conta.receitasRealizadas)}</td>
            <td>{money(conta.despesasRealizadas)}</td>
            <td>{money(conta.saldoRealizado)}</td>
          </tr>)}</tbody>
        </table></div>)}
      <p className="finance-note">A consulta do realizado fornece valores por conta. O total consolidado do período está indisponível.</p>
      <button className="button button-secondary" onClick={retry}>Tentar novamente</button>
    </section>}
    {resumo && <section className="finance-panel" aria-labelledby="cashflow-summary-title">
      <h2 id="cashflow-summary-title">Resumo do período</h2>
      <p className="finance-note">{period}</p>
      {resumo.quantidadeLancamentos === 0 ? <p className="empty-state" role="status">Sem movimentações no período selecionado.</p> :
        <div className="table-scroll">
          <table aria-labelledby="cashflow-summary-title">
            <thead><tr><th scope="col">Movimentação</th><th scope="col">Previsto</th><th scope="col">Realizado</th></tr></thead>
            <tbody>
              <tr><th scope="row">Receitas</th><td>{money(resumo.receitasPrevistas)}</td><td>{money(resumo.receitasRealizadas)}</td></tr>
              <tr><th scope="row">Despesas</th><td>{money(resumo.despesasPrevistas)}</td><td>{money(resumo.despesasRealizadas)}</td></tr>
              <tr><th scope="row">Resultado líquido</th><td>{money(resumo.saldoPrevisto)}</td><td>{money(resumo.saldoRealizado)}</td></tr>
            </tbody>
          </table>
        </div>}
      <p className="finance-note">O previsto usa o principal pendente por vencimento. O realizado usa o principal das baixas válidas por data de pagamento. O saldo bancário acumulado está indisponível neste resumo.</p>
    </section>}
    {empresaId && (resumo || partial) && <section className="finance-panel" aria-labelledby="cashflow-movements-title">
      <div className="section-heading">
        <h2 id="cashflow-movements-title">Detalhamento do realizado</h2>
        <button className="button button-secondary" aria-expanded={showMovements} aria-controls="cashflow-movements"
          onClick={() => setShowMovements(open => !open)}>
          {showMovements ? 'Ocultar movimentações realizadas' : 'Ver movimentações realizadas'}
        </button>
      </div>
      <div id="cashflow-movements" hidden={!showMovements}>
        {showMovements && <RealizedMovements key={empresaId} empresaId={empresaId} filters={filters} authorizedRequest={authorizedRequest} />}
      </div>
    </section>}
  </>
}
