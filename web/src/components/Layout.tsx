import { CalendarDays, ChartPie, Settings } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'
import { cn } from '@/lib/utils'

const NAV_ITEMS = [
  { to: '/', label: 'Nhật ký', icon: CalendarDays },
  { to: '/summary', label: 'Tổng kết', icon: ChartPie },
  { to: '/settings', label: 'Cài đặt', icon: Settings },
]

export function Layout() {
  return (
    <div className="mx-auto flex min-h-dvh max-w-3xl flex-col">
      <main className="flex-1 px-3 pt-[max(0.75rem,env(safe-area-inset-top))] pb-24">
        <Outlet />
      </main>

      <nav className="bg-background/95 fixed inset-x-0 bottom-0 z-20 border-t pb-[env(safe-area-inset-bottom)] backdrop-blur">
        <div className="mx-auto flex max-w-3xl">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                cn(
                  'flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-xs',
                  isActive ? 'text-foreground font-medium' : 'text-muted-foreground',
                )
              }
            >
              <Icon className="size-5" aria-hidden />
              {label}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  )
}
