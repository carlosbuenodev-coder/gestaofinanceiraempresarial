import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { AppLayout } from '../components/AppLayout'
import { LoginPage } from '../pages/LoginPage'
import { HomePage } from '../pages/HomePage'
import { DashboardPage } from '../pages/DashboardPage'
import { CashflowPage } from '../pages/CashflowPage'
import { EntriesPage } from '../pages/EntriesPage'
import { EntryPage } from '../pages/EntryPage'
import { StatusPage } from '../pages/StatusPage'

function ProtectedRoute() {
  const { status, notice } = useAuth()
  if (status === 'authenticating') return <main className="session-loading" role="status"><span className="spinner" />Verificando seu acesso…</main>
  if (status !== 'authenticated') return <Navigate to={notice?.code === 'forbidden' ? '/nao-autorizado' : '/login'} replace />
  return <Outlet />
}
export function AppRoutes() {
  return <Routes>
    <Route path="/" element={<Navigate to="/app" replace />} />
    <Route path="/login" element={<LoginPage />} />
    <Route element={<ProtectedRoute />}>
      <Route path="/app" element={<AppLayout />}>
        <Route index element={<HomePage />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="fluxo-caixa" element={<CashflowPage />} />
        <Route path="lancamentos" element={<EntriesPage key="all" />} />
        <Route path="contas-a-pagar" element={<EntriesPage key="payable" fixedType="DESPESA" />} />
        <Route path="contas-a-receber" element={<EntriesPage key="receivable" fixedType="RECEITA" />} />
        <Route path="lancamentos/novo" element={<EntryPage key="create" create />} />
        <Route path="lancamentos/:id" element={<EntryPage key="detail" />} />
        <Route path="lancamentos/:id/editar" element={<EntryPage key="edit" edit />} />
      </Route>
    </Route>
    <Route path="/nao-autorizado" element={<StatusPage forbidden />} />
    <Route path="/nao-encontrado" element={<StatusPage />} />
    <Route path="*" element={<Navigate to="/nao-encontrado" replace />} />
  </Routes>
}
