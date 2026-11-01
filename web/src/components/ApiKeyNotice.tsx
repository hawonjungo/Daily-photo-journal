import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function ApiKeyNotice({ invalid = false }: { invalid?: boolean }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{invalid ? 'API key không hợp lệ' : 'Chưa có API key'}</CardTitle>
        <CardDescription>Nhập API key trong Cài đặt để xem nhật ký của bạn.</CardDescription>
      </CardHeader>
      <CardContent>
        <Link to="/settings" className={buttonVariants()}>
          Mở Cài đặt
        </Link>
      </CardContent>
    </Card>
  )
}
