const LOCALE = 'vi-VN'

// Amounts come from the API, which does all money arithmetic in decimal. They are only displayed here.
export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat(LOCALE, { style: 'currency', currency }).format(amount)
}

export function formatTotals(totals: { currency: string; amount: number }[]): string {
  return totals.map((t) => formatMoney(t.amount, t.currency)).join(' · ')
}

/** Today's date on this device, as yyyy-MM-dd. */
export function localToday(now = new Date()): string {
  return toIsoDate(now)
}

export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return toIsoDate(new Date(year, month - 1, day + days))
}

export function formatDayTitle(isoDate: string, today = localToday()): string {
  if (isoDate === today) {
    return 'Hôm nay'
  }
  if (isoDate === addDays(today, -1)) {
    return 'Hôm qua'
  }

  const [year, month, day] = isoDate.split('-').map(Number)
  return new Intl.DateTimeFormat(LOCALE, {
    weekday: 'short',
    day: 'numeric',
    month: 'numeric',
    year: year === Number(today.slice(0, 4)) ? undefined : 'numeric',
  }).format(new Date(year, month - 1, day))
}

function parseIso(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/** Monday of the week containing the date. Must match the week the API uses. */
function mondayOf(isoDate: string): string {
  return addDays(isoDate, -((parseIso(isoDate).getDay() + 6) % 7))
}

/** A date in the neighbouring period. Months move from the 1st, so 31 January never skips February. */
export function shiftPeriod(isoDate: string, period: 'day' | 'week' | 'month', delta: number): string {
  if (period === 'day') {
    return addDays(isoDate, delta)
  }
  if (period === 'week') {
    return addDays(isoDate, delta * 7)
  }

  const date = parseIso(isoDate)
  return toIsoDate(new Date(date.getFullYear(), date.getMonth() + delta, 1))
}

export function formatPeriodTitle(period: 'day' | 'week' | 'month', isoDate: string, today = localToday()): string {
  if (period === 'day') {
    return formatDayTitle(isoDate, today)
  }

  if (period === 'week') {
    const monday = mondayOf(isoDate)
    if (monday === mondayOf(today)) {
      return 'Tuần này'
    }
    const short = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'numeric' })
    return `${short.format(parseIso(monday))} – ${short.format(parseIso(addDays(monday, 6)))}`
  }

  if (isoDate.slice(0, 7) === today.slice(0, 7)) {
    return 'Tháng này'
  }
  const date = parseIso(isoDate)
  return `Tháng ${date.getMonth() + 1}, ${date.getFullYear()}`
}

/** Clock time where the photo was taken, not where it is being viewed. */
export function formatEntryTime(takenAt: string, timeZone: string): string {
  return new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit', timeZone }).format(new Date(takenAt))
}
