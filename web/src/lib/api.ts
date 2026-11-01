const API_KEY_STORAGE = 'relifes.apiKey'

export type CurrencyTotal = {
  currency: string
  amount: number
}

export type TimelineEntry = {
  id: string
  takenAt: string
  timeZone: string
  imageUrl: string
  thumbUrl: string
  price: number | null
  currency: string | null
  placeId: string | null
  placeName: string | null
  category: string | null
  note: string | null
  rating: number | null
}

export type TimelineDay = {
  date: string
  count: number
  totals: CurrencyTotal[]
  entries: TimelineEntry[]
}

export type TimelineResponse = {
  from: string
  to: string
  days: TimelineDay[]
}

export type SummaryPeriod = 'day' | 'week' | 'month'

export type SummaryResponse = {
  period: SummaryPeriod
  from: string
  to: string
  count: number
  totals: CurrencyTotal[]
  topPlaces: { placeId: string; name: string; count: number; totals: CurrencyTotal[] }[]
  categories: { category: string | null; count: number; totals: CurrencyTotal[] }[]
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export function getApiKey(): string | null {
  return localStorage.getItem(API_KEY_STORAGE)
}

export function setApiKey(key: string | null) {
  if (key) {
    localStorage.setItem(API_KEY_STORAGE, key)
  } else {
    localStorage.removeItem(API_KEY_STORAGE)
  }
}

export async function apiGet<T>(path: string, signal?: AbortSignal, apiKey = getApiKey()): Promise<T> {
  const response = await fetch(path, {
    headers: apiKey ? { 'X-Api-Key': apiKey } : {},
    signal,
  })

  if (!response.ok) {
    throw new ApiError(response.status, `Request to ${path} failed with status ${response.status}`)
  }

  return (await response.json()) as T
}
