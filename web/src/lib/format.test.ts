import { describe, expect, it } from 'vitest'
import {
  addDays,
  formatDayTitle,
  formatEntryTime,
  formatMoney,
  formatPeriodTitle,
  formatTotals,
  localToday,
  shiftPeriod,
} from './format'

// Intl separates a Vietnamese amount from its symbol with a non-breaking space.
const normalize = (text: string) => text.replace(/\s/g, ' ')

describe('formatMoney', () => {
  it('shows VND without decimals', () => {
    expect(normalize(formatMoney(45000, 'VND'))).toBe('45.000 ₫')
  })

  it('shows AUD with two decimals', () => {
    expect(normalize(formatMoney(12.5, 'AUD'))).toBe('12,50 AU$')
  })
})

describe('formatTotals', () => {
  it('keeps currencies separate', () => {
    const totals = [
      { currency: 'AUD', amount: 12.5 },
      { currency: 'VND', amount: 45000 },
    ]

    expect(normalize(formatTotals(totals))).toBe('12,50 AU$ · 45.000 ₫')
  })

  it('is empty when nothing has a price', () => {
    expect(formatTotals([])).toBe('')
  })
})

describe('dates', () => {
  it('reads the date on the device, not in UTC', () => {
    expect(localToday(new Date(2026, 10, 1, 23, 59))).toBe('2026-11-01')
  })

  it('crosses month and year boundaries', () => {
    expect(addDays('2026-11-01', -1)).toBe('2026-10-31')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
  })

  it('names today and yesterday', () => {
    expect(formatDayTitle('2026-11-01', '2026-11-01')).toBe('Hôm nay')
    expect(formatDayTitle('2026-10-31', '2026-11-01')).toBe('Hôm qua')
  })

  it('shows the year only for other years', () => {
    expect(formatDayTitle('2026-10-20', '2026-11-01')).not.toContain('2026')
    expect(formatDayTitle('2025-10-20', '2026-11-01')).toContain('2025')
  })

  it('moves months without skipping short ones', () => {
    expect(shiftPeriod('2027-01-31', 'month', 1)).toBe('2027-02-01')
    expect(shiftPeriod('2027-01-15', 'month', -1)).toBe('2026-12-01')
  })

  it('moves weeks and days', () => {
    expect(shiftPeriod('2026-11-01', 'week', -1)).toBe('2026-10-25')
    expect(shiftPeriod('2026-11-01', 'day', 1)).toBe('2026-11-02')
  })

  it('titles periods, treating Sunday as the end of its week', () => {
    // 1 November 2026 is a Sunday, in the week of 26 October.
    expect(formatPeriodTitle('week', '2026-10-26', '2026-11-01')).toBe('Tuần này')
    expect(formatPeriodTitle('week', '2026-11-02', '2026-11-01')).toBe('2/11 – 8/11')
    expect(formatPeriodTitle('month', '2026-11-20', '2026-11-01')).toBe('Tháng này')
    expect(formatPeriodTitle('month', '2026-10-01', '2026-11-01')).toBe('Tháng 10, 2026')
    expect(formatPeriodTitle('day', '2026-11-01', '2026-11-01')).toBe('Hôm nay')
  })

  it('shows the time where the photo was taken', () => {
    expect(formatEntryTime('2026-11-01T05:30:00Z', 'Asia/Ho_Chi_Minh')).toBe('12:30')
    expect(formatEntryTime('2026-11-01T05:30:00Z', 'Australia/Sydney')).toBe('16:30')
  })
})
