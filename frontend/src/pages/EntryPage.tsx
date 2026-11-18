import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { entriesApi } from '../services/entries'
import { statuses } from '../types/entries'
import type { Entry } from '../types/entries'
import { EntryForm } from '../components/EntryForm'
import { EntryBalancePanel, entryError } from '../components/EntryBalancePanel'
import { dateLabel, money } from '../utils/format'
import { validIdText } from '../utils/entryForm'
export function EntryPage({ edit = false, create = false }: { edit?: boolean; create?: boolean }) {
  const { usuario, authorizedRequest } = useAuth()
  const { id = '' } = useParams()
  const location = useLocation()
  const empresaId = usuario?.empresaId
  const canWrite = usuario?.perfil === 'ADMIN' || usuario?.perfil === 'FINANCEIRO'
  const [item, setItem] = useState<Entry | null>(null)
  const [loading, setLoading] = useState(!create)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  const [confirm, setConfirm] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState('')
  const [cancelled, setCancelled] = useState(false)
  const pending = useRef<AbortController | null>(null)
  const lock = useRef(false)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => {
    pending.current?.abort(); lock.current = false; setCancelling(false)
    setItem(null); setError(''); setConfirm(false); setCancelled(false); setCancelError('')
    if (create || !empresaId || (edit && !canWrite)) { setLoading(false); return }
    if (!validIdText(id)) { setError('Código de lançamento inválido.'); setLoading(false); return }
    setLoading(true)
    const controller = new AbortController()
    entriesApi(authorizedRequest, empresaId).get(Number(id), controller.signal)
      .then(result => { if (!controller.signal.aborted) setItem(result) })
      .catch(e => { if (!controller.signal.aborted) setError(entryError(e).message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id, create, edit, canWrite, empresaId, authorizedRequest, revision])
  if (!usuario) return null
  if ((create || edit) && !canWrite) return <section className="finance-panel" role="alert"><h1>Acesso restrito</h1><p>Seu perfil permite somente consulta.</p><Link to="/app/lancamentos">Voltar aos lançamentos</Link></section>
  const returnTo = typeof location.state?.returnTo === 'string' && /^\/app\/(lancamentos|contas-a-pagar|contas-a-receber)(\?|$)/.test(location.state.returnTo) ? location.state.returnTo : '/app/lancamentos'
  async function cancel() {
    if (!item || !canWrite || item.status !== 'ABERTO' || lock.current) return
    lock.current = true; setCancelling(true); setCancelError('')
    const controller = new AbortController(); pending.current = controller
    try {
      const result = await entriesApi(authorizedRequest, item.empresaId).cancel(item.id, controller.signal)
      if (!controller.signal.aborted) { setItem(result); setConfirm(false); setCancelled(true) }
    } catch (e) {
      if (!controller.signal.aborted) setCancelError(entryError(e).message + ' Consulte o estado atual antes de tentar novamente.')
    } finally { lock.current = false; if (!controller.signal.aborted) setCancelling(false) }
  }
  return <>
    <div className="page-heading"><div><p className="eyebrow">LANÇAMENTOS</p><h1>{create ? 'Novo lançamento' : edit ? 'Editar lançamento' : `Lançamento #${id}`}</h1><p>Empresa #{usuario.empresaId} · Dados oficiais da API.</p></div><Link className="button button-secondary" to={returnTo}>Voltar à consulta</Link></div>
    {create && <EntryForm />}
    {loading && <section className="finance-panel" role="status">Carregando lançamento…</section>}
    {error && <section className="finance-panel" role="alert"><p>{error}</p><button className="button button-primary" onClick={() => setRevision(n => n + 1)}>Tentar novamente</button></section>}
    {!loading && item && (edit ? <EntryForm key={item.id} entry={item} /> : <>
      {(cancelled || location.state?.notice === 'created' || location.state?.notice === 'updated') && <p className="entry-success" role="status">{cancelled ? 'Lançamento cancelado com sucesso.' : location.state.notice === 'created' ? 'Lançamento cadastrado com sucesso.' : 'Lançamento atualizado com sucesso.'}</p>}
      <section className="finance-panel"><h2>{item.descricao}</h2><dl className="entry-details"><div><dt>Tipo</dt><dd>{item.tipo === 'RECEITA' ? 'Receita' : 'Despesa'}</dd></div><div><dt>Situação</dt><dd>{statuses[item.status]}</dd></div><div><dt>Valor original</dt><dd>{money(item.valor)}</dd></div><div><dt>Emissão</dt><dd>{dateLabel(item.dataEmissao)}</dd></div><div><dt>Vencimento</dt><dd>{dateLabel(item.dataVencimento)}</dd></div><div><dt>Categoria</dt><dd>#{item.categoriaId}</dd></div><div><dt>Cliente</dt><dd>{item.clienteId === null ? 'Não informado' : `#${item.clienteId}`}</dd></div><div><dt>Fornecedor</dt><dd>{item.fornecedorId === null ? 'Não informado' : `#${item.fornecedorId}`}</dd></div><div><dt>Centro de custo</dt><dd>{item.centroCustoId === null ? 'Não informado' : `#${item.centroCustoId}`}</dd></div><div className="entry-wide"><dt>Observações</dt><dd className="entry-observations">{item.observacoes || 'Sem observações.'}</dd></div></dl>
        <button className="button button-secondary" disabled={cancelling} onClick={() => setRevision(n => n + 1)}>Atualizar detalhes</button>
      </section>
      <section className="finance-panel"><h2>Valores pagos/recebidos e saldo</h2><p className="finance-note">Principal das baixas válidas, conforme a API. Não inclui juros, multas e descontos; não é saldo bancário. Lançamentos cancelados não representam obrigações pendentes.</p><EntryBalancePanel key={`${item.id}-${item.status}`} id={item.id} automatic revision={revision} /></section>
      {canWrite && <section className="finance-panel"><h2>Operações disponíveis</h2>{item.status === 'ABERTO' ? <><p className="finance-note">Edição e cancelamento exigem ausência de qualquer baixa, inclusive estornada. O servidor valida a condição atual antes de gravar.</p><div className="movement-actions"><Link className="button button-secondary" aria-disabled={cancelling} onClick={e => { if (cancelling) e.preventDefault() }} to={`/app/lancamentos/${item.id}/editar`}>Editar lançamento</Link><button className="button button-danger" disabled={cancelling || confirm} onClick={() => setConfirm(true)}>Cancelar lançamento</button></div>
        {confirm && <div className="cancel-confirmation" role="group" aria-label="Confirmar cancelamento"><p>Cancelar o lançamento #{item.id}, de {money(item.valor)}? Esta tela não oferece reabertura.</p><div className="movement-actions"><button className="button button-danger" disabled={cancelling} onClick={() => void cancel()}>{cancelling ? 'Cancelando…' : 'Confirmar cancelamento'}</button><button className="button button-secondary" disabled={cancelling} onClick={() => setConfirm(false)}>Manter lançamento</button></div></div>}
      </> : <p>Somente lançamentos abertos podem ser editados ou cancelados.</p>}{cancelError && <p role="alert" className="alert">{cancelError}</p>}</section>}
    </>)}
  </>
}
