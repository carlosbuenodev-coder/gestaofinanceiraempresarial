import type { Periodo, Totals } from '../types/cashflow'
// Display-only grouping of complete daily API aggregates. No payment or balance rules are recalculated here.
export function monthlyRows(periods: Periodo[]): { label: string; values: number[] }[] {
  const fields: (keyof Totals)[] = ['saldoPrevisto', 'saldoRealizado']
  const months = new Map<string, number[]>()
  for (const period of periods) {
    const month = period.data.slice(0, 7)
    const values = months.get(month) ?? [0, 0]
    fields.forEach((field, i) => { values[i] += period[field] })
    months.set(month, values)
  }
  return [...months].sort(([a], [b]) => a.localeCompare(b)).map(([month, values]) => ({ label: month.split('-').reverse().join('/'), values }))
}
