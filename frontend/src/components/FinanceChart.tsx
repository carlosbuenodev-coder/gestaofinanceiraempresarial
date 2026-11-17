import { money } from '../utils/format'
export function MetricCard({ label, value, tone = '' }: { label: string; value: number; tone?: string }) {
  return <article className={`metric-card ${tone}`}><h2>{label}</h2><strong>{money(value)}</strong></article>
}
export interface ChartRow { label: string; values: number[] }
export function FinanceChart({ title, rows, series }: { title: string; rows: ChartRow[]; series: string[] }) {
  const max = Math.max(1, ...rows.flatMap(r => r.values.map(Math.abs)))
  const hasNegative = rows.some(r => r.values.some(v => v < 0))
  const origin = hasNegative ? 365 : 115, span = hasNegative ? 200 : 420
  const width = 680, height = Math.max(130, rows.length * 78 + 20)
  return <section className="finance-panel"><h2>{title}</h2><div className="chart-legend">{series.map((s, i) => <span key={s}><i className={`series-${i}`} />{s}</span>)}</div>
    {!rows.length ? <p className="empty-state">Sem dados para este gráfico.</p> : <><div className="chart-scroll"><svg className="finance-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={title}>
      <title>{title}. Valores negativos à esquerda do eixo; positivos à direita.</title><line x1={origin} x2={origin} y1="0" y2={height} stroke="#9cabbc" />{rows.map((r, n) => <g key={`${r.label}-${n}`}><text x="0" y={n * 78 + 22}>{r.label}</text>{r.values.map((v, i) => <rect key={i} className={`series-${i}`} x={v < 0 ? origin - Math.abs(v) / max * span : origin} y={n * 78 + i * 13 + 8} width={Math.abs(v) / max * span} height="9" rx="3"><title>{`${r.label} · ${series[i]}: ${money(v)}`}</title></rect>)}</g>)}
    </svg></div><details><summary>Ver valores do gráfico</summary><div className="table-scroll"><table><caption>{title}</caption><thead><tr><th scope="col">Período</th>{series.map(s => <th key={s} scope="col">{s}</th>)}</tr></thead><tbody>{rows.map((r, i) => <tr key={i}><th scope="row">{r.label}</th>{r.values.map((v, j) => <td key={j}>{money(v)}</td>)}</tr>)}</tbody></table></div></details></>}
  </section>
}
