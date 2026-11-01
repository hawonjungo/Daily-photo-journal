import { ImageOff } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiKeyNotice } from '@/components/ApiKeyNotice'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, apiGet, getApiKey, type TimelineDay, type TimelineEntry, type TimelineResponse } from '@/lib/api'
import { addDays, formatDayTitle, formatEntryTime, formatMoney, formatTotals, localToday } from '@/lib/format'

const WINDOW_DAYS = 30

type LoadState = 'loading' | 'idle' | 'error' | 'unauthorized'

export function TimelinePage() {
  const [days, setDays] = useState<TimelineDay[]>([])
  const [state, setState] = useState<LoadState>('loading')
  const [nextEnd, setNextEnd] = useState(localToday)
  const controller = useRef<AbortController | null>(null)

  const load = useCallback((end: string) => {
    controller.current?.abort()
    const abort = new AbortController()
    controller.current = abort

    const from = addDays(end, -(WINDOW_DAYS - 1))
    apiGet<TimelineResponse>(`/api/entries?from=${from}&to=${end}`, abort.signal)
      .then((response) => {
        setDays((current) => [...current, ...response.days])
        setNextEnd(addDays(from, -1))
        setState('idle')
      })
      .catch((error: unknown) => {
        if (abort.signal.aborted) {
          return
        }
        setState(error instanceof ApiError && error.status === 401 ? 'unauthorized' : 'error')
      })
  }, [])

  useEffect(() => {
    if (getApiKey()) {
      load(localToday())
    }
    return () => controller.current?.abort()
  }, [load])

  if (!getApiKey()) {
    return <ApiKeyNotice />
  }
  if (state === 'unauthorized') {
    return <ApiKeyNotice invalid />
  }

  return (
    <div className="flex flex-col gap-6">
      {days.map((day) => (
        <DaySection key={day.date} day={day} />
      ))}

      {state === 'idle' && days.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Chưa có ảnh nào</CardTitle>
            <CardDescription>Không có ảnh trong {WINDOW_DAYS} ngày gần đây.</CardDescription>
          </CardHeader>
        </Card>
      )}

      {state === 'loading' && <TimelineSkeleton />}

      {state === 'error' && (
        <Card>
          <CardHeader>
            <CardTitle>Không tải được nhật ký</CardTitle>
            <CardDescription>Kiểm tra kết nối mạng rồi thử lại.</CardDescription>
          </CardHeader>
        </Card>
      )}

      {state !== 'loading' && (
        <Button variant="outline" className="min-h-11" onClick={() => {
            setState('loading')
            load(nextEnd)
          }}>
          {state === 'error' ? 'Thử lại' : 'Xem cũ hơn'}
        </Button>
      )}
    </div>
  )
}

function DaySection({ day }: { day: TimelineDay }) {
  return (
    <section aria-label={formatDayTitle(day.date)}>
      <header className="bg-background/95 sticky top-0 z-10 flex items-baseline justify-between gap-3 py-2 backdrop-blur">
        <h2 className="font-heading text-base font-semibold">{formatDayTitle(day.date)}</h2>
        <p className="text-right text-sm font-medium tabular-nums">
          {day.totals.length > 0 ? formatTotals(day.totals) : <span className="text-muted-foreground">{day.count} ảnh</span>}
        </p>
      </header>

      <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
        {day.entries.map((entry) => (
          <li key={entry.id}>
            <EntryTile entry={entry} />
          </li>
        ))}
      </ul>
    </section>
  )
}

function EntryTile({ entry }: { entry: TimelineEntry }) {
  const [failed, setFailed] = useState(false)
  const time = formatEntryTime(entry.takenAt, entry.timeZone)

  return (
    <a href={entry.imageUrl} target="_blank" rel="noreferrer" className="bg-muted relative block aspect-square overflow-hidden rounded-lg">
      {failed ? (
        <span className="text-muted-foreground flex size-full items-center justify-center">
          <ImageOff className="size-6" aria-label="Không tải được ảnh" />
        </span>
      ) : (
        <img
          src={entry.thumbUrl}
          alt={entry.placeName ?? `Ảnh lúc ${time}`}
          loading="lazy"
          onError={() => setFailed(true)}
          className="size-full object-cover"
        />
      )}

      {entry.price !== null && entry.currency !== null && (
        <Badge className="absolute top-2 right-2 tabular-nums">{formatMoney(entry.price, entry.currency)}</Badge>
      )}

      <span className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/75 to-transparent px-2 pt-6 pb-1.5 text-white">
        {entry.placeName && <span className="block truncate text-sm font-medium">{entry.placeName}</span>}
        <span className="block text-xs opacity-90">{time}</span>
      </span>
    </a>
  )
}

function TimelineSkeleton() {
  return (
    <div className="flex flex-col gap-2" aria-busy="true" aria-label="Đang tải">
      <Skeleton className="h-6 w-32" />
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="aspect-square rounded-lg" />
        ))}
      </div>
    </div>
  )
}
