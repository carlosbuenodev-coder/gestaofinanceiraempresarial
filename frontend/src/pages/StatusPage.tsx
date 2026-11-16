import { Link } from 'react-router-dom'
import { Brand } from '../components/Brand'
import { Icon } from '../components/Icon'
import { useAuth } from '../hooks/useAuth'

export function StatusPage({ forbidden = false }: { forbidden?: boolean }) {
  const { usuario, notice, logout } = useAuth()
  return <main className="status-page"><Brand /><section><span className="status-symbol"><Icon name={forbidden ? 'lock' : 'entries'} /></span><span className="eyebrow">{forbidden ? 'ACESSO RESTRITO' : 'PÁGINA NÃO ENCONTRADA'}</span><h1>{forbidden ? 'Não foi possível liberar o acesso.' : 'Este caminho não está disponível.'}</h1><p>{forbidden ? (notice?.code === 'forbidden' ? notice.message : 'Verifique suas permissões com o administrador da empresa.') : 'A página pode ter mudado de endereço ou ainda não estar disponível.'}</p><Link className="button button-primary" to={usuario ? '/app' : '/login'} onClick={usuario ? undefined : logout}>{usuario ? 'Voltar ao início' : 'Voltar para o login'}<Icon name="arrow" /></Link></section><footer>Gestão Financeira Empresarial</footer></main>
}
