import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useState } from 'react'
import { ApiKeyNotice } from '@/components/ApiKeyNotice'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ApiError, apiGet, getApiKey, type CurrencyTotal, type SummaryPeriod, type SummaryResponse } from '@/lib/api'
import { formatPeriodTitle, formatTotals, localToday, shiftPeriod } from '@/lib/format'

const PERIODS: { value: SummaryPeriod; label: string }[] = [
  { value: 'day', label: 'Ngày' },
  { value: 'week', label: 'Tuần' },
  { value: 'month', label: 'Tháng' },
]

type Result =
  | { query: string; kind: 'loaded'; summary: SummaryResponse }
  | { query: string; kind: 'unauthorized' | 'error' }

export function SummaryPage() {
  const [period, setPeriod] = useState<SummaryPeriod>('day')
  const [date, setDate] = useState(localToday)
  const [attempt, setAttempt] = useState(0)
  const [result, setResult] = useState<Result | null>(null)

  const query = `period=${period}&date=${date}&attempt=${attempt}`

  useEffect(() => {
    if (!getApiKey()) {
      return
    }

    const abort = new AbortController()
    apiGet<SummaryResponse>(`/api/summary?period=${period}&date=${date}`, abort.signal)
      .then((summary) => setResult({ query, kind: 'loaded', summary }))
      .catch((error: unknown) => {
        if (!abort.signal.aborted) {
          setResult({ query, kind: error instanceof ApiError && error.status === 401 ? 'unauthorized' : 'error' })
        }
      })
    return () => abort.abort()
  }, [period, date, query])

  if (!getApiKey()) {
    return <ApiKeyNotice />
  }
  if (result?.query === query && result.kind === 'unauthorized') {
    return <ApiKeyNotice invalid />
  }

  const current = result?.query === query ? result : null
  const summary = current?.kind === 'loaded' ? current.summary : null
  const isLatest = summary === null || summary.to >= localToday()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-heading text-xl font-semibold">Tổng kết</h1>

      <Tabs value={period} onValueChange={(value) => setPeriod(value as SummaryPeriod)}>
        <TabsList className="w-full group-data-horizontal/tabs:h-11">
          {PERIODS.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <div className="flex items-center justify-between gap-2">
        <Button
          variant="outline"
          size="icon"
          className="size-11"
          aria-label="Kỳ trước"
          onClick={() => setDate(shiftPeriod(date, period, -1))}
        >
          <ChevronLeft aria-hidden />
        </Button>
        <p className="text-center font-medium" aria-live="polite">
          {formatPeriodTitle(period, date)}
        </p>
        <Button
          variant="outline"
          size="icon"
          className="size-11"
          aria-label="Kỳ sau"
          disabled={isLatest}
          onClick={() => setDate(shiftPeriod(date, period, 1))}
        >
          <ChevronRight aria-hidden />
        </Button>
      </div>

      {current === null && <SummarySkeleton />}

      {current?.kind === 'error' && (
        <Card>
          <CardHeader>
            <CardTitle>Không tải được tổng kết</CardTitle>
            <CardDescription>Kiểm tra kết nối mạng rồi thử lại.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="outline" className="min-h-11" onClick={() => setAttempt(attempt + 1)}>
              Thử lại
            </Button>
          </CardContent>
        </Card>
      )}

      {summary && <SummaryDetails summary={summary} />}
    </div>
  )
}

function SummaryDetails({ summary }: { summary: SummaryResponse }) {
  if (summary.count === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Chưa có ảnh nào</CardTitle>
          <CardDescription>Không có ảnh trong kỳ này.</CardDescription>
        </CardHeader>
      </Card>
    )
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardDescription>Tổng chi tiêu</CardDescription>
          {summary.totals.length > 0 ? (
            <ul className="flex flex-col gap-1">
              {summary.totals.map((total) => (
                <li key={total.currency} className="font-heading text-2xl font-semibold tabular-nums">
                  {formatTotals([total])}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground">Chưa nhập giá</p>
          )}
          <CardDescription>{summary.count} ảnh</CardDescription>
        </CardHeader>
      </Card>

      {summary.topPlaces.length > 0 && (
        <BreakdownCard
          title="Quán hay đến"
          rows={summary.topPlaces.map((place) => ({
            key: place.placeId,
            label: place.name,
            count: place.count,
            totals: place.totals,
          }))}
        />
      )}

      <BreakdownCard
        title="Theo danh mục"
        rows={summary.categories.map((category) => ({
          key: category.category ?? 'none',
          label: category.category ?? 'Chưa phân loại',
          count: category.count,
          totals: category.totals,
        }))}
      />
    </>
  )
}

type BreakdownRow = { key: string; label: string; count: number; totals: CurrencyTotal[] }

function BreakdownCard({ title, rows }: { title: string; rows: BreakdownRow[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-3">
          {rows.map((row) => (
            <li key={row.key} className="flex items-baseline justify-between gap-3">
              <span className="min-w-0">
                <span className="block truncate">{row.label}</span>
                <span className="text-muted-foreground block text-xs">{row.count} ảnh</span>
              </span>
              <span className="shrink-0 text-right text-sm font-medium tabular-nums">{formatTotals(row.totals)}</span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function SummarySkeleton() {
  return (
    <div className="flex flex-col gap-4" aria-busy="true" aria-label="Đang tải">
      <Skeleton className="h-28 rounded-xl" />
      <Skeleton className="h-40 rounded-xl" />
    </div>
  )
}
