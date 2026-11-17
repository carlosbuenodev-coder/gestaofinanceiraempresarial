import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthProvider'
import { useAuth } from '../hooks/useAuth'
import { AppRoutes } from '../routes/AppRoutes'
import { cashflowApi } from '../services/cashflow'
import { monthlyRows } from '../utils/charts'
import { money, presetRange, validDate } from '../utils/format'

const usuario = { id: 12, empresaId: 7, nome: 'Pessoa Teste', email: 'teste@teste.invalid', perfil: 'CONSULTA', ativo: true }
const range = presetRange('current')
const totals = { receitasPrevistas: 200, despesasPrevistas: 70, receitasRealizadas: 100, despesasRealizadas: 40, saldoPrevisto: 130, saldoRealizado: 60 }
const resumo = { ...totals, empresaId: 7, ...range, valoresPendentes: 270, quantidadeLancamentos: 3, quantidadeBaixasValidas: 2 }
const periodo = { ...totals, data: `${range.dataInicial}T00:00:00` }
const conta = { contaBancariaId: 5, receitasRealizadas: 100, despesasRealizadas: 40, saldoRealizado: 60, saldoBancarioAcumulado: null }
const movimento = { lancamentoId: 1, baixaId: null, contaBancariaId: null, tipo: 'RECEITA', descricao: 'Movimento apenas de teste', categoriaId: 2, centroCustoId: null, data: periodo.data, principal: 200, juros: 0, multa: 0, desconto: 0 }
const page = <T,>(itens: T[], total = itens.length, pagina = 1) => ({ empresaId: 7, pagina, tamanhoPagina: 100, total, itens })
const dashboard = () => ({ resumo: { ...resumo }, porPeriodo: page([periodo]), porConta: page([conta]) })
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
function Harness() {
  const auth = useAuth()

  return <><button onClick={() => void auth.login({ empresaId: 7, email: usuario.email, senha: 'somente-mock' })}>Iniciar sessão de teste</button><AppRoutes /></>
}
function renderApp() {
  return render(<MemoryRouter initialEntries={['/app/dashboard']}><AuthProvider><Harness /></AuthProvider></MemoryRouter>)
}
async function login() { await userEvent.click(screen.getByRole('button', { name: 'Iniciar sessão de teste' })); await screen.findByRole('heading', { name: 'Olá, Pessoa.' }); await userEvent.click(screen.getByRole('link', { name: 'Dashboard' })) }
beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async (input: string | URL | Request) => {
    const url = String(input)
    if (url.endsWith('/auth/usuarios/login')) return json({ token: 'mock-only', tipo: 'Bearer', expiraEmUtc: new Date(Date.now() + 3600000).toISOString(), usuario })
    if (url.endsWith('/usuarios/me')) return json(usuario)
    if (url.includes('/dashboard?')) return json(dashboard())
    if (url.includes('/previsto?') || url.includes('/realizado?')) return json(page([movimento]))
    throw Error('Unexpected test request')
  }))
})

describe('Dashboard protegido e integrado', () => {
  it('protege a rota sem sessão e não consulta finanças', () => {
    renderApp()
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('exibe indicadores BRL, gráficos acessíveis e contas com GET autorizado sem escopo arbitrário', async () => {
    renderApp(); await login()
    expect(await screen.findByRole('heading', { name: 'Dashboard financeiro' })).toBeTruthy()
    await screen.findByRole('heading', { name: 'Receitas previstas' })
    expect(screen.getAllByText(money(200).replace(/\u00a0/g, ' ')).length).toBeGreaterThan(0)
    expect(screen.getAllByRole('img').length).toBe(3)
    expect(screen.getByText('Conta #5')).toBeTruthy()
    const call = vi.mocked(fetch).mock.calls.find(c => String(c[0]).includes('/fluxo-caixa/'))!
    expect(call[1]?.method).toBe('GET')
    expect(call[1]?.headers).toMatchObject({ Authorization: 'Bearer mock-only' })
    expect(String(call[0])).not.toContain('empresaId')
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it('mostra carregamento sem valores financeiros provisórios', async () => {
    const original = fetch
    vi.stubGlobal('fetch', vi.fn((input, options) => String(input).includes('/dashboard?') ? new Promise(() => {}) : original(input, options)))
    renderApp(); await login()
    expect(await screen.findByText('Carregando informações financeiras…')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Receitas previstas' })).toBeNull()
  })
  it('trata indisponibilidade e permite tentar novamente sem exibir zeros', async () => {
    const original = fetch
    let failed = true
    vi.stubGlobal('fetch', vi.fn((input, options) => String(input).includes('/dashboard?') && failed ? Promise.resolve(json({ stack: 'segredo interno' }, 500)) : original(input, options)))
    renderApp(); await login()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByText(/segredo interno/)).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Receitas previstas' })).toBeNull()
    failed = false
    await userEvent.click(screen.getByRole('button', { name: /^Tentar novamente$/ }))
    expect(await screen.findByRole('heading', { name: 'Receitas previstas' })).toBeTruthy()
  })
  it('mostra ausência real de movimentos e gráficos vazios', async () => {
    const original = fetch
    const empty = { resumo: { ...resumo, ...Object.fromEntries(Object.keys(totals).map(k => [k, 0])), valoresPendentes: 0, quantidadeLancamentos: 0, quantidadeBaixasValidas: 0 }, porPeriodo: page([]), porConta: page([]) }
    vi.stubGlobal('fetch', vi.fn((input, options) => String(input).includes('/dashboard?') ? Promise.resolve(json(empty)) : original(input, options)))
    renderApp(); await login()
    expect(await screen.findByText('Sem movimentações no período selecionado.')).toBeTruthy()
    expect(screen.getAllByText('Sem dados para este gráfico.').length).toBe(3)
  })
  it.each([null, {}, { ...dashboard(), resumo: { ...resumo, receitasPrevistas: '200' } }, { ...dashboard(), porConta: { ...page([conta]), empresaId: 8 } }, { ...dashboard(), resumo: { ...resumo, empresaId: 8 } }])('recusa resposta inesperada ou de outra empresa', async value => {
    const original = fetch
    vi.stubGlobal('fetch', vi.fn((input, options) => String(input).includes('/dashboard?') ? Promise.resolve(json(value)) : original(input, options)))
    renderApp(); await login()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByText('Conta #5')).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Receitas previstas' })).toBeNull()
  })
  it.each([401, 403])('retira o dashboard quando a API responde %s', async status => {
    const original = fetch
    vi.stubGlobal('fetch', vi.fn((input, options) => String(input).includes('/dashboard?') ? Promise.resolve(json({}, status)) : original(input, options)))
    renderApp(); await login()
    await waitFor(() => expect(screen.getByRole('heading', { name: status === 401 ? 'Acesse sua conta' : 'Não foi possível liberar o acesso.' })).toBeTruthy())
    expect(screen.queryByRole('heading', { name: 'Receitas previstas' })).toBeNull()
  })
  it('aplica datas somente ao enviar e atualiza indicadores pelos novos resultados oficiais', async () => {
    renderApp(); await login(); await screen.findByText('Conta #5')
    const count = vi.mocked(fetch).mock.calls.length
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2026-01-01' } })
    fireEvent.change(screen.getByLabelText('Data final'), { target: { value: '2026-01-31' } })
    expect(fetch).toHaveBeenCalledTimes(count)
    vi.mocked(fetch).mockResolvedValueOnce(json({ ...dashboard(), resumo: { ...resumo, dataInicial: '2026-01-01', dataFinal: '2026-01-31', receitasPrevistas: 999 }, porPeriodo: page([{ ...periodo, data: '2026-01-10T00:00:00' }]) }))
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(screen.getAllByText(money(999).replace(/\u00a0/g, ' ')).length).toBeGreaterThan(0))
    expect(String(vi.mocked(fetch).mock.calls.at(-1)![0])).toContain('dataInicial=2026-01-01&dataFinal=2026-01-31')
  })
  it('rejeita datas invertidas e vazias sem consultar novamente', async () => {
    renderApp(); await login(); await screen.findByText('Conta #5')
    const count = vi.mocked(fetch).mock.calls.length
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2099-01-01' } })
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(screen.getByRole('alert')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '' } })
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(fetch).toHaveBeenCalledTimes(count)
  })
  it('consulta previstos e realizados sob demanda sem calcular totais com páginas de movimentos', async () => {
    renderApp(); await login(); await screen.findByText('Conta #5')
    await userEvent.click(screen.getByRole('button', { name: 'Ver pendências previstas' }))
    expect(await screen.findByText(movimento.descricao)).toBeTruthy()
    expect(vi.mocked(fetch).mock.calls.some(c => String(c[0]).includes('/previsto?'))).toBe(true)
    await userEvent.click(screen.getByRole('button', { name: 'Ver baixas realizadas' }))
    expect(await screen.findByText('Baixas por pagamento')).toBeTruthy()
    expect(vi.mocked(fetch).mock.calls.some(c => String(c[0]).includes('/realizado?'))).toBe(true)
  })
  it('ignora resposta antiga após logout', async () => {
    renderApp(); await login(); await screen.findByText('Conta #5')
    let resolve!: (r: Response) => void
    vi.mocked(fetch).mockImplementationOnce(() => new Promise(r => { resolve = r }))
    await userEvent.click(screen.getByRole('button', { name: 'Atualizar dashboard' }))
    // Unmount also aborts in-flight reads and must not restore protected data.
    await userEvent.click(screen.getByRole('button', { name: 'Sair da conta' }))
    await act(async () => resolve(json(dashboard())))
    expect(screen.queryByRole('heading', { name: 'Receitas previstas' })).toBeNull()
  })
  it('permite escolher todos os períodos predefinidos sem disparar consulta', async () => {
    renderApp(); await login(); await screen.findByText('Conta #5')
    const count = vi.mocked(fetch).mock.calls.length
    for (const preset of ['previous', '90', 'year', 'current', 'custom']) await userEvent.selectOptions(screen.getByLabelText('Período'), preset)
    expect(fetch).toHaveBeenCalledTimes(count)
    expect(within(screen.getByRole('form', { name: 'Filtros do dashboard' })).getByLabelText('Data inicial')).toBeTruthy()
  })
})

describe('Contratos, paginação e formatação', () => {
  it('agrupa somente os meses existentes com resultados oficiais, incluindo negativos', () => {
    expect(monthlyRows([{ ...periodo, data: '2026-01-01T00:00:00', saldoPrevisto: -20, saldoRealizado: 10 }, { ...periodo, data: '2026-01-02T00:00:00', saldoPrevisto: 5, saldoRealizado: 10 }, { ...periodo, data: '2026-03-01T00:00:00', saldoPrevisto: -1, saldoRealizado: -2 }])).toEqual([{ label: '01/2026', values: [-15, 20] }, { label: '03/2026', values: [-1, -2] }])
  })
  it('formata BRL e datas locais incluindo ano bissexto', () => {
    expect(money(1234.56)).toBe('R$\u00a01.234,56')
    expect(validDate('2026-02-30')).toBe(false)
    expect(validDate('2024-02-29')).toBe(true)
    expect(presetRange('previous', new Date(2026, 0, 5))).toEqual({ dataInicial: '2025-12-01', dataFinal: '2025-12-31' })
    expect(presetRange('90', new Date(2026, 2, 1))).toEqual({ dataInicial: '2025-12-02', dataFinal: '2026-03-01' })
  })
  it('completa páginas do período e das contas sem truncar gráficos', async () => {
    const first = dashboard()
    first.porPeriodo = page(Array.from({ length: 100 }, () => periodo), 101)
    first.porConta = page(Array.from({ length: 100 }, (_, i) => ({ ...conta, contaBancariaId: i + 1 })), 101)
    const request = vi.fn().mockResolvedValueOnce(first).mockResolvedValueOnce(page([periodo], 101, 2)).mockResolvedValueOnce(page([{ ...conta, contaBancariaId: 101 }], 101, 2))
    const result = await cashflowApi(request, 7, range).dashboard()
    expect(result.porPeriodo.itens.length).toBe(101); expect(result.porConta.itens.length).toBe(101)
    expect(request.mock.calls[1][0]).toContain('/por-periodo?'); expect(request.mock.calls[2][0]).toContain('/por-conta?')
  })
  it('recusa mudança de empresa ou total entre páginas', async () => {
    const first = dashboard(); first.porPeriodo = page(Array.from({ length: 100 }, () => periodo), 101)
    for (const next of [{ ...page([periodo], 101, 2), empresaId: 8 }, page([periodo, periodo], 102, 2)]) {
      const request = vi.fn().mockResolvedValueOnce(first).mockResolvedValueOnce(next)
      await expect(cashflowApi(request, 7, range).dashboard()).rejects.toMatchObject({ code: 'unexpected' })
    }
  })
  it('integra o resumo e valida o intervalo devolvido', async () => {
    const request = vi.fn().mockResolvedValueOnce(resumo).mockResolvedValueOnce({ ...resumo, dataFinal: '2000-01-01' })
    expect(await cashflowApi(request, 7, range).resumo()).toEqual(resumo)
    expect(request.mock.calls[0][0]).toContain('/resumo?')
    await expect(cashflowApi(request, 7, range).resumo()).rejects.toMatchObject({ code: 'unexpected' })
  })
})
