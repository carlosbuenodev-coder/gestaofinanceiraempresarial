import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Link, MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthProvider'
import { useAuth } from '../hooks/useAuth'
import { AppRoutes } from '../routes/AppRoutes'
import { presetRange } from '../utils/format'

const usuario = { id: 12, empresaId: 7, nome: 'Pessoa Teste', email: 'teste@teste.invalid', perfil: 'ADMIN', ativo: true }
const totals = {
  receitasPrevistas: 1234.56, despesasPrevistas: 101.02, receitasRealizadas: 20, despesasRealizadas: 85,
  saldoPrevisto: 87.65, saldoRealizado: -65, valoresPendentes: 1335.58, quantidadeLancamentos: 3, quantidadeBaixasValidas: 2,
}
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const summaryCalls = () => vi.mocked(fetch).mock.calls.filter(([url]) => String(url).includes('/fluxo-caixa/'))
const movementCalls = () => summaryCalls().filter(([url]) => String(url).includes('/realizado?'))
const params = (url: string) => new URL(url, 'http://local').searchParams
let profile: string
let getSummary: (url: URL) => Response | Promise<Response>
let getAccounts: (url: URL) => Response | Promise<Response>
let getMovements: (url: URL) => Response | Promise<Response>
const account = { contaBancariaId: 15, receitasRealizadas: 80, despesasRealizadas: 100.01, saldoRealizado: -20.01, saldoBancarioAcumulado: null }
const accounts = (items = [account], empresaId = 7) => ({
  empresaId, pagina: 1, tamanhoPagina: 100, total: items.length, itens: items,
})

function TestSession() {
  const { login } = useAuth()
  return <>
    <button onClick={() => void login({ empresaId: 7, email: usuario.email, senha: 'somente-mock' })}>Iniciar sessão simulada</button>
    <Link to="/app/fluxo-caixa?empresaId=8">Abrir com parâmetro de outra empresa</Link>
    <AppRoutes />
  </>
}

function app(path = '/app') {
  return render(<MemoryRouter initialEntries={[path]}><AuthProvider><TestSession /></AuthProvider></MemoryRouter>)
}

async function openCashflow(fromForeignLink = false) {
  fireEvent.click(screen.getByRole('button', { name: 'Iniciar sessão simulada' }))
  await screen.findByRole('heading', { name: 'Olá, Pessoa.' })
  fireEvent.click(screen.getByRole('link', { name: fromForeignLink ? 'Abrir com parâmetro de outra empresa' : 'Fluxo de caixa' }))
}

function summary(url: URL, overrides = {}) {
  return {
    ...totals, empresaId: 7, dataInicial: url.searchParams.get('dataInicial'), dataFinal: url.searchParams.get('dataFinal'), ...overrides,
  }
}

function movementPage(url: URL, { total = 1, empresaId = 7, descricao = 'Pagamento de teste' } = {}) {
  const pagina = Number(url.searchParams.get('pagina'))
  const offset = (pagina - 1) * 100
  return {
    empresaId, pagina, tamanhoPagina: 100, total,
    itens: Array.from({ length: Math.min(100, Math.max(0, total - offset)) }, (_, index) => ({
      lancamentoId: offset + index + 1, baixaId: offset + index + 1, contaBancariaId: 15,
      tipo: url.searchParams.get('tipo') ?? 'DESPESA', descricao: total > 1 ? `${descricao} ${offset + index + 1}` : descricao,
      categoriaId: 4, centroCustoId: null, data: `${url.searchParams.get('dataInicial')}T00:00:00`,
      principal: 85.25, juros: 4, multa: 2, desconto: 1,
    })),
  }
}

function openMovements() {
  fireEvent.click(screen.getByRole('button', { name: 'Ver movimentações realizadas' }))
}

const movementTable = () => screen.findByRole('table', { name: 'Movimentações realizadas' })

beforeEach(() => {
  profile = 'ADMIN'
  getSummary = url => json(summary(url))
  getAccounts = () => json(accounts())
  getMovements = url => json(movementPage(url))
  vi.stubGlobal('fetch', vi.fn(async input => {
    const url = new URL(String(input), 'http://local')
    if (url.pathname === '/auth/usuarios/login') {
      return json({ token: 'mock-only', tipo: 'Bearer', expiraEmUtc: new Date(Date.now() + 3600000).toISOString(), usuario: { ...usuario, perfil: profile } })
    }
    if (url.pathname === '/usuarios/me') return json({ ...usuario, perfil: profile })
    if (url.pathname === '/fluxo-caixa/resumo') return getSummary(url)
    if (url.pathname === '/fluxo-caixa/por-conta') return getAccounts(url)
    if (url.pathname === '/fluxo-caixa/realizado') return getMovements(url)
    throw new Error('Requisição não simulada bloqueada')
  }))
})

describe('Detalhamento das movimentações realizadas', () => {
  it.each([200, 409])('carrega sob demanda com resumo %s e preserva a visão já exibida', async status => {
    profile = 'CONSULTA'
    if (status === 409) getSummary = () => json({}, 409)
    app(); await openCashflow(true)
    const overview = status === 409 ? 'Realizado por conta' : 'Resumo do período'
    await screen.findByRole('table', { name: overview })
    expect(movementCalls()).toHaveLength(0)
    openMovements()
    const table = await movementTable()
    const row = within(table).getByRole('row', { name: /Pagamento de teste/ })
    expect(within(row).getAllByRole('cell').map(cell => cell.textContent?.replace(/\s/g, ' ')))
      .toEqual([presetRange('current').dataInicial.split('-').reverse().join('/'), 'Despesa', 'Conta #15', 'R$ 85,25'])
    expect(within(table).getByRole('columnheader', { name: 'Principal realizado' })).toBeTruthy()
    expect(screen.getByRole('table', { name: overview })).toBeTruthy()
    const [[url, options]] = movementCalls()
    expect(Object.fromEntries(params(String(url)))).toEqual({ ...presetRange('current'), pagina: '1', tamanhoPagina: '100' })
    expect(options).toMatchObject({ method: 'GET', headers: { Authorization: 'Bearer mock-only' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ocultar movimentações realizadas' }))
    expect(screen.queryByRole('table', { name: 'Movimentações realizadas' })).toBeNull()
  })

  it('pagina, oculta linhas antigas durante a consulta e reinicia ao aplicar novos filtros', async () => {
    let finishPage!: () => void
    getMovements = url => url.searchParams.get('pagina') === '2'
      ? new Promise(resolve => { finishPage = () => resolve(json(movementPage(url, { total: 101, descricao: 'Página antiga' }))) })
      : json(movementPage(url, { total: 101 }))
    app(); await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    expect(within(await movementTable()).getAllByRole('row')).toHaveLength(101)
    expect((screen.getByRole('button', { name: 'Anterior' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('Carregando movimentações realizadas…')).toBeTruthy()
    expect(screen.queryByRole('table', { name: 'Movimentações realizadas' })).toBeNull()
    const [pendingUrl, pendingOptions] = movementCalls().at(-1)!
    expect(params(String(pendingUrl)).get('pagina')).toBe('2')

    fireEvent.change(screen.getByLabelText('Tipo de movimentação'), { target: { value: 'RECEITA' } })
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2026-10-01' } })
    fireEvent.change(screen.getByLabelText('Data final'), { target: { value: '2026-10-31' } })
    expect(movementCalls()).toHaveLength(2)
    getMovements = url => json(movementPage(url, { total: 0 }))
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Nenhuma movimentação realizada para os filtros aplicados.')).toBeTruthy()
    expect(pendingOptions?.signal?.aborted).toBe(true)
    expect(Object.fromEntries(params(String(movementCalls().at(-1)?.[0])))).toEqual({
      dataInicial: '2026-10-01', dataFinal: '2026-10-31', tipo: 'RECEITA', pagina: '1', tamanhoPagina: '100',
    })
    await act(async () => finishPage())
    expect(screen.queryByText('Página antiga 101')).toBeNull()
  })

  it('navega até a última página e retorna à primeira', async () => {
    getMovements = url => json(movementPage(url, { total: 101 }))
    app(); await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    await movementTable()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByRole('rowheader', { name: 'Pagamento de teste 101' })).toBeTruthy()
    expect(screen.getByText('Página 2 de 2')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Próxima' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(await screen.findByRole('rowheader', { name: 'Pagamento de teste 1' })).toBeTruthy()
    expect(screen.getByText('Página 1 de 2')).toBeTruthy()
  })

  it.each([403, 500, 200])('preserva o resumo e permite repetir após resposta %s inválida para a consulta', async status => {
    getMovements = url => status === 200 ? json(movementPage(url, { empresaId: 8 })) : json({ mensagem: 'Detalhe privado' }, status)
    app(); await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    const alert = await screen.findByRole('alert')
    const message = status === 403 ? 'Você não tem permissão' : status === 500 ? 'O sistema está temporariamente indisponível' : 'Não foi possível confirmar a resposta'
    expect(alert.textContent).toContain(message)
    expect(screen.queryByText('Detalhe privado')).toBeNull()
    expect(screen.queryByText('Pagamento de teste')).toBeNull()
    expect(screen.getByRole('table', { name: 'Resumo do período' })).toBeTruthy()
    getMovements = url => json(movementPage(url))
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente o detalhamento' }))
    expect(await movementTable()).toBeTruthy()
    expect(movementCalls()[1][0]).toBe(movementCalls()[0][0])
  })

  it('encerra a sessão quando o detalhamento retorna 401', async () => {
    getMovements = () => json({}, 401)
    app(); await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Fluxo de caixa' })).toBeNull()
  })

  it('descarta o resultado de uma sessão anterior mesmo ao entrar na mesma empresa', async () => {
    let finishSession!: () => void
    getMovements = url => new Promise(resolve => { finishSession = () => resolve(json(movementPage(url, { descricao: 'Sessão anterior' }))) })
    app(); await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    const signal = movementCalls()[0][1]?.signal
    fireEvent.click(screen.getByRole('button', { name: 'Sair da conta' }))
    expect(signal?.aborted).toBe(true)
    getMovements = url => json(movementPage(url))
    await openCashflow()
    await screen.findByRole('table', { name: 'Resumo do período' })
    openMovements()
    expect(await movementTable()).toBeTruthy()
    await act(async () => finishSession())
    expect(screen.queryByText('Sessão anterior')).toBeNull()
    expect(screen.getByText('Pagamento de teste')).toBeTruthy()
  })
})

describe('Tela de fluxo de caixa', () => {
  it('protege a rota sem sessão e não consulta a API', () => {
    app('/app/fluxo-caixa')
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })

  it.each(['ADMIN', 'FINANCEIRO', 'CONSULTA'])('abre pelo menu para %s e mostra os valores oficiais em reais', async role => {
    profile = role
    app(); await openCashflow()
    expect(await screen.findByRole('heading', { name: 'Fluxo de caixa' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Fluxo de caixa' }).getAttribute('aria-current')).toBe('page')
    const table = await screen.findByRole('table', { name: 'Resumo do período' })
    const rowValues = (label: string) => within(within(table).getByRole('row', { name: new RegExp(label) }))
      .getAllByRole('cell').map(cell => cell.textContent?.replace(/\s/g, ' '))
    expect(rowValues('Receitas')).toEqual(['R$ 1.234,56', 'R$ 20,00'])
    expect(rowValues('Despesas')).toEqual(['R$ 101,02', 'R$ 85,00'])
    expect(rowValues('Resultado líquido')).toEqual(['R$ 87,65', '-R$ 65,00'])
    const [[url, options]] = summaryCalls()
    const range = presetRange('current')
    expect(params(String(url)).get('dataInicial')).toBe(range.dataInicial)
    expect(params(String(url)).get('dataFinal')).toBe(range.dataFinal)
    expect(options).toMatchObject({ method: 'GET', headers: { Authorization: 'Bearer mock-only' } })
    expect(summaryCalls()).toHaveLength(1)
  })

  it('valida o período e envia datas e tipo somente ao aplicar os filtros', async () => {
    app(); await openCashflow()
    await screen.findByRole('table')
    const start = screen.getByLabelText('Data inicial')
    const end = screen.getByLabelText('Data final')
    const type = screen.getByLabelText('Tipo de movimentação')
    const apply = screen.getByRole('button', { name: 'Aplicar filtros' })
    fireEvent.change(start, { target: { value: '2026-10-31' } })
    fireEvent.change(end, { target: { value: '2026-10-01' } })
    fireEvent.change(type, { target: { value: 'DESPESA' } })
    expect(summaryCalls()).toHaveLength(1)
    fireEvent.click(apply)
    expect(screen.getByRole('alert').textContent).toContain('Informe datas válidas')
    expect(summaryCalls()).toHaveLength(1)
    fireEvent.change(start, { target: { value: '2026-10-01' } })
    fireEvent.change(end, { target: { value: '2026-10-31' } })
    expect(summaryCalls()).toHaveLength(1)
    fireEvent.click(apply)
    await screen.findByText('01/10/2026 a 31/10/2026 · Despesas')
    expect(Object.fromEntries(params(String(summaryCalls().at(-1)?.[0])))).toMatchObject({
      dataInicial: '2026-10-01', dataFinal: '2026-10-31', tipo: 'DESPESA',
    })
    fireEvent.change(type, { target: { value: '' } })
    fireEvent.click(apply)
    await screen.findByText('01/10/2026 a 31/10/2026 · Receitas e despesas')
    expect(params(String(summaryCalls().at(-1)?.[0])).has('tipo')).toBe(false)
  })

  it('mostra carregamento, ignora resposta antiga e informa quando não há movimentos', async () => {
    let finishFirst!: () => void
    getSummary = url => new Promise(resolve => { finishFirst = () => resolve(json(summary(url))) })
    app(); await openCashflow()
    expect(await screen.findByText('Carregando fluxo de caixa…')).toBeTruthy()
    expect(screen.queryByRole('table')).toBeNull()
    const firstSignal = summaryCalls()[0][1]?.signal
    getSummary = url => json(summary(url, {
      receitasPrevistas: 0, despesasPrevistas: 0, receitasRealizadas: 0, despesasRealizadas: 0,
      saldoPrevisto: 0, saldoRealizado: 0, valoresPendentes: 0, quantidadeLancamentos: 0, quantidadeBaixasValidas: 0,
    }))
    fireEvent.change(screen.getByLabelText('Tipo de movimentação'), { target: { value: 'RECEITA' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Sem movimentações no período selecionado.')).toBeTruthy()
    expect(firstSignal?.aborted).toBe(true)
    await act(async () => finishFirst())
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('ignora empresa na URL e rejeita resumo de outro escopo', async () => {
    getSummary = url => json(summary(url, { empresaId: 8 }))
    app(); await openCashflow(true)
    expect((await screen.findByRole('alert')).textContent).toContain('Não foi possível confirmar a resposta')
    expect(params(String(summaryCalls()[0][0])).has('empresaId')).toBe(false)
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('encerra a sessão quando a consulta retorna 401', async () => {
    getSummary = () => json({}, 401)
    app(); await openCashflow()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Fluxo de caixa' })).toBeNull()
  })

  it.each([
    [403, 'Você não tem permissão para esta operação'],
    [500, 'O sistema está temporariamente indisponível'],
  ])('mostra erro %s sem expor dados internos e permite nova consulta', async (status, message) => {
    getSummary = () => json({ mensagem: 'Detalhe interno de teste' }, status)
    app(); await openCashflow()
    expect((await screen.findByRole('alert')).textContent).toContain(message)
    expect(screen.queryByText('Detalhe interno de teste')).toBeNull()
    expect(screen.queryByRole('table')).toBeNull()
    expect(screen.getByRole('button', { name: 'Sair da conta' })).toBeTruthy()
    getSummary = url => json(summary(url))
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByRole('table')).toBeTruthy()
  })

  it('usa os mesmos filtros no resultado parcial e exibe só os valores oficiais por conta', async () => {
    profile = 'CONSULTA'
    app(); await openCashflow(true)
    await screen.findByRole('table')
    getSummary = () => json({ mensagem: 'Detalhe privado' }, 409)
    fireEvent.change(screen.getByLabelText('Tipo de movimentação'), { target: { value: 'DESPESA' } })
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2026-10-01' } })
    fireEvent.change(screen.getByLabelText('Data final'), { target: { value: '2026-10-31' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByRole('heading', { name: 'Resultado parcial do período' })).toBeTruthy()
    const table = await screen.findByRole('table', { name: 'Realizado por conta' })
    expect(within(table).getAllByRole('cell').map(cell => cell.textContent?.replace(/\s/g, ' ')))
      .toEqual(['R$ 80,00', 'R$ 100,01', '-R$ 20,01'])
    expect(screen.getByText('Valores previstos indisponíveis.')).toBeTruthy()
    expect(screen.queryByRole('columnheader', { name: 'Previsto' })).toBeNull()
    expect(screen.queryByText('Detalhe privado')).toBeNull()
    const [[summaryUrl], [accountsUrl, options]] = summaryCalls().slice(-2)
    expect(String(accountsUrl)).toContain('/fluxo-caixa/por-conta?')
    expect(params(String(accountsUrl)).toString()).toBe(params(String(summaryUrl)).toString())
    expect(params(String(accountsUrl)).has('empresaId')).toBe(false)
    expect(options).toMatchObject({ method: 'GET', headers: { Authorization: 'Bearer mock-only' } })
  })

  it.each([
    [403, 'Você não tem permissão'],
    [500, 'O sistema está temporariamente indisponível'],
  ])('mantém o aviso e permite tentar novamente se o realizado retornar %s', async (status, message) => {
    getSummary = () => json({}, 409)
    getAccounts = () => json({ mensagem: 'Erro interno' }, status)
    app(); await openCashflow()
    expect(await screen.findByText(new RegExp(message))).toBeTruthy()
    expect(screen.getByText('Valores previstos indisponíveis.')).toBeTruthy()
    expect(screen.queryByRole('table')).toBeNull()
    expect(screen.queryByText('Erro interno')).toBeNull()
    expect(screen.getByRole('button', { name: 'Sair da conta' })).toBeTruthy()
    getSummary = url => json(summary(url))
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByRole('table', { name: 'Resumo do período' })).toBeTruthy()
  })

  it('encerra a sessão se o realizado retornar 401', async () => {
    getSummary = () => json({}, 409)
    getAccounts = () => json({}, 401)
    app(); await openCashflow()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByText('Valores previstos indisponíveis.')).toBeNull()
  })

  it('recusa dados do realizado de outra empresa', async () => {
    getSummary = () => json({}, 409)
    getAccounts = () => json(accounts([account], 8))
    app(); await openCashflow()
    expect(await screen.findByText(/Não foi possível carregar os valores realizados/)).toBeTruthy()
    expect(screen.queryByRole('rowheader', { name: 'Conta #15' })).toBeNull()
  })

  it('distingue realizado vazio e ignora uma resposta parcial de filtros anteriores', async () => {
    let finishAccounts!: () => void
    getSummary = () => json({}, 409)
    getAccounts = () => new Promise(resolve => { finishAccounts = () => resolve(json(accounts())) })
    app(); await openCashflow()
    expect(await screen.findByText('Carregando valores realizados…')).toBeTruthy()
    const pending = summaryCalls().at(-1)?.[1]?.signal
    getAccounts = () => json(accounts([]))
    fireEvent.change(screen.getByLabelText('Tipo de movimentação'), { target: { value: 'RECEITA' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Sem movimentações realizadas no período selecionado.')).toBeTruthy()
    expect(pending?.aborted).toBe(true)
    await act(async () => finishAccounts())
    expect(screen.queryByRole('table')).toBeNull()
  })
})
