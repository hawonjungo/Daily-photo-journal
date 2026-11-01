import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { SettingsPage } from '@/pages/SettingsPage'
import { SummaryPage } from '@/pages/SummaryPage'
import { TimelinePage } from '@/pages/TimelinePage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<TimelinePage />} />
          <Route path="summary" element={<SummaryPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
