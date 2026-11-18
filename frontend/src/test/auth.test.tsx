import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthProvider'
import { AppRoutes } from '../routes/AppRoutes'
import { useAuth } from '../hooks/useAuth'

const usuario = { id: 12, empresaId: 7, nome: 'Ana Fictícia', email: 'ana@teste.invalid', perfil: 'FINANCEIRO', ativo: true }
const token = 'token-ficticio-apenas-para-mocks'
const loginResponse = () => ({ token, tipo: 'Bearer', expiraEmUtc: new Date(Date.now() + 3_600_000).toISOString(), usuario })
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
function renderApp(path = '/login') {
  return render(<MemoryRouter initialEntries={[path]}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter>)
}
async function fillAndSubmit(user = userEvent.setup()) {
  await user.type(screen.getByLabelText('Código da empresa'), '7')
  await user.type(screen.getByLabelText('E-mail'), usuario.email)
  await user.type(screen.getByLabelText('Senha', { exact: true }), 'senha-ficticia')
  await user.click(screen.getByRole('button', { name: /^Entrar/ }))
}
function success() {
  vi.mocked(fetch).mockResolvedValueOnce(json(loginResponse())).mockResolvedValueOnce(json(usuario))
}
beforeEach(() => { vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Network error'))) })

describe('Acesso empresarial', () => {
  it('renderiza login sem consultar ou listar empresas', () => {
    renderApp()
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(screen.getByLabelText('Código da empresa')).toBeTruthy()
    expect(screen.getByLabelText('E-mail')).toBeTruthy()
    expect(screen.getByLabelText('Senha', { exact: true }).getAttribute('type')).toBe('password')
    expect(fetch).not.toHaveBeenCalled()
  })
  it('valida campos obrigatórios e foca o primeiro inválido', async () => {
    renderApp()
    await userEvent.click(screen.getByRole('button', { name: /^Entrar/ }))
    expect(screen.getByText('Informe um código de empresa válido.')).toBeTruthy()
    expect(screen.getByText('Informe um e-mail válido.')).toBeTruthy()
    expect(screen.getByText('Informe sua senha.')).toBeTruthy()
    expect(document.activeElement).toBe(screen.getByLabelText('Código da empresa'))
    expect(fetch).not.toHaveBeenCalled()
  })
  it.each(['0', '-1', '1e3', '9007199254740992'])('rejeita código de empresa inválido %s', async value => {
    renderApp()
    await userEvent.type(screen.getByLabelText('Código da empresa'), value)
    await userEvent.click(screen.getByRole('button', { name: /^Entrar/ }))
    expect(screen.getByText('Informe um código de empresa válido.')).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('permite mostrar e ocultar senha sem enviar o formulário', async () => {
    renderApp()
    await userEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }))
    expect(screen.getByLabelText('Senha', { exact: true }).getAttribute('type')).toBe('text')
    await userEvent.click(screen.getByRole('button', { name: 'Ocultar senha' }))
    expect(screen.getByLabelText('Senha', { exact: true }).getAttribute('type')).toBe('password')
    expect(fetch).not.toHaveBeenCalled()
  })
  it('exibe erro genérico para credenciais inválidas e limpa somente a senha', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json({ detalhe: 'dado privado' }, 401))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.getByText(/Confira a empresa, o e-mail e a senha/)).toBeTruthy()
    expect((screen.getByLabelText('Código da empresa') as HTMLInputElement).value).toBe('7')
    expect((screen.getByLabelText('E-mail') as HTMLInputElement).value).toBe(usuario.email)
    expect((screen.getByLabelText('Senha', { exact: true }) as HTMLInputElement).value).toBe('')
    expect(screen.queryByText('dado privado')).toBeNull()
    expect(fetch).toHaveBeenCalledTimes(1)
  })
  it('faz login com o contrato real e confirma identidade via GET /usuarios/me', async () => {
    success(); renderApp(); await fillAndSubmit()
    expect(await screen.findByRole('heading', { name: 'Olá, Ana.' })).toBeTruthy()
    const calls = vi.mocked(fetch).mock.calls
    expect(calls[0][0]).toBe('http://localhost:5216/auth/usuarios/login')
    expect(JSON.parse(calls[0][1]?.body as string)).toEqual({ empresaId: 7, email: usuario.email, senha: 'senha-ficticia' })
    expect(calls[1][0]).toBe('http://localhost:5216/usuarios/me')
    expect(calls[1][1]?.headers).toMatchObject({ Authorization: `Bearer ${token}` })
    expect(calls[1][1]?.method).toBe('GET')
    expect(screen.getByText(usuario.email)).toBeTruthy()
    expect(screen.getAllByText('Financeiro').length).toBeGreaterThan(0)
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it('não libera dados enquanto /usuarios/me está pendente', async () => {
    let resolve!: (response: Response) => void
    vi.mocked(fetch).mockResolvedValueOnce(json(loginResponse())).mockImplementationOnce(() => new Promise(r => { resolve = r }))
    renderApp(); await fillAndSubmit()
    expect(screen.getByRole('button', { name: /Verificando acesso/ }).hasAttribute('disabled')).toBe(true)
    expect(screen.queryByRole('heading', { name: 'Olá, Ana.' })).toBeNull()
    await act(async () => { resolve(json(usuario)) })
    expect(await screen.findByRole('heading', { name: 'Olá, Ana.' })).toBeTruthy()
  })
  it('não aceita resposta de identidade pertencente a outra empresa', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json(loginResponse())).mockResolvedValueOnce(json({ ...usuario, empresaId: 8 }))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Olá, Ana.' })).toBeNull()
  })
  it('preserva os campos em falha de conexão recuperável', async () => {
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Não foi possível conectar ao sistema/)).toBeTruthy()
    expect((screen.getByLabelText('Senha', { exact: true }) as HTMLInputElement).value).toBe('senha-ficticia')
    expect((screen.getByLabelText('E-mail') as HTMLInputElement).value).toBe(usuario.email)
  })
  it('oculta detalhes internos de erros 500', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json({ stack: 'MySQL detalhe interno' }, 500))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/temporariamente indisponível/)).toBeTruthy()
    expect(screen.queryByText(/MySQL/)).toBeNull()
  })
  it.each([null, {}, { ...loginResponse(), tipo: 'Outro' }, { ...loginResponse(), expiraEmUtc: 'inválido' }])('trata contrato inesperado de login sem liberar acesso', async value => {
    vi.mocked(fetch).mockResolvedValueOnce(json(value))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Não foi possível confirmar a resposta/)).toBeTruthy()
    expect(fetch).toHaveBeenCalledTimes(1)
  })
  it('trata HTML em lugar de JSON sem expor o corpo', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response('<html>stack privado</html>', { headers: { 'Content-Type': 'text/html' } }))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Não foi possível confirmar a resposta/)).toBeTruthy()
    expect(screen.queryByText('stack privado')).toBeNull()
  })
  it('recusa login já expirado sem consultar /me', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json({ ...loginResponse(), expiraEmUtc: new Date(Date.now() - 60000).toISOString() }))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(fetch).toHaveBeenCalledTimes(1)
  })
  it('trata JWT recusado por /usuarios/me', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json(loginResponse())).mockResolvedValueOnce(json({}, 401))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByText('Seu acesso')).toBeNull()
  })
  it('não aceita usuário inativo ou perfil geral no contrato empresarial', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(json(loginResponse())).mockResolvedValueOnce(json({ ...usuario, ativo: false, perfil: 'ADMIN_GERAL' }))
    renderApp(); await fillAndSubmit()
    expect(await screen.findByText(/Não foi possível confirmar a resposta/)).toBeTruthy()
    expect(screen.queryByText('Seu acesso')).toBeNull()
  })
})

describe('Rotas e ciclo de sessão', () => {
  it('protege /app sem expor conteúdo ou consultar a API sem token', () => {
    renderApp('/app')
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(screen.queryByText('Seu acesso')).toBeNull()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('exibe rota não encontrada', () => {
    renderApp('/rota-inexistente')
    expect(screen.getByRole('heading', { name: 'Este caminho não está disponível.' })).toBeTruthy()
  })
  it('exibe rota não autorizada e permite voltar ao login', async () => {
    renderApp('/nao-autorizado')
    expect(screen.getByRole('heading', { name: 'Não foi possível liberar o acesso.' })).toBeTruthy()
    await userEvent.click(screen.getByRole('link', { name: /Voltar para o login/ }))
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
  })
  it('faz logout e bloqueia o retorno à área autenticada', async () => {
    success(); const view = renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    await userEvent.click(screen.getByRole('button', { name: 'Sair da conta' }))
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(screen.queryByText(usuario.email)).toBeNull()
    view.unmount(); renderApp('/app')
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(fetch).toHaveBeenCalledTimes(2)
  })
  it('não restaura sessão ao recarregar e não persiste token', async () => {
    success(); const view = renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    view.unmount(); renderApp('/app')
    expect(screen.getByRole('heading', { name: 'Acesse sua conta' })).toBeTruthy()
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  })
  it('retira dados protegidos e solicita login quando uma consulta retorna 401', async () => {
    success(); renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    vi.mocked(fetch).mockResolvedValueOnce(json({}, 401))
    await userEvent.click(screen.getByRole('button', { name: 'Verificar acesso' }))
    expect(await screen.findByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByText(usuario.email)).toBeNull()
  })
  it('redireciona para acesso negado quando a API revoga o acesso', async () => {
    success(); renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    vi.mocked(fetch).mockResolvedValueOnce(json({}, 403))
    await userEvent.click(screen.getByRole('button', { name: 'Verificar acesso' }))
    expect(await screen.findByRole('heading', { name: 'Não foi possível liberar o acesso.' })).toBeTruthy()
    expect(screen.queryByText(usuario.email)).toBeNull()
  })
  it('atualiza identificação ao retornar à janela', async () => {
    success(); renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    vi.mocked(fetch).mockResolvedValueOnce(json({ ...usuario, nome: 'Ana Atualizada' }))
    fireEvent.focus(window)
    expect((await screen.findAllByText('Ana Atualizada')).length).toBe(2)
    expect(fetch).toHaveBeenCalledTimes(3)
  })
  it('expira automaticamente sem precisar de nova requisição', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'] })
    vi.mocked(fetch).mockResolvedValueOnce(json({ ...loginResponse(), expiraEmUtc: new Date(Date.now() + 3000).toISOString() })).mockResolvedValueOnce(json(usuario))
    renderApp()
    fireEvent.change(screen.getByLabelText('Código da empresa'), { target: { value: '7' } })
    fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: usuario.email } })
    fireEvent.change(screen.getByLabelText('Senha', { exact: true }), { target: { value: 'ficticia' } })
    await act(async () => { fireEvent.submit(screen.getByRole('form', { name: 'Acesso empresarial' })) })
    expect(screen.getByRole('heading', { name: 'Olá, Ana.' })).toBeTruthy()
    await act(async () => { vi.advanceTimersByTime(3001) })
    expect(screen.getByText(/Sua sessão expirou/)).toBeTruthy()
    expect(screen.queryByText(usuario.email)).toBeNull()
    expect(fetch).toHaveBeenCalledTimes(2)
  })
  it('mantém os três módulos futuros desabilitados e libera o fluxo de caixa', async () => {
    success(); renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    const buttons = screen.getAllByTitle('Em desenvolvimento')
    expect(buttons.length).toBe(3)
    expect(buttons.every(b => b.hasAttribute('disabled'))).toBe(true)
    expect(screen.getByRole('link', { name: 'Fluxo de caixa' }).getAttribute('href')).toBe('/app/fluxo-caixa')
  })
  it('abre o menu móvel e fecha com Escape devolvendo foco', async () => {
    success(); renderApp(); await fillAndSubmit()
    await screen.findByRole('heading', { name: 'Olá, Ana.' })
    const trigger = screen.getByRole('button', { name: 'Abrir menu' })
    await userEvent.click(trigger)
    expect(screen.getByRole('dialog', { name: 'Navegação principal' })).toBeTruthy()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })
  it('ignora resposta tardia de login após logout', async () => {
    let resolve!: (response: Response) => void
    vi.mocked(fetch).mockImplementationOnce(() => new Promise(r => { resolve = r })).mockResolvedValueOnce(json(usuario))
    function Harness() {
      const auth = useAuth()
      return <><button onClick={() => { void auth.login({ empresaId: 7, email: usuario.email, senha: 'ficticia' }) }}>Login teste</button><button onClick={auth.logout}>Logout teste</button><span>{auth.status}</span></>
    }
    render(<AuthProvider><Harness /></AuthProvider>)
    await userEvent.click(screen.getByRole('button', { name: 'Login teste' }))
    await userEvent.click(screen.getByRole('button', { name: 'Logout teste' }))
    await act(async () => { resolve(json(loginResponse())) })
    await waitFor(() => expect(screen.getByText('anonymous')).toBeTruthy())
    expect(screen.queryByText('authenticated')).toBeNull()
  })
})
