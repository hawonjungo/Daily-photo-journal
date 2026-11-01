import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { ApiError, apiGet, getApiKey, setApiKey } from '@/lib/api'
import { localToday } from '@/lib/format'
import { getTheme, setTheme, type Theme } from '@/lib/theme'

const THEMES: { value: Theme; label: string }[] = [
  { value: 'system', label: 'Theo máy' },
  { value: 'light', label: 'Sáng' },
  { value: 'dark', label: 'Tối' },
]

type KeyStatus = { kind: 'idle' | 'checking' | 'saved' | 'removed' } | { kind: 'error'; message: string }

export function SettingsPage() {
  const [key, setKey] = useState('')
  const [hasKey, setHasKey] = useState(() => getApiKey() !== null)
  const [status, setStatus] = useState<KeyStatus>({ kind: 'idle' })
  const [theme, setThemeState] = useState(getTheme)

  async function saveKey(event: FormEvent) {
    event.preventDefault()
    const candidate = key.trim()
    if (!candidate) {
      return
    }

    setStatus({ kind: 'checking' })
    try {
      const today = localToday()
      await apiGet(`/api/entries?from=${today}&to=${today}`, undefined, candidate)
      setApiKey(candidate)
      setHasKey(true)
      setKey('')
      setStatus({ kind: 'saved' })
    } catch (error) {
      setStatus({
        kind: 'error',
        message: error instanceof ApiError && error.status === 401 ? 'API key không đúng.' : 'Không kết nối được máy chủ.',
      })
    }
  }

  function removeKey() {
    setApiKey(null)
    setHasKey(false)
    setStatus({ kind: 'removed' })
  }

  function chooseTheme(value: Theme) {
    setTheme(value)
    setThemeState(value)
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-heading text-xl font-semibold">Cài đặt</h1>

      <Card>
        <CardHeader>
          <CardTitle>API key</CardTitle>
          <CardDescription>{hasKey ? 'Đã lưu trên thiết bị này.' : 'Chưa có key trên thiết bị này.'}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={saveKey} className="flex flex-col gap-3">
            <Input
              type="password"
              value={key}
              onChange={(event) => setKey(event.target.value)}
              placeholder="rl_..."
              autoComplete="off"
              aria-label="API key"
              className="min-h-11"
            />
            <div className="flex gap-2">
              <Button type="submit" className="min-h-11 flex-1" disabled={status.kind === 'checking' || !key.trim()}>
                {status.kind === 'checking' ? 'Đang kiểm tra...' : 'Lưu'}
              </Button>
              {hasKey && (
                <Button type="button" variant="outline" className="min-h-11" onClick={removeKey}>
                  Xoá key
                </Button>
              )}
            </div>
            <p role="status" className="min-h-5 text-sm">
              {status.kind === 'saved' && 'Đã lưu API key.'}
              {status.kind === 'removed' && 'Đã xoá API key khỏi thiết bị này.'}
              {status.kind === 'error' && <span className="text-destructive">{status.message}</span>}
            </p>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Giao diện</CardTitle>
        </CardHeader>
        <CardContent className="flex gap-2">
          {THEMES.map(({ value, label }) => (
            <Button
              key={value}
              type="button"
              variant={theme === value ? 'default' : 'outline'}
              aria-pressed={theme === value}
              className="min-h-11 flex-1"
              onClick={() => chooseTheme(value)}
            >
              {label}
            </Button>
          ))}
        </CardContent>
      </Card>
    </div>
  )
}
