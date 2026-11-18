import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { perfis } from '../types/auth'
import { Icon } from '../components/Icon'

export function HomePage() {
  const { usuario, refreshSession } = useAuth()
  const [checking, setChecking] = useState(false)
  const [updated, setUpdated] = useState(false)
  if (!usuario) return null
  async function refresh() {
    setChecking(true)
    await refreshSession()
    setChecking(false)
    setUpdated(true)
  }
  return <>
    <div className="page-heading"><div><p className="eyebrow">SEU AMBIENTE DE GESTÃO</p><h1>Olá, {usuario.nome.split(' ')[0]}.</h1><p>Bem-vindo ao espaço financeiro da sua empresa.</p></div><span className="session-badge"><Icon name="lock" /> Sessão autenticada</span></div>
    <section className="welcome-panel" aria-labelledby="welcome-title"><div><span className="eyebrow">TUDO COMEÇA COM ORGANIZAÇÃO</span><h2 id="welcome-title">Sua gestão, em um só lugar.</h2><p>Seu acesso está configurado. Esta é a base para acompanhar os próximos passos da gestão financeira da sua empresa.</p><div className="welcome-meta"><Icon name="company" /><span>Ambiente exclusivo da <strong>empresa #{usuario.empresaId}</strong></span></div></div><div className="welcome-art" aria-hidden="true"><div className="welcome-art-card"><Icon name="entries" /><span /><span /><span /></div><span className="welcome-art-lock"><Icon name="lock" /></span></div></section>
    <section className="identity-panel" aria-labelledby="identity-title"><div className="section-heading"><div><h2 id="identity-title">Seu acesso</h2><p>Identificação confirmada pela plataforma.</p></div><button type="button" className="button button-secondary" onClick={() => void refresh()} disabled={checking}>{checking ? 'Verificando…' : 'Verificar acesso'}</button></div><dl className="identity-grid"><div><dt>USUÁRIO</dt><dd>{usuario.nome}</dd></div><div><dt>E-MAIL</dt><dd>{usuario.email}</dd></div><div><dt>EMPRESA ATIVA</dt><dd>Empresa #{usuario.empresaId}</dd></div><div><dt>PERFIL DE ACESSO</dt><dd><span className="profile-badge">{perfis[usuario.perfil]}</span></dd></div></dl><p className="identity-note" role="status">{updated ? 'Identificação atualizada. Seu acesso continua autorizado.' : 'As permissões são definidas pelo administrador da sua empresa.'}</p></section>
    <section className="next-section" aria-labelledby="next-title"><div className="section-heading"><div><h2 id="next-title">Ferramentas da gestão</h2></div></div><div className="future-grid">
      <article className="future-card"><span className="feature-icon"><Icon name="entries" /></span><h3>Rotina financeira</h3><p>Lançamentos, contas a pagar e a receber, organizados em um único ambiente.</p><span className="future-caption">Disponível em uma próxima etapa</span></article>
      <article className="future-card"><span className="feature-icon"><Icon name="flow" /></span><h3>Visão do caixa</h3><p>Entradas, saídas e resultados previstos e realizados.</p><Link className="future-caption" to="/app/fluxo-caixa">Abrir fluxo de caixa</Link></article>
      <article className="future-card"><span className="feature-icon"><Icon name="report" /></span><h3>Informação para decidir</h3><p>Dashboard e relatórios para dar mais clareza à sua gestão.</p><span className="future-caption">Disponível em uma próxima etapa</span></article>
    </div></section>
  </>
}
