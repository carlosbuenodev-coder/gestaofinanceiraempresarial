import type { ReactNode } from 'react'
export type IconName = 'home' | 'chart' | 'entries' | 'out' | 'in' | 'payments' | 'flow' | 'report' | 'settings' | 'lock' | 'arrow' | 'menu' | 'close' | 'logout' | 'company'
const paths: Record<IconName, ReactNode> = {
  home: <><path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z" /></>,
  chart: <><path d="M4 4v16h17M8 15v-4m5 4V7m5 8v-5" /></>,
  entries: <><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 8h8M8 12h8M8 16h5" /></>,
  out: <><path d="M5 19 19 5M9 5h10v10M4 8v12h12" /></>,
  in: <><path d="M19 5 5 19M15 19H5V9M20 16V4H8" /></>,
  payments: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="M3 10h18M7 15h3" /></>,
  flow: <><path d="M3 7h17l-4-4M21 17H4l4 4M20 7l-4 4M4 17l4-4" /></>,
  report: <><path d="M14 3H5v18h14V8Zm0 0v5h5M8 12h8M8 16h6" /></>,
  settings: <><circle cx="12" cy="12" r="3" /><path d="m9 3-1 3-3 1-2 3 2 2-1 4 3 2h3l2 3 3-2 3-1 1-3 2-3-2-3-3-1-1-3Z" /></>,
  lock: <><rect x="5" y="10" width="14" height="11" rx="2" /><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" /></>,
  arrow: <><path d="M4 12h16m-6-6 6 6-6 6" /></>,
  menu: <><path d="M4 6h16M4 12h16M4 18h16" /></>,
  close: <><path d="m6 6 12 12M6 18 18 6" /></>,
  logout: <><path d="M9 4H4v16h5m5-12 4 4-4 4M8 12h12" /></>,
  company: <><path d="M4 21V4h11v17M15 10h5v11M8 8h3M8 12h3M8 16h3M2 21h20" /></>,
}
export function Icon({ name }: { name: IconName }) {
  return <svg className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
}
