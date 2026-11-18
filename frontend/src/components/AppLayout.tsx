import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { Brand } from './Brand'
import { Icon } from './Icon'
import type { IconName } from './Icon'
import { perfis } from '../types/auth'

const future: { name: string; icon: IconName }[] = [
  { name: 'Baixas', icon: 'payments' },
  { name: 'Relatórios', icon: 'report' }, { name: 'Configurações', icon: 'settings' },
]
export function AppLayout() {
  const location = useLocation()
  const { usuario, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const toggle = useRef<HTMLButtonElement>(null)
  const menu = useRef<HTMLElement>(null)
  useEffect(() => {
    if (!menuOpen) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    menu.current?.querySelector<HTMLButtonElement>('button')?.focus()
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') { setMenuOpen(false); toggle.current?.focus() }
      if (event.key === 'Tab') {
        const items = Array.from(menu.current?.querySelectorAll<HTMLElement>('button:not(:disabled), a[href]') ?? [])
        const first = items[0], last = items.at(-1)
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
      }
    }
    window.addEventListener('keydown', onKey)
    return () => { window.removeEventListener('keydown', onKey); document.body.style.overflow = previousOverflow }
  }, [menuOpen])
  if (!usuario) return null
  const initials = usuario.nome.split(/\s+/).filter(Boolean).slice(0, 2).map(n => n[0]).join('').toUpperCase()
  return <div className="app-shell">
    <a href="#conteudo" className="skip-link">Pular para o conteúdo</a>
    {menuOpen && <button className="menu-overlay" tabIndex={-1} aria-label="Fechar navegação" onClick={() => { setMenuOpen(false); toggle.current?.focus() }} />}
    <aside id="navegacao" className={`sidebar${menuOpen ? ' sidebar-open' : ''}`} ref={menu} aria-label="Navegação principal" role={menuOpen ? 'dialog' : undefined} aria-modal={menuOpen || undefined}>
      <div className="sidebar-brand"><Brand light /><button className="icon-button mobile-menu-close" aria-label="Fechar menu" onClick={() => { setMenuOpen(false); toggle.current?.focus() }}><Icon name="close" /></button></div>
      <div className="sidebar-company"><span className="company-icon"><Icon name="company" /></span><span><small>EMPRESA ATIVA</small><strong>Empresa #{usuario.empresaId}</strong></span></div>
      <nav><p className="nav-label">ÁREA DE TRABALHO</p>
        <NavLink to="/app" end className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="home" />Início</NavLink>
        <NavLink to="/app/dashboard" className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="chart" />Dashboard</NavLink>
        <NavLink to="/app/lancamentos" className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="entries" />Lançamentos</NavLink>
        <NavLink to="/app/contas-a-pagar" className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="out" />Contas a pagar</NavLink>
        <NavLink to="/app/contas-a-receber" className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="in" />Contas a receber</NavLink>
        <NavLink to="/app/fluxo-caixa" className="nav-item" onClick={() => setMenuOpen(false)}><Icon name="flow" />Fluxo de caixa</NavLink>
        <p className="nav-label future-label">GESTÃO FINANCEIRA <span>EM DESENVOLVIMENTO</span></p>
        {future.map(item => <button key={item.name} className="nav-item nav-future" disabled title="Em desenvolvimento"><Icon name={item.icon} /><span>{item.name}</span><span className="nav-dot" aria-hidden="true" /></button>)}
      </nav>
      <div className="sidebar-bottom"><span className="avatar">{initials}</span><span className="sidebar-user"><strong>{usuario.nome}</strong><small>{perfis[usuario.perfil]}</small></span><button className="icon-button" title="Sair da conta" aria-label="Sair da conta" onClick={logout}><Icon name="logout" /></button></div>
    </aside>
    <div className="app-body"><header className="app-header"><div className="header-location"><button ref={toggle} className="icon-button mobile-menu-toggle" aria-label="Abrir menu" aria-expanded={menuOpen} aria-controls="navegacao" onClick={() => setMenuOpen(true)}><Icon name="menu" /></button><span>Área de trabalho</span><span className="breadcrumb-slash">/</span><strong>{location.pathname.startsWith('/app/lancamentos') ? 'Lançamentos' : location.pathname === '/app/contas-a-pagar' ? 'Contas a pagar' : location.pathname === '/app/contas-a-receber' ? 'Contas a receber' : location.pathname === '/app/dashboard' ? 'Dashboard' : location.pathname === '/app/fluxo-caixa' ? 'Fluxo de caixa' : 'Início'}</strong></div><div className="header-account"><span className="header-company"><Icon name="company" /> Empresa #{usuario.empresaId}</span><span className="avatar avatar-small">{initials}</span></div></header>
      <main id="conteudo" className="app-content" tabIndex={-1}><Outlet /></main>
      <footer className="app-footer"><span>Gestão Financeira Empresarial</span><span>Ambiente da empresa #{usuario.empresaId}</span></footer>
    </div>
  </div>
}
