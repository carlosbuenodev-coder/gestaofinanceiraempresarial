import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Link, MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthProvider'
import { AppRoutes } from '../routes/AppRoutes'
import { useAuth } from '../hooks/useAuth'
import { entriesApi } from '../services/entries'
import { emptyFilters } from '../types/entries'
import type { Entry } from '../types/entries'
import { draftEntry, entryPayload, validateEntry } from '../utils/entryForm'
import { money } from '../utils/format'

const catalogRoute = '/cadastros-financeiros'
const catalogs = {
  empresaId: 7,
  categorias: [{ id: 2, nome: 'Serviços contratados', tipo: 'DESPESA' }, { id: 6, nome: 'Vendas', tipo: 'RECEITA' }],
  clientes: [{ id: 4, nome: 'Cliente de teste', ativo: true }, { id: 9, nome: 'Cliente inativo', ativo: false }],
  fornecedores: [{ id: 3, nome: 'Fornecedor antigo', ativo: false }, { id: 8, nome: 'Fornecedor de teste', ativo: true }],
  centrosCusto: [{ id: 5, nome: 'Operações' }],
}
const usuario = { id: 12, empresaId: 7, nome: 'Pessoa Teste', email: 'somente@teste.invalid', perfil: 'ADMIN', ativo: true }
const entry: Entry = { id: 10, empresaId: 7, tipo: 'DESPESA', descricao: 'Serviço somente de teste', clienteId: null, fornecedorId: 3, categoriaId: 2, centroCustoId: null, dataEmissao: '2026-11-01T00:00:00', dataVencimento: '2026-11-20T00:00:00', valor: 250.50, status: 'ABERTO', observacoes: 'Observação de teste', criadoEm: null }
const balance = { lancamentoId: 10, empresaId: 7, valorOriginal: 250.50, totalBaixado: 50, saldoRestante: 200.50, status: 'PARCIAL' }
const pagina = (itens = [entry], total = itens.length, page = 1) => ({ itens, total, pagina: page, tamanhoPagina: 20, totalPaginas: Math.ceil(total / 20) })
const json = (v: unknown, status = 200) => new Response(JSON.stringify(v), { status, headers: { 'Content-Type': 'application/json' } })
let profile = 'ADMIN'
let listItems = [entry]
let readEntry = entry
let interceptor: (url: string, options?: RequestInit) => Response | Promise<Response> | undefined
const calls = () => vi.mocked(fetch).mock.calls
const writes = () => calls().filter(c => c[1]?.method === 'POST' && String(c[0]).startsWith('/lancamentos') || c[1]?.method === 'PUT')
function Harness({ target }: { target: string }) {
  const auth = useAuth()
  return <><button onClick={() => void auth.login({ empresaId: 7, email: usuario.email, senha: 'mock-apenas' })}>Login simulado</button><Link to={target}>Abrir tela de teste</Link><AppRoutes /></>
}
function app(target = '/app/lancamentos') { return render(<MemoryRouter initialEntries={[target]}><AuthProvider><Harness target={target} /></AuthProvider></MemoryRouter>) }
async function login() {
  await userEvent.click(screen.getByRole('button', { name: 'Login simulado' }))
  await screen.findByRole('heading', { name: 'Olá, Pessoa.' })
  await userEvent.click(screen.getByRole('link', { name: 'Abrir tela de teste' }))
}
async function fill() {
  await screen.findByRole('option', { name: 'Vendas' })
  fireEvent.change(screen.getByLabelText('Descrição *'), { target: { value: 'Receita teste' } })
  await userEvent.selectOptions(screen.getByLabelText('Categoria *'), '6')
  fireEvent.change(screen.getByLabelText('Valor original (R$) *'), { target: { value: '1234,56' } })
}
beforeEach(() => {
  profile = 'ADMIN'; listItems = [entry]; readEntry = entry; interceptor = () => undefined
  vi.stubGlobal('fetch', vi.fn(async (input: string | URL | Request, options?: RequestInit) => {
    const url = String(input)
    const intercepted = interceptor(url, options)
    if (intercepted !== undefined) return intercepted
    if (url.endsWith('/auth/usuarios/login')) return json({ token: 'mock-financeiro-only', tipo: 'Bearer', expiraEmUtc: new Date(Date.now() + 3600000).toISOString(), usuario: { ...usuario, perfil: profile } })
    if (url.endsWith('/usuarios/me')) return json({ ...usuario, perfil: profile })
    if (url.endsWith(catalogRoute)) return json(catalogs)
    if (url.startsWith('/lancamentos?')) return json(pagina(listItems))
    if (url === '/lancamentos/10/saldo') return json(balance)
    if (url === '/lancamentos/10/cancelamento') return json({ ...entry, status: 'CANCELADO' })
    if (url === '/lancamentos' && options?.method === 'POST') {
      const payload = JSON.parse(String(options.body)); readEntry = { ...entry, ...payload, dataEmissao: payload.dataEmissao + 'T00:00:00', dataVencimento: payload.dataVencimento + 'T00:00:00' }
      return json(readEntry, 201)
    }
    if (url === '/lancamentos/10' && options?.method === 'PUT') { const payload = JSON.parse(String(options.body)); readEntry = { ...entry, ...payload, dataEmissao: payload.dataEmissao + 'T00:00:00', dataVencimento: payload.dataVencimento + 'T00:00:00' }; return json(readEntry) }
    if (url === '/lancamentos/10') return json(readEntry)
    throw new Error('Requisição não simulada bloqueada')
  }))
})

describe('Telas operacionais protegidas', () => {
  it.each(['/app/lancamentos', '/app/contas-a-pagar', '/app/contas-a-receber', '/app/lancamentos/novo', '/app/lancamentos/10', '/app/lancamentos/10/editar'])('protege %s sem consultar a API', path => {
    app(path)
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('lista dados oficiais e consulta saldo somente sob demanda, com JWT em memória', async () => {
    app(); await login(); await screen.findByText(/#10 · Serviço/)
    expect(calls().some(c => String(c[0]).endsWith('/saldo'))).toBe(false)
    await userEvent.click(screen.getByRole('button', { name: 'Consultar valores de #10' }))
    await waitFor(() => expect(screen.getByText(money(200.50).replace(/\u00a0/g, ' '))).toBeTruthy())
    const call = calls().find(c => c[0] === '/lancamentos/10/saldo')!
    expect(call[1]?.headers).toMatchObject({ Authorization: 'Bearer mock-financeiro-only' })
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it.each([['/app/contas-a-pagar?tipo=RECEITA&empresaId=8', 'DESPESA'], ['/app/contas-a-receber?tipo=DESPESA&empresaId=8', 'RECEITA']])('fixa tipo e ignora escopo arbitrário em %s', async (path, tipo) => {
    listItems = [{ ...entry, tipo: tipo as Entry['tipo'] }]
    app(path); await login(); await screen.findByText(/#10 · Serviço/)
    const url = String(calls().find(c => String(c[0]).startsWith('/lancamentos?'))![0])
    expect(url).toContain(`tipo=${tipo}`); expect(url).not.toContain('empresaId')
    expect(screen.queryByLabelText('Tipo')).toBeNull()
  })
  it('aplica filtros explicitamente e rejeita datas e referências inválidas', async () => {
    app(); await login(); await screen.findByText(/#10 · Serviço/)
    const count = calls().length
    fireEvent.change(screen.getByLabelText('Vencimento inicial'), { target: { value: '2026-12-31' } })
    fireEvent.change(screen.getByLabelText('Vencimento final'), { target: { value: '2026-01-01' } })
    expect(calls().length).toBe(count)
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(screen.getByRole('alert').textContent).toContain('intervalo')
    fireEvent.change(screen.getByLabelText('Vencimento final'), { target: { value: '2026-12-31' } })
    fireEvent.change(screen.getByLabelText('Categoria (ID)'), { target: { value: '-1' } })
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(calls().length).toBe(count)
    fireEvent.change(screen.getByLabelText('Categoria (ID)'), { target: { value: '2' } })
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'ABERTO')
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(calls().length).toBeGreaterThan(count))
    expect(String(calls().at(-1)![0])).toContain('status=ABERTO')
    expect(String(calls().at(-1)![0])).toContain('vencimentoInicio=2026-12-31')
  })
  it('pagina conforme o total oficial e mantém filtros na URL', async () => {
    interceptor = url => url.startsWith('/lancamentos?') ? json(new URL(url, 'http://local').searchParams.get('pagina') === '2' ? pagina([{ ...entry, id: 30 }], 21, 2) : pagina(Array.from({ length: 20 }, (_, i) => ({ ...entry, id: i + 1 })), 21)) : undefined
    app(); await login(); await screen.findByText('Página 1 de 2')
    await userEvent.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('Página 2 de 2')).toBeTruthy()
    expect(String(calls().at(-1)![0])).toContain('pagina=2')
    expect(screen.getByRole('button', { name: 'Próxima' }).hasAttribute('disabled')).toBe(true)
  })
  it('mostra carregamento e remove dados provisórios', async () => {
    interceptor = url => url.startsWith('/lancamentos?') ? new Promise(() => {}) : undefined
    app(); await login()
    expect(screen.getByText('Carregando lançamentos…')).toBeTruthy()
    expect(screen.queryByText(/#10 ·/)).toBeNull()
  })
  it('trata lista vazia sem inventar valores', async () => {
    listItems = []
    app(); await login()
    expect(await screen.findByText('Nenhum lançamento nesta página para os filtros selecionados.')).toBeTruthy()
    expect(screen.queryByText('Saldo restante')).toBeNull()
  })
  it('oculta erros internos e oferece nova tentativa da listagem', async () => {
    interceptor = url => url.startsWith('/lancamentos?') ? json({ stack: 'privado mysql' }, 500) : undefined
    app(); await login()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByText(/privado mysql/)).toBeNull()
    interceptor = () => undefined
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText(/#10 · Serviço/)).toBeTruthy()
  })
  it.each(['ADMIN', 'FINANCEIRO'])('cadastra com perfil %s e não envia empresa, status ou saldo', async role => {
    profile = role
    app('/app/lancamentos/novo'); await login(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect(await screen.findByText('Lançamento cadastrado com sucesso.')).toBeTruthy()
    expect(writes().length).toBe(1)
    const body = JSON.parse(String(writes()[0][1]?.body))
    expect(body).toMatchObject({ tipo: 'RECEITA', descricao: 'Receita teste', valor: 1234.56, categoriaId: 6, clienteId: null })
    expect(body).not.toHaveProperty('empresaId'); expect(body).not.toHaveProperty('status'); expect(body).not.toHaveProperty('saldo')
  })
  it('valida cadastro antes de enviar e mantém os campos digitados', async () => {
    app('/app/lancamentos/novo'); await login()
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Descrição *'), { target: { value: 'Preservar descrição' } })
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect(screen.getByRole('alert').textContent).toContain('Revise')
    expect((screen.getByLabelText('Descrição *') as HTMLInputElement).value).toBe('Preservar descrição')
    expect(writes().length).toBe(0)
  })
  it('preserva o formulário em falha de rede e não repete a gravação automaticamente', async () => {
    interceptor = (url, options) => url === '/lancamentos' && options?.method === 'POST' ? Promise.reject(new TypeError('Network')) : undefined
    app('/app/lancamentos/novo'); await login(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect((await screen.findByRole('alert')).textContent).toContain('Consulte a listagem antes de reenviar')
    expect((screen.getByLabelText('Valor original (R$) *') as HTMLInputElement).value).toBe('1234,56')
    expect(writes().length).toBe(1)
  })
  it('mapeia validação do servidor sem expor seu corpo', async () => {
    interceptor = (url, options) => url === '/lancamentos' && options?.method === 'POST' ? json({ errors: { CategoriaId: ['stack mysql credencial privada'] }, detail: 'segredo interno' }, 400) : undefined
    app('/app/lancamentos/novo'); await login(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect((await screen.findByRole('alert')).textContent).toContain('Revise os campos')
    const category = screen.getByLabelText(/^Categoria \*/) as HTMLSelectElement
    expect(category.getAttribute('aria-invalid')).toBe('true')
    expect(category.value).toBe('6')
    expect(screen.queryByText(/stack mysql|segredo interno/)).toBeNull()
  })
  it.each(['/app/lancamentos/novo', '/app/lancamentos/10/editar'])('bloqueia CONSULTA na rota %s', async path => {
    profile = 'CONSULTA'; app(path); await login()
    expect(await screen.findByRole('heading', { name: 'Acesso restrito' })).toBeTruthy()
    expect(calls().some(c => String(c[0]).startsWith('/lancamentos') || String(c[0]).endsWith(catalogRoute))).toBe(false)
  })
  it('edita somente os campos do contrato, sem permitir alterar o tipo', async () => {
    app('/app/lancamentos/10/editar'); await login()
    await screen.findByRole('option', { name: 'Fornecedor antigo (inativo)' })
    expect(screen.getByLabelText('Tipo *').hasAttribute('disabled')).toBe(true)
    expect((screen.getByLabelText('Categoria *') as HTMLSelectElement).selectedOptions[0].textContent).toBe('Serviços contratados')
    expect((screen.getByLabelText('Fornecedor') as HTMLSelectElement).value).toBe('3')
    fireEvent.change(screen.getByLabelText('Descrição *'), { target: { value: 'Descrição editada' } })
    await userEvent.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect(await screen.findByText('Lançamento atualizado com sucesso.')).toBeTruthy()
    const write = writes()[0]
    expect(write[1]?.method).toBe('PUT')
    expect(JSON.parse(String(write[1]?.body))).not.toHaveProperty('tipo')
    expect(JSON.parse(String(write[1]?.body))).toMatchObject({ categoriaId: 2, fornecedorId: 3 })
  })
  it.each(['PARCIAL', 'QUITADO', 'CANCELADO'] as const)('impede edição da situação %s', async status => {
    readEntry = { ...entry, status }
    app('/app/lancamentos/10/editar'); await login()
    expect((await screen.findByRole('alert')).textContent).toContain('Somente lançamentos abertos')
    expect(screen.queryByRole('form', { name: 'Editar lançamento' })).toBeNull()
    expect(writes().length).toBe(0)
  })
  it('exige confirmação de cancelamento e aceita o resultado oficial', async () => {
    app('/app/lancamentos/10'); await login(); await screen.findByRole('heading', { name: entry.descricao })
    await userEvent.click(screen.getByRole('button', { name: /^Cancelar lançamento$/ }))
    expect(writes().length).toBe(0)
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))
    expect(await screen.findByText('Lançamento cancelado com sucesso.')).toBeTruthy()
    expect(writes()[0][0]).toBe('/lancamentos/10/cancelamento')
    expect(JSON.parse(String(writes()[0][1]?.body))).toEqual({})
    expect(screen.queryByRole('link', { name: 'Editar lançamento' })).toBeNull()
  })
  it('trata conflito de baixa no cancelamento sem anunciar sucesso', async () => {
    interceptor = url => url.endsWith('/cancelamento') ? json({ mensagem: 'interno' }, 409) : undefined
    app('/app/lancamentos/10'); await login(); await screen.findByRole('heading', { name: entry.descricao })
    await userEvent.click(screen.getByRole('button', { name: /^Cancelar lançamento$/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))
    expect((await screen.findByRole('alert')).textContent).toContain('A situação atual impede o cancelamento.')
    expect(screen.queryByText('Lançamento cancelado com sucesso.')).toBeNull()
  })
  it('encerra a sessão quando uma gravação recebe HTTP 401', async () => {
    interceptor = (url, options) => url === '/lancamentos' && options?.method === 'POST' ? json({}, 401) : undefined
    app('/app/lancamentos/novo'); await login(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect(await screen.findByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(screen.getByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByLabelText('Descrição *')).toBeNull()
  })
  it.each([
    [403, 'Você não tem permissão para esta operação.'],
    [500, 'O sistema está temporariamente indisponível.'],
  ])('preserva sessão e formulário quando o cadastro recebe HTTP %s', async (status, message) => {
    interceptor = (url, options) => url === '/lancamentos' && options?.method === 'POST' ? json({ detalhe: 'detalhe interno da API' }, status) : undefined
    app('/app/lancamentos/novo'); await login(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect((await screen.findByRole('alert')).textContent).toContain(message)
    expect(screen.queryByText(/detalhe interno da API/)).toBeNull()
    expect((screen.getByLabelText('Descrição *') as HTMLInputElement).value).toBe('Receita teste')
    expect((screen.getByLabelText('Valor original (R$) *') as HTMLInputElement).value).toBe('1234,56')
    expect((screen.getByLabelText('Categoria *') as HTMLSelectElement).value).toBe('6')
    expect(writes()).toHaveLength(1)
    await userEvent.click(screen.getByRole('link', { name: 'Voltar à consulta' }))
    expect(await screen.findByText(/#10 · Serviço/)).toBeTruthy()
  })
  it('trata detalhes inexistentes sem revelar dados de outra empresa', async () => {
    interceptor = url => url === '/lancamentos/10' ? json({}, 404) : undefined
    app('/app/lancamentos/10'); await login()
    expect((await screen.findByRole('alert')).textContent).toContain('não encontrado')
    expect(screen.queryByText(entry.descricao)).toBeNull()
  })
  it('ignora resposta tardia da lista anterior quando filtros mudam', async () => {
    app(); await login(); await screen.findByText(/#10 · Serviço/)
    let resolve!: (r: Response) => void
    let reads = 0
    interceptor = url => url.startsWith('/lancamentos?') && ++reads === 1 ? new Promise(r => { resolve = r }) : undefined
    await userEvent.click(screen.getByRole('button', { name: 'Atualizar lista' }))
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'ABERTO')
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await screen.findByText(/#10 · Serviço/)
    await act(async () => resolve(json(pagina([{ ...entry, descricao: 'Resposta antiga não deve aparecer' }]))))
    expect(screen.queryByText(/Resposta antiga não deve aparecer/)).toBeNull()
  })
})

describe('Seletores de cadastros', () => {
  it('filtra categorias pelo tipo e envia somente os IDs dos nomes selecionados', async () => {
    app('/app/lancamentos/novo?empresaId=8'); await login(); await fill()
    expect(screen.queryByRole('option', { name: 'Serviços contratados' })).toBeNull()
    expect(screen.queryByRole('option', { name: /inativo/ })).toBeNull()
    await userEvent.selectOptions(screen.getByLabelText('Tipo *'), 'DESPESA')
    expect((screen.getByLabelText('Categoria *') as HTMLSelectElement).value).toBe('')
    expect(screen.queryByRole('option', { name: 'Vendas' })).toBeNull()
    for (const [field, name] of [['Categoria *', 'Serviços contratados'], ['Cliente', 'Cliente de teste'],
      ['Fornecedor', 'Fornecedor de teste'], ['Centro de custo', 'Operações']]) {
      await userEvent.selectOptions(screen.getByLabelText(field), screen.getByRole('option', { name }))
    }
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar lançamento' }))
    expect(await screen.findByText('Lançamento cadastrado com sucesso.')).toBeTruthy()
    expect(JSON.parse(String(writes()[0][1]?.body))).toMatchObject({
      tipo: 'DESPESA', categoriaId: 2, clienteId: 4, fornecedorId: 8, centroCustoId: 5,
    })
    const catalogCall = calls().find(c => String(c[0]).endsWith(catalogRoute))
    expect(catalogCall?.[1]).toMatchObject({ method: 'GET', headers: { Authorization: 'Bearer mock-financeiro-only' } })
    expect(calls().filter(c => String(c[0]).includes(catalogRoute))).toHaveLength(1)
  })

  it('mostra carregamento e ausência de opções, com envio bloqueado sem categoria', async () => {
    let resolve!: (result: Response) => void
    interceptor = url => url.endsWith(catalogRoute) ? new Promise(r => { resolve = r }) : undefined
    app('/app/lancamentos/novo'); await login()
    expect(await screen.findByText('Carregando opções de cadastro…')).toBeTruthy()
    const submit = screen.getByRole('button', { name: 'Cadastrar lançamento' }) as HTMLButtonElement
    expect(submit.disabled).toBe(true)
    await act(async () => resolve(json({ ...catalogs, categorias: [], clientes: [], fornecedores: [], centrosCusto: [] })))
    expect(screen.getByRole('option', { name: 'Nenhuma categoria para este tipo' })).toBeTruthy()
    expect(screen.getAllByRole('option', { name: 'Nenhuma opção disponível' })).toHaveLength(3)
    expect(submit.disabled).toBe(true)
    expect(writes()).toHaveLength(0)
  })

  it('permite repetir a consulta após erro sem perder o que foi digitado', async () => {
    interceptor = url => url.endsWith(catalogRoute) ? json({ detalhe: 'erro interno de catálogo' }, 503) : undefined
    app('/app/lancamentos/novo'); await login()
    expect((await screen.findByRole('alert')).textContent).toContain('Não foi possível carregar as opções')
    expect(screen.queryByText(/erro interno de catálogo/)).toBeNull()
    const description = screen.getByLabelText('Descrição *') as HTMLInputElement
    fireEvent.change(description, { target: { value: 'Rascunho do lançamento' } })
    interceptor = () => undefined
    await userEvent.click(screen.getByRole('button', { name: 'Tentar carregar opções novamente' }))
    expect(await screen.findByRole('option', { name: 'Vendas' })).toBeTruthy()
    expect(description.value).toBe('Rascunho do lançamento')
    expect(writes()).toHaveLength(0)
  })

  it('recusa o catálogo de outra empresa sem renderizar suas opções', async () => {
    interceptor = url => url.endsWith(catalogRoute)
      ? json({ ...catalogs, empresaId: 8, categorias: [{ id: 17, nome: 'Nome de outra empresa', tipo: 'RECEITA' }] })
      : undefined
    app('/app/lancamentos/novo'); await login()
    expect((await screen.findByRole('alert')).textContent).toContain('Não foi possível carregar as opções')
    expect(screen.queryByRole('option', { name: 'Nome de outra empresa' })).toBeNull()
    expect((screen.getByRole('button', { name: 'Cadastrar lançamento' }) as HTMLButtonElement).disabled).toBe(true)
    expect(writes()).toHaveLength(0)
  })

  it('encerra a sessão se a consulta de cadastros retornar 401', async () => {
    interceptor = url => url.endsWith(catalogRoute) ? json({}, 401) : undefined
    app('/app/lancamentos/novo'); await login()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByLabelText('Categoria *')).toBeNull()
    expect(writes()).toHaveLength(0)
  })
})

describe('Proteções durante consultas e gravações', () => {
  it('bloqueia envios simultâneos do cadastro', async () => {
    let resolve!: (r: Response) => void
    interceptor = (url, options) => url === '/lancamentos' && options?.method === 'POST' ? new Promise(r => { resolve = r }) : undefined
    app('/app/lancamentos/novo'); await login(); await fill()
    const form = screen.getByRole('form', { name: 'Cadastrar lançamento' })
    fireEvent.submit(form); fireEvent.submit(form)
    await waitFor(() => expect(writes().length).toBe(1))
    expect(screen.getByRole('button', { name: 'Salvando…' }).hasAttribute('disabled')).toBe(true)
    await act(async () => resolve(json({ ...entry, tipo: 'RECEITA', descricao: 'Receita teste', valor: 1234.56 }, 201)))
    await screen.findByText('Lançamento cadastrado com sucesso.')
  })
  it.each([
    [409, 'Os dados financeiros deste lançamento apresentam uma inconsistência e precisam de verificação.'],
    [500, 'O sistema está temporariamente indisponível.'],
  ])('exibe a mensagem de saldo para HTTP %s e permite nova consulta', async (status, message) => {
    interceptor = url => url.endsWith('/saldo') ? json({ mensagem: 'detalhe interno da API' }, status) : undefined
    app(); await login(); await screen.findByText(/#10 · Serviço/)
    await userEvent.click(screen.getByRole('button', { name: 'Consultar valores de #10' }))
    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain(message)
    expect(alert.textContent).not.toMatch(/edição|cancelamento|detalhe interno da API/)
    expect(screen.queryByText('Saldo restante')).toBeNull()
    expect(screen.getByText(/#10 · Serviço/)).toBeTruthy()
    interceptor = () => undefined
    await userEvent.click(screen.getByRole('button', { name: 'Tentar valores de #10 novamente' }))
    expect(await screen.findByText('Saldo restante')).toBeTruthy()
  })
  it('não expõe os dados de uma lista recebida de outra empresa', async () => {
    listItems = [{ ...entry, empresaId: 8, descricao: 'Identificação privada de outra empresa' }]
    app(); await login()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByText(/Identificação privada/)).toBeNull()
  })
  it('recusa IDs de detalhe inválidos antes da consulta', async () => {
    app('/app/lancamentos/9007199254740992'); await login()
    expect((await screen.findByRole('alert')).textContent).toContain('Código de lançamento inválido')
    expect(calls().some(c => String(c[0]).startsWith('/lancamentos'))).toBe(false)
  })
  it('preserva a edição quando o servidor informa conflito de baixa', async () => {
    interceptor = (url, options) => url === '/lancamentos/10' && options?.method === 'PUT' ? json({}, 409) : undefined
    app('/app/lancamentos/10/editar'); await login(); await screen.findByRole('option', { name: 'Serviços contratados' })
    fireEvent.change(screen.getByLabelText('Descrição *'), { target: { value: 'Preservar alteração' } })
    await userEvent.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect((await screen.findByRole('alert')).textContent).toContain('A situação atual impede a edição.')
    expect((screen.getByLabelText('Descrição *') as HTMLInputElement).value).toBe('Preservar alteração')
    expect(screen.queryByText('Lançamento atualizado com sucesso.')).toBeNull()
  })
  it('preserva os dados digitados quando a sessão é revalidada ao focar a janela', async () => {
    app('/app/lancamentos/novo'); await login(); await fill()
    fireEvent.focus(window)
    await waitFor(() => expect(calls().filter(c => String(c[0]).endsWith('/usuarios/me')).length).toBe(2))
    expect((screen.getByLabelText('Descrição *') as HTMLInputElement).value).toBe('Receita teste')
    expect((screen.getByLabelText('Valor original (R$) *') as HTMLInputElement).value).toBe('1234,56')
    expect(writes().length).toBe(0)
  })
})
describe('Contratos e isolamento de lançamentos', () => {
  it.each([null, {}, { ...pagina(), itens: [{ ...entry, empresaId: 8 }] }, { ...pagina(), itens: [{ ...entry, valor: '250' }] }, { ...pagina(), totalPaginas: 99 }])('recusa contrato de lista inesperado ou de outra empresa', async value => {
    await expect(entriesApi(vi.fn().mockResolvedValue(value), 7).list(emptyFilters, 1)).rejects.toMatchObject({ code: 'unexpected' })
  })
  it('recusa detalhe e saldo que não pertencem à empresa ou ao lançamento', async () => {
    await expect(entriesApi(vi.fn().mockResolvedValue({ ...entry, empresaId: 8 }), 7).get(10)).rejects.toMatchObject({ code: 'unexpected' })
    await expect(entriesApi(vi.fn().mockResolvedValue({ ...balance, lancamentoId: 11 }), 7).balance(10)).rejects.toMatchObject({ code: 'unexpected' })
    await expect(entriesApi(vi.fn().mockResolvedValue({ ...balance, empresaId: 8 }), 7).balance(10)).rejects.toMatchObject({ code: 'unexpected' })
  })
  it('recusa mutações com retorno de outra empresa', async () => {
    await expect(entriesApi(vi.fn().mockResolvedValue({ ...entry, empresaId: 8 }), 7).create(entryPayload(draftEntry(entry)))).rejects.toMatchObject({ code: 'unexpected' })
  })
  it('valida centavos, IDs, datas e limite de observações sem inventar regras financeiras', () => {
    const valid = draftEntry(entry)
    expect(validateEntry(valid)).toEqual({})
    expect(entryPayload({ ...valid, valor: '0,01' }).valor).toBe(.01)
    for (const valor of ['0', '-1', '1,234', '1e3', '1.234,56', '10000000000000']) expect(validateEntry({ ...valid, valor })).toHaveProperty('valor')
    expect(validateEntry({ ...valid, categoriaId: '9007199254740992' })).toHaveProperty('categoriaId')
    expect(validateEntry({ ...valid, dataEmissao: '2026-02-30' })).toHaveProperty('dataEmissao')
    expect(validateEntry({ ...valid, observacoes: 'á'.repeat(32768) })).toHaveProperty('observacoes')
  })
})
