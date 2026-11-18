import type { EntryFilters as Filters } from '../types/entries'
import { statuses } from '../types/entries'
export function EntryFilters({ draft, setDraft, apply, clear, fixedType, error }: { draft: Filters; setDraft: (value: Filters) => void; apply: (event: React.SubmitEvent<HTMLFormElement>) => void; clear: () => void; fixedType?: string; error: string }) {
  const field = (key: keyof Filters, label: string, type = 'text') => <label key={key}>{label}<input type={type} value={draft[key]} onChange={e => setDraft({ ...draft, [key]: e.target.value })} aria-describedby={error ? 'entry-filter-error' : undefined} /></label>
  return <form className="finance-panel entry-filters" onSubmit={apply} noValidate aria-label="Filtros de lançamentos">
    <div className="entry-fields">
      {!fixedType && <label>Tipo<select value={draft.tipo} onChange={e => setDraft({ ...draft, tipo: e.target.value as Filters['tipo'] })}><option value="">Todos</option><option value="RECEITA">Receita</option><option value="DESPESA">Despesa</option></select></label>}
      <label>Situação<select value={draft.status} onChange={e => setDraft({ ...draft, status: e.target.value as Filters['status'] })}><option value="">Todas</option>{Object.entries(statuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      {field('vencimentoInicio', 'Vencimento inicial', 'date')}{field('vencimentoFim', 'Vencimento final', 'date')}
    </div>
    <details><summary>Mais filtros e ordenação</summary><div className="entry-fields">
      {field('categoriaId', 'Categoria (ID)')}{field('clienteId', 'Cliente (ID)')}{field('fornecedorId', 'Fornecedor (ID)')}{field('centroCustoId', 'Centro de custo (ID)')}
      <label>Ordenar por<select value={draft.ordenarPor} onChange={e => setDraft({ ...draft, ordenarPor: e.target.value as Filters['ordenarPor'] })}><option value="dataVencimento">Vencimento</option><option value="dataEmissao">Emissão</option><option value="valor">Valor</option><option value="id">ID</option></select></label>
      <label>Direção<select value={draft.direcao} onChange={e => setDraft({ ...draft, direcao: e.target.value as Filters['direcao'] })}><option value="asc">Crescente</option><option value="desc">Decrescente</option></select></label>
      <label>Itens por página<select value={draft.tamanhoPagina} onChange={e => setDraft({ ...draft, tamanhoPagina: e.target.value })}><option value="10">10</option><option value="20">20</option><option value="50">50</option><option value="100">100</option></select></label>
    </div></details>
    {error && <p id="entry-filter-error" role="alert" className="field-error">{error}</p>}
    <div className="movement-actions"><button className="button button-primary" type="submit">Aplicar filtros</button><button className="button button-secondary" type="button" onClick={clear}>Limpar filtros</button></div>
  </form>
}
