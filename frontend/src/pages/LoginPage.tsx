import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { ApiError } from '../services/api'
import { Alert } from '../components/Alert'
import { Brand } from '../components/Brand'
import { Icon } from '../components/Icon'

export function LoginPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const [empresa, setEmpresa] = useState('')
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const busy = auth.status === 'authenticating'
  if (auth.status === 'authenticated') return <Navigate to="/app" replace />
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    const invalid: Record<string, string> = {}
    const id = Number(empresa)
    if (!/^\d+$/.test(empresa.trim()) || !Number.isSafeInteger(id) || id <= 0) invalid.empresa = 'Informe um código de empresa válido.'
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) invalid.email = 'Informe um e-mail válido.'
    if (!senha) invalid.senha = 'Informe sua senha.'
    setErrors(invalid)
    if (Object.keys(invalid).length) {
      document.getElementById(Object.keys(invalid)[0])?.focus()
      return
    }
    try {
      await auth.login({ empresaId: id, email: email.trim(), senha })
      setSenha('')
      navigate('/app', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && ['credentials', 'expired', 'forbidden'].includes(error.code)) setSenha('')
    }
  }
  return <div className="login-layout">
    <aside className="login-story" aria-label="Gestão Financeira Empresarial">
      <Brand light />
      <div className="story-content">
        <span className="eyebrow story-eyebrow">ORGANIZAÇÃO QUE MOVE SUA EMPRESA</span>
        <h1>Clareza para decidir.<br /><span>Controle para crescer.</span></h1>
        <p>Um espaço para organizar a gestão financeira e acompanhar o que importa para o seu negócio.</p>
        <div className="document-art" aria-hidden="true">
          <div className="art-sheet art-sheet-back" />
          <div className="art-sheet art-sheet-middle" />
          <div className="art-sheet art-sheet-front">
            <span className="art-symbol"><Icon name="entries" /></span>
            <span className="art-line art-line-title" /><span className="art-line" /><span className="art-line art-line-short" />
            <div className="art-divider" /><div className="art-row"><i /><span /><b /></div><div className="art-row"><i /><span /><b /></div><div className="art-row"><i /><span /><b /></div>
          </div>
          <div className="art-lock"><Icon name="lock" /></div>
          <span className="art-orbit orbit-one" /><span className="art-orbit orbit-two" />
        </div>
      </div>
      <div className="story-footer"><span className="small-rule" /> Gestão com confiança, em cada etapa.</div>
    </aside>
    <main className="login-main" id="conteudo">
      <div className="login-top"><span>PORTAL EMPRESARIAL</span><span className="version">Acesso à plataforma</span></div>
      <div className="login-form-container">
        <div className="mobile-brand"><Brand /></div>
        <span className="eyebrow">BEM-VINDO DE VOLTA</span>
        <h2>Acesse sua conta</h2>
        <p className="login-description">Informe seus dados para entrar no ambiente da sua empresa.</p>
        {auth.notice && <Alert>{auth.notice.message}</Alert>}
        <form onSubmit={submit} noValidate aria-label="Acesso empresarial" aria-busy={busy}>
          <div className="field"><label htmlFor="empresa">Código da empresa</label>
            <input id="empresa" name="empresa" type="text" inputMode="numeric" autoComplete="off" placeholder="Informe o código" value={empresa} onChange={e => setEmpresa(e.target.value)} disabled={busy} aria-invalid={!!errors.empresa} aria-describedby={errors.empresa ? 'empresa-error' : 'empresa-help'} required />
            {errors.empresa ? <span id="empresa-error" className="field-error">{errors.empresa}</span> : <span id="empresa-help" className="field-help">Utilize o código fornecido pelo administrador.</span>}
          </div>
          <div className="field"><label htmlFor="email">E-mail</label>
            <input id="email" name="email" type="email" autoComplete="username" autoCapitalize="none" spellCheck={false} placeholder="voce@empresa.com.br" value={email} onChange={e => setEmail(e.target.value)} disabled={busy} aria-invalid={!!errors.email} aria-describedby={errors.email ? 'email-error' : undefined} required />
            {errors.email && <span id="email-error" className="field-error">{errors.email}</span>}
          </div>
          <div className="field"><label htmlFor="senha">Senha</label><div className="password-field">
            <input id="senha" name="senha" type={showPassword ? 'text' : 'password'} autoComplete="current-password" placeholder="Digite sua senha" value={senha} onChange={e => setSenha(e.target.value)} disabled={busy} aria-invalid={!!errors.senha} aria-describedby={errors.senha ? 'senha-error' : undefined} required />
            <button className="password-toggle" type="button" onClick={() => setShowPassword(v => !v)} aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} aria-pressed={showPassword} disabled={busy}>{showPassword ? 'Ocultar' : 'Mostrar'}</button>
          </div>{errors.senha && <span id="senha-error" className="field-error">{errors.senha}</span>}</div>
          <button className="button button-primary login-submit" type="submit" disabled={busy}>{busy ? <><span className="spinner" aria-hidden="true" /> Verificando acesso…</> : <>Entrar <Icon name="arrow" /></>}</button>
          <div className="form-note"><Icon name="lock" /><p>Seu acesso respeita as permissões definidas pela sua empresa.</p></div>
        </form>
        <p className="login-help">Precisa de acesso? <strong>Fale com o administrador da sua empresa.</strong></p>
      </div>
      <footer className="login-footer"><span>Gestão Financeira Empresarial</span><span>Acesso exclusivo a usuários autorizados</span></footer>
    </main>
  </div>
}
