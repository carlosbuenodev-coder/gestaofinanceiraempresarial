import type { ReactNode } from 'react'

export function Alert({ children }: { children: ReactNode }) {
  return <div className="alert" role="alert"><span aria-hidden="true">!</span><p>{children}</p></div>
}
