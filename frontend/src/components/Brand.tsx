export function Brand({ light = false }: { light?: boolean }) {
  return <div className={`brand${light ? ' brand-light' : ''}`}>
    <span className="brand-mark" aria-hidden="true"><svg viewBox="0 0 32 32" fill="none"><path d="M7 23V15M16 23V8M25 23V12" stroke="currentColor" strokeWidth="4" strokeLinecap="round" /></svg></span>
    <span><strong>Gestão Financeira</strong><small>EMPRESARIAL</small></span>
  </div>
}
