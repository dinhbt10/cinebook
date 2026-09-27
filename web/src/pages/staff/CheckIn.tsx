import { useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Html5Qrcode } from 'html5-qrcode'
import { AlertTriangle, Camera, CameraOff, CheckCircle2, Keyboard, ScanLine, XCircle } from 'lucide-react'
import clsx from 'clsx'
import { api, ApiError } from '@/lib/api'
import type { CheckInResult } from '@/lib/types'
import { fullDateTime } from '@/lib/format'
import { Button, Card, Input } from '@/components/ui'

const READER_ID = 'qr-reader'

/** Mỗi kết quả soát vé có một màu và biểu tượng riêng để nhân viên nhìn phát biết ngay. */
const VERDICT: Record<string, { label: string; tone: 'ok' | 'warn' | 'bad' }> = {
  CHO_VAO: { label: 'CHO VÀO', tone: 'ok' },
  DUNG_LAI: { label: 'VÉ ĐÃ DÙNG', tone: 'bad' },
  VE_GIA: { label: 'VÉ KHÔNG HỢP LỆ', tone: 'bad' },
  DA_HUY: { label: 'VÉ ĐÃ HỦY', tone: 'bad' },
  SAI_RAP: { label: 'SAI RẠP', tone: 'bad' },
  CHUA_THANH_TOAN: { label: 'CHƯA THANH TOÁN', tone: 'bad' },
  QUA_SOM: { label: 'CHƯA TỚI GIỜ VÀO', tone: 'warn' },
  QUA_MUON: { label: 'QUÁ GIỜ VÀO CỬA', tone: 'warn' },
}

export default function CheckIn() {
  const [code, setCode] = useState('')
  const [result, setResult] = useState<CheckInResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [scanning, setScanning] = useState(false)
  const scannerRef = useRef<Html5Qrcode | null>(null)

  const scan = useMutation({
    mutationFn: (payload: string) => api.post<CheckInResult>('/api/checkin/scan', { qrPayload: payload }),
    onSuccess: (r) => { setResult(r); setError(null) },
    onError: (e: ApiError) => setError(e.message),
  })

  const byCode = useMutation({
    mutationFn: (value: string) => api.post<CheckInResult>(`/api/checkin/by-code/${encodeURIComponent(value)}`),
    onSuccess: (r) => { setResult(r); setError(null); setCode('') },
    onError: (e: ApiError) => setError(e.message),
  })

  const stopCamera = async () => {
    const s = scannerRef.current
    if (!s) return
    try { await s.stop(); await s.clear() } catch { /* camera đã dừng */ }
    scannerRef.current = null
    setScanning(false)
  }

  const startCamera = async () => {
    setError(null)
    try {
      const scanner = new Html5Qrcode(READER_ID)
      scannerRef.current = scanner
      setScanning(true)
      await scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 240, height: 240 } },
        (decoded) => { scan.mutate(decoded); void stopCamera() },
        () => { /* khung hình chưa có mã, bỏ qua */ },
      )
    } catch {
      setScanning(false)
      setError('Không mở được camera. Hãy cấp quyền cho trình duyệt, hoặc nhập mã vé bằng tay.')
    }
  }

  useEffect(() => () => { void stopCamera() }, [])

  const verdict = result ? VERDICT[result.verdict] ?? { label: result.verdict, tone: 'bad' as const } : null

  return (
    <div className="mx-auto max-w-xl px-4 py-10">
      <div className="mb-7 text-center">
        <ScanLine size={30} className="mx-auto mb-3 text-brand-500" />
        <h1 className="font-display text-2xl font-extrabold">Soát vé</h1>
        <p className="mt-1.5 text-[13px] text-faint">Quét mã QR trên vé của khách tại cửa phòng chiếu</p>
      </div>

      {/* Kết quả */}
      {verdict && result && (
        <div className={clsx(
          'mb-6 animate-rise rounded-2xl border p-6 text-center',
          verdict.tone === 'ok' && 'border-mint-500/45 bg-mint-950',
          verdict.tone === 'warn' && 'border-brand-600/45 bg-brand-950',
          verdict.tone === 'bad' && 'border-velvet-500/45 bg-velvet-950',
        )}>
          {verdict.tone === 'ok' ? <CheckCircle2 size={46} className="mx-auto text-mint-400" />
            : verdict.tone === 'warn' ? <AlertTriangle size={46} className="mx-auto text-brand-400" />
            : <XCircle size={46} className="mx-auto text-velvet-400" />}

          <p className={clsx('mt-3 font-display text-2xl font-extrabold tracking-tight',
            verdict.tone === 'ok' ? 'text-mint-400'
              : verdict.tone === 'warn' ? 'text-brand-400' : 'text-velvet-400')}>
            {verdict.label}
          </p>
          <p className="mt-1.5 text-sm text-cream">{result.message}</p>

          {result.ticketCode && (
            <dl className="mt-5 grid grid-cols-2 gap-3 border-t border-white/10 pt-4 text-left text-[13px]">
              <Info label="Mã vé" value={result.ticketCode} mono />
              {result.seatLabel && <Info label="Ghế" value={result.seatLabel} mono />}
              {result.movieTitle && <Info label="Phim" value={result.movieTitle} />}
              {result.roomName && <Info label="Phòng" value={result.roomName} />}
              {result.startsAtUtc && <Info label="Suất chiếu" value={fullDateTime(result.startsAtUtc)} />}
            </dl>
          )}

          <Button variant="secondary" className="mt-5" onClick={() => setResult(null)}>
            Quét vé tiếp theo
          </Button>
        </div>
      )}

      {!result && (
        <Card className="p-6">
          <div id={READER_ID} className={clsx('mb-4 overflow-hidden rounded-xl', !scanning && 'hidden')} />

          {!scanning ? (
            <Button size="lg" className="w-full" onClick={startCamera} loading={scan.isPending}>
              <Camera size={17} /> Bật camera quét mã
            </Button>
          ) : (
            <Button size="lg" variant="secondary" className="w-full" onClick={stopCamera}>
              <CameraOff size={17} /> Tắt camera
            </Button>
          )}

          <div className="my-5 flex items-center gap-3 text-[12px] text-faint">
            <span className="h-px flex-1 bg-ink-700" /> hoặc <span className="h-px flex-1 bg-ink-700" />
          </div>

          <form onSubmit={(e) => { e.preventDefault(); if (code.trim()) byCode.mutate(code.trim()) }}
                className="flex flex-col gap-3">
            <div className="flex items-center gap-2 text-[13px] text-muted">
              <Keyboard size={15} /> Nhập mã vé khi mã QR bị mờ
            </div>
            <div className="flex gap-2">
              <Input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())}
                     placeholder="TKXXXXXXXX" className="font-mono uppercase" />
              <Button type="submit" loading={byCode.isPending} disabled={!code.trim()}>Tra cứu</Button>
            </div>
          </form>

          {error && <p className="mt-4 text-[13px] text-velvet-400">{error}</p>}
        </Card>
      )}
    </div>
  )
}

function Info({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wide text-faint">{label}</dt>
      <dd className={clsx('mt-0.5 font-medium text-cream', mono && 'font-mono')}>{value}</dd>
    </div>
  )
}
