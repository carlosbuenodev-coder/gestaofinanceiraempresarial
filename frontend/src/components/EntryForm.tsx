import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import type { Entry } from '../types/entries'
import { useAuth } from '../hooks/useAuth'
import { entriesApi } from '../services/entries'
import { loadEntryCatalogs } from '../services/entryCatalogs'
import type { CatalogOption } from '../services/entryCatalogs'
import { draftEntry, entryPayload, validateEntry } from '../utils/entryForm'
import type { EntryDraft } from '../utils/entryForm'
import { entryError } from './EntryBalancePanel'

type ReferenceField = 'categoriaId' | 'clienteId' | 'fornecedorId' | 'centroCustoId'
type CatalogResult = { data: Awaited<ReturnType<typeof loadEntryCatalogs>> } | { error: string }

export function EntryForm({ entry }: { entry?: Entry }) {
  const { usuario, authorizedRequest } = useAuth()
  const navigate = useNavigate()
  const [draft, setDraft] = useState(() => draftEntry(entry))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [catalogAttempt, setCatalogAttempt] = useState(0)
  const [catalogResult, setCatalogResult] = useState<{ empresaId: number; attempt: number; result: CatalogResult } | null>(null)
  const pending = useRef<AbortController | null>(null)
  const lock = useRef(false)
  const form = useRef<HTMLFormElement>(null)
  useEffect(() => () => pending.current?.abort(), [])
  const empresaId = usuario && usuario.perfil !== 'CONSULTA' && (!entry || entry.status === 'ABERTO') ? usuario.empresaId : undefined
  useEffect(() => {
    if (!empresaId) return
    const controller = new AbortController()
    function finish(result: CatalogResult) {
      if (!controller.signal.aborted && empresaId !== undefined) {
        setCatalogResult({ empresaId, attempt: catalogAttempt, result })
      }
    }
    loadEntryCatalogs(authorizedRequest, empresaId, controller.signal)
      .then(data => finish({ data }), error => finish({ error: entryError(error).message }))
    return () => controller.abort()
  }, [authorizedRequest, catalogAttempt, empresaId])

  if (!usuario) return null
  if (usuario.perfil === 'CONSULTA') return <section className="finance-panel" role="alert">Seu perfil permite somente consulta. Cadastro e edição não estão autorizados.</section>
  if (entry && entry.status !== 'ABERTO') return <section className="finance-panel" role="alert">Somente lançamentos abertos podem ser editados. <Link to={`/app/lancamentos/${entry.id}`}>Ver detalhes</Link></section>
  const result = catalogResult && catalogResult.empresaId === empresaId && catalogResult.attempt === catalogAttempt
    ? catalogResult.result : null
  const catalogs = result && 'data' in result ? result.data : null
  const options: Record<ReferenceField, (CatalogOption & { ativo?: boolean })[]> = {
    categoriaId: catalogs?.categorias.filter(option => option.tipo === draft.tipo) ?? [],
    clienteId: catalogs?.clientes.filter(option => option.ativo || option.id === entry?.clienteId) ?? [],
    fornecedorId: catalogs?.fornecedores.filter(option => option.ativo || option.id === entry?.fornecedorId) ?? [],
    centroCustoId: catalogs?.centrosCusto ?? [],
  }

  function field(key: Exclude<keyof EntryDraft, 'tipo' | ReferenceField>, label: string, type = 'text', required = false) {
    return <label key={key} htmlFor={`entry-${key}`}>{label}{required ? ' *' : ''}<input id={`entry-${key}`} type={type} value={draft[key]} required={required} maxLength={key === 'descricao' ? 255 : undefined} inputMode={key === 'valor' ? 'decimal' : undefined} aria-invalid={!!errors[key]} aria-describedby={errors[key] ? `error-${key}` : undefined} onChange={e => setDraft(d => ({ ...d, [key]: e.target.value }))} />{errors[key] && <span id={`error-${key}`} className="field-error">{errors[key]}</span>}</label>
  }

  function referenceField(key: ReferenceField, label: string, required = false) {
    const selected = draft[key]
    const available = options[key]
    const missing = selected && !available.some(option => String(option.id) === selected)
    const placeholder = !catalogs ? 'Opções indisponíveis' : available.length
      ? required ? 'Selecione uma categoria' : 'Não informado'
      : required ? 'Nenhuma categoria para este tipo' : 'Nenhuma opção disponível'
    return <label htmlFor={`entry-${key}`}>{label}{required ? ' *' : ''}
      <select id={`entry-${key}`} value={selected} required={required}
        disabled={!catalogs || (!available.length && !selected)}
        aria-invalid={!!errors[key]} aria-describedby={errors[key] ? `error-${key}` : undefined}
        onChange={e => setDraft(d => ({ ...d, [key]: e.target.value }))}>
        <option value="">{placeholder}</option>
        {missing && <option value={selected} disabled>{catalogs ? 'Vínculo indisponível' : 'Vínculo atual'}</option>}
        {available.map(option => <option key={option.id} value={option.id}>
          {option.nome.trim() || `Cadastro #${option.id}`}{option.ativo === false ? ' (inativo)' : ''}
        </option>)}
      </select>
      {errors[key] && <span id={`error-${key}`} className="field-error">{errors[key]}</span>}
    </label>
  }
  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (lock.current || !usuario || usuario.perfil === 'CONSULTA' || !catalogs || !options.categoriaId.length) return
    const validation = validateEntry(draft)
    for (const key of ['categoriaId', 'clienteId', 'fornecedorId', 'centroCustoId'] as const) {
      if (draft[key] && !options[key].some(option => String(option.id) === draft[key])) {
        validation[key] = 'O vínculo atual está indisponível. Atualize a seleção.'
      }
    }
    setErrors(validation); setError('')
    if (Object.keys(validation).length) {
      setError('Revise os campos destacados antes de salvar.')
      queueMicrotask(() => form.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus())
      return
    }
    lock.current = true; setSaving(true)
    const controller = new AbortController(); pending.current = controller
    try {
      const api = entriesApi(authorizedRequest, usuario.empresaId)
      const result = entry ? await api.update(entry.id, entryPayload(draft), controller.signal) : await api.create(entryPayload(draft), controller.signal)
      if (!controller.signal.aborted) navigate(`/app/lancamentos/${result.id}`, { replace: true, state: { notice: entry ? 'updated' : 'created' } })
    } catch (e) {
      if (!controller.signal.aborted) {
        const failure = entryError(e)
        setErrors(failure.fields)
        setError(failure.message + (['network', 'server', 'unexpected'].includes(failure.code) ? ' O resultado da gravação não foi confirmado. Consulte a listagem antes de reenviar para evitar duplicidade.' : ''))
      }
    } finally {
      lock.current = false
      if (!controller.signal.aborted) setSaving(false)
    }
  }
  return <form ref={form} className="finance-panel entry-form" onSubmit={submit} aria-label={entry ? 'Editar lançamento' : 'Cadastrar lançamento'} noValidate>
    <p className="finance-note">* Campos obrigatórios.</p>
    {!result && <p role="status">Carregando opções de cadastro…</p>}
    {result && 'error' in result && <div role="alert">
      <p>Não foi possível carregar as opções de cadastro. {result.error}</p>
      <button type="button" className="button button-secondary" onClick={() => setCatalogAttempt(n => n + 1)}>Tentar carregar opções novamente</button>
    </div>}
    {error && <p role="alert" className="alert">{error}</p>}
    <fieldset disabled={saving}><legend>Dados do lançamento</legend><div className="entry-fields">
      <label>Tipo *<select value={draft.tipo} disabled={!!entry} onChange={e => setDraft(d => ({ ...d, tipo: e.target.value as EntryDraft['tipo'], categoriaId: '' }))}><option value="RECEITA">Receita</option><option value="DESPESA">Despesa</option></select></label>
      {field('descricao', 'Descrição', 'text', true)}{field('valor', 'Valor original (R$)', 'text', true)}{referenceField('categoriaId', 'Categoria', true)}
      {field('dataEmissao', 'Data de emissão', 'date', true)}{field('dataVencimento', 'Data de vencimento', 'date', true)}
      {referenceField('clienteId', 'Cliente')}{referenceField('fornecedorId', 'Fornecedor')}{referenceField('centroCustoId', 'Centro de custo')}
      <label className="entry-wide">Observações<textarea value={draft.observacoes} aria-invalid={!!errors.observacoes} aria-describedby={errors.observacoes ? 'error-observacoes' : undefined} onChange={e => setDraft(d => ({ ...d, observacoes: e.target.value }))} />{errors.observacoes && <span id="error-observacoes" className="field-error">{errors.observacoes}</span>}</label>
    </div></fieldset>
    <p className="finance-note">O tipo não pode ser alterado após o cadastro. Saldo, baixas, estornos e status são controlados pelo servidor. Edição exige lançamento aberto sem qualquer baixa registrada.</p>
    <div className="movement-actions"><button type="submit" className="button button-primary" disabled={saving || !catalogs || !options.categoriaId.length}>{saving ? 'Salvando…' : entry ? 'Salvar alterações' : 'Cadastrar lançamento'}</button>{!saving && <Link className="button button-secondary" to={entry ? `/app/lancamentos/${entry.id}` : '/app/lancamentos'}>Voltar sem salvar</Link>}</div>
  </form>
}
