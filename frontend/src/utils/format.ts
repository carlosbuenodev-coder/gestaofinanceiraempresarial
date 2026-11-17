export const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
export const localDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
export const validDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value) && localDate(new Date(`${value}T12:00:00`)) === value
export const dateLabel = (value: string) => value.slice(0, 10).split('-').reverse().join('/')
export interface DateRange { dataInicial: string; dataFinal: string }
export function presetRange(preset: string, today = new Date()): DateRange {
  const year = today.getFullYear(), month = today.getMonth()
  let start = new Date(year, month, 1), end = new Date(year, month + 1, 0)
  if (preset === 'previous') { start = new Date(year, month - 1, 1); end = new Date(year, month, 0) }
  if (preset === '90') { end = new Date(year, month, today.getDate()); start = new Date(year, month, today.getDate() - 89) }
  if (preset === 'year') { start = new Date(year, 0, 1); end = new Date(year, 11, 31) }
  return { dataInicial: localDate(start), dataFinal: localDate(end) }
}
