import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { AppLayout } from '../components/AppLayout'
import { LoginPage } from '../pages/LoginPage'
import { HomePage } from '../pages/HomePage'
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
    <Route element={<ProtectedRoute />}><Route path="/app" element={<AppLayout />}><Route index element={<HomePage />} /></Route></Route>
    <Route path="/nao-autorizado" element={<StatusPage forbidden />} />
    <Route path="/nao-encontrado" element={<StatusPage />} />
    <Route path="*" element={<Navigate to="/nao-encontrado" replace />} />
  </Routes>
}
