import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { ApiError, unexpected } from '../services/api'
import { cashflowApi } from '../services/cashflow'
import type { Dashboard, Movimento, Pagina } from '../types/cashflow'
import { dateLabel, money, presetRange, validDate } from '../utils/format'
import type { DateRange } from '../utils/format'
import { monthlyRows } from '../utils/charts'
import { FinanceChart, MetricCard } from '../components/FinanceChart'

export function DashboardPage() {
  const { usuario, authorizedRequest } = useAuth()
  const [draft, setDraft] = useState<DateRange>(() => presetRange('current'))
  const [preset, setPreset] = useState('current')
  const [range, setRange] = useState(draft)
  const [revision, setRevision] = useState(0)
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')
  const [validation, setValidation] = useState('')
  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<'previsto' | 'realizado' | ''>('')
  const [page, setPage] = useState(1)
  const [movements, setMovements] = useState<Pagina<Movimento> | null>(null)
  const [detailError, setDetailError] = useState('')
  const [detailRevision, setDetailRevision] = useState(0)
  const empresaId = usuario?.empresaId
  useEffect(() => {
    if (!empresaId) return
    const controller = new AbortController()
    setLoading(true); setData(null); setError('')
    cashflowApi(authorizedRequest, empresaId, range, controller.signal).dashboard()
      .then(result => { if (!controller.signal.aborted) setData(result) })
      .catch(e => { if (!controller.signal.aborted) setError((e instanceof ApiError ? e : unexpected()).message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [authorizedRequest, empresaId, range, revision])
  useEffect(() => {
    setMovements(null); setDetailError('')
    if (!empresaId || !detail || !data || loading) return
    const controller = new AbortController()
    cashflowApi(authorizedRequest, empresaId, range, controller.signal).movimentos(detail, page)
      .then(result => { if (!controller.signal.aborted) setMovements(result) })
      .catch(e => { if (!controller.signal.aborted) setDetailError((e instanceof ApiError ? e : unexpected()).message) })
    return () => controller.abort()
  }, [authorizedRequest, empresaId, range, detail, page, data, loading, detailRevision])
  if (!usuario) return null
  function apply(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!validDate(draft.dataInicial) || !validDate(draft.dataFinal) || draft.dataInicial > draft.dataFinal) {
      setValidation('Informe datas válidas, com a data inicial anterior ou igual à final.'); return
    }
    setValidation(''); setDetail(''); setPage(1)
    setRange({ ...draft })
  }
  return <>
    <div className="page-heading"><div><p className="eyebrow">VISÃO FINANCEIRA</p><h1>Dashboard financeiro</h1><p>Acompanhe os resultados do período da empresa #{usuario.empresaId}.</p></div><button className="button button-secondary" disabled={loading} onClick={() => setRevision(n => n + 1)}>Atualizar dashboard</button></div>
    <form className="finance-filters finance-panel" onSubmit={apply} aria-label="Filtros do dashboard" noValidate>
      <label>Período<select value={preset} onChange={e => { setPreset(e.target.value); if (e.target.value !== 'custom') setDraft(presetRange(e.target.value)) }}><option value="current">Mês atual</option><option value="previous">Mês anterior</option><option value="90">Últimos 90 dias</option><option value="year">Ano atual</option><option value="custom">Período personalizado</option></select></label>
      <label>Data inicial<input type="date" value={draft.dataInicial} aria-invalid={!!validation} aria-describedby={validation ? 'filter-error' : undefined} onChange={e => { setPreset('custom'); setDraft({ ...draft, dataInicial: e.target.value }) }} /></label>
      <label>Data final<input type="date" value={draft.dataFinal} aria-invalid={!!validation} aria-describedby={validation ? 'filter-error' : undefined} onChange={e => { setPreset('custom'); setDraft({ ...draft, dataFinal: e.target.value }) }} /></label>
      <button className="button button-primary" type="submit" disabled={loading}>Aplicar filtros</button>
      {validation && <p id="filter-error" className="field-error" role="alert">{validation}</p>}
    </form>
    <p className="finance-note">Empresa autenticada #{usuario.empresaId} · Previsto: principal ainda pendente por vencimento. Realizado: principal das baixas válidas por pagamento. Resultados do período não representam saldo bancário acumulado.</p>
    {loading && <div className="finance-panel" role="status"><span className="spinner" /> Carregando informações financeiras…</div>}
    {error && <section className="finance-panel" role="alert"><h2>Não foi possível carregar o dashboard</h2><p>{error}</p><button className="button button-primary" onClick={() => setRevision(n => n + 1)}>Tentar novamente</button></section>}
    {!loading && data && <div aria-live="polite">
      <p className="finance-note">Período consultado: {dateLabel(range.dataInicial)} a {dateLabel(range.dataFinal)}</p>
      {data.resumo.quantidadeLancamentos === 0 && <p className="empty-state" role="status">Sem movimentações no período selecionado.</p>}
      <div className="metrics-grid">
        <MetricCard label="Receitas previstas" value={data.resumo.receitasPrevistas} tone="income" /><MetricCard label="Despesas previstas" value={data.resumo.despesasPrevistas} tone="expense" />
        <MetricCard label="Receitas realizadas" value={data.resumo.receitasRealizadas} tone="income" /><MetricCard label="Despesas realizadas" value={data.resumo.despesasRealizadas} tone="expense" />
        <MetricCard label="Resultado previsto" value={data.resumo.saldoPrevisto} /><MetricCard label="Resultado realizado" value={data.resumo.saldoRealizado} /><MetricCard label="Valores pendentes" value={data.resumo.valoresPendentes} tone="pending" />
      </div>
      <FinanceChart title="Previsto e realizado no período" series={['Receitas', 'Despesas']} rows={data.resumo.quantidadeLancamentos ? [{ label: 'Previsto', values: [data.resumo.receitasPrevistas, data.resumo.despesasPrevistas] }, { label: 'Realizado', values: [data.resumo.receitasRealizadas, data.resumo.despesasRealizadas] }] : []} />
      <FinanceChart title="Entradas e saídas por data" series={['Receitas previstas', 'Despesas previstas', 'Receitas realizadas', 'Despesas realizadas']} rows={data.porPeriodo.itens.map(p => ({ label: dateLabel(p.data), values: [p.receitasPrevistas, p.despesasPrevistas, p.receitasRealizadas, p.despesasRealizadas] }))} />
      <FinanceChart title="Evolução mensal dos resultados no intervalo" series={['Resultado previsto', 'Resultado realizado']} rows={monthlyRows(data.porPeriodo.itens)} /><p className="finance-note">Agrupamento visual dos resultados diários retornados pela API. Meses nas extremidades podem estar incompletos conforme o filtro; meses sem movimentos não são criados.</p><section className="finance-panel"><h2>Resultado realizado por conta</h2><p className="finance-note">Principal das baixas válidas; não inclui juros, multas ou descontos.</p>{!data.porConta.itens.length ? <p className="empty-state">Sem movimentações realizadas por conta.</p> : <div className="table-scroll"><table><thead><tr><th scope="col">Conta</th><th scope="col">Receitas</th><th scope="col">Despesas</th><th scope="col">Resultado</th></tr></thead><tbody>{data.porConta.itens.map(c => <tr key={c.contaBancariaId}><th scope="row">Conta #{c.contaBancariaId}</th><td>{money(c.receitasRealizadas)}</td><td>{money(c.despesasRealizadas)}</td><td>{money(c.saldoRealizado)}</td></tr>)}</tbody></table></div>}</section>
      <section className="finance-panel"><h2>Movimentações do período</h2><div className="movement-actions"><button className="button button-secondary" aria-pressed={detail === 'previsto'} onClick={() => { setDetail('previsto'); setPage(1) }}>Ver pendências previstas</button><button className="button button-secondary" aria-pressed={detail === 'realizado'} onClick={() => { setDetail('realizado'); setPage(1) }}>Ver baixas realizadas</button></div>
        {detail && !movements && !detailError && <p role="status">Carregando movimentações…</p>}
        {detailError && <div role="alert"><p>{detailError}</p><button className="button button-secondary" onClick={() => setDetailRevision(n => n + 1)}>Tentar novamente movimentações</button></div>}
        {movements && <>{!movements.itens.length ? <p className="empty-state">Sem movimentações nesta consulta.</p> : <div className="table-scroll"><table><caption>{detail === 'previsto' ? 'Pendências por vencimento' : 'Baixas por pagamento'}</caption><thead><tr><th scope="col">Data</th><th scope="col">Descrição</th><th scope="col">Tipo</th><th scope="col">Principal</th></tr></thead><tbody>{movements.itens.map((m, i) => <tr key={`${m.lancamentoId}-${m.baixaId}-${i}`}><td>{dateLabel(m.data)}</td><th scope="row">{m.descricao}</th><td>{m.tipo === 'RECEITA' ? 'Receita' : 'Despesa'}</td><td>{money(m.principal)}</td></tr>)}</tbody></table></div>}<div className="movement-actions"><button className="button button-secondary" disabled={page <= 1} onClick={() => setPage(n => n - 1)}>Anterior</button><span>Página {page} de {Math.max(1, Math.ceil(movements.total / 100))}</span><button className="button button-secondary" disabled={page * 100 >= movements.total} onClick={() => setPage(n => n + 1)}>Próxima</button></div></>}
      </section>
    </div>}
  </>
}
