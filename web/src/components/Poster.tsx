import { useId } from 'react'
import clsx from 'clsx'
import { artBackground, isPlaceholder, movieArt } from '@/lib/art'

/**
 * Poster phim. Có ảnh thật thì dùng ảnh; không có (hoặc chỉ là ảnh tạm của
 * dữ liệu mẫu) thì dựng một tấm bìa bằng gradient và hoạ tiết tia chiếu,
 * để lưới phim không còn là một dãy khối màu phẳng giống hệt nhau.
 */
export function Poster({ title, posterUrl, className, eager }: {
  title: string
  posterUrl?: string | null
  className?: string
  eager?: boolean
}) {
  // Hook phải gọi trước mọi nhánh return, nếu không thứ tự hook đổi giữa các lần render.
  const beamId = useId()

  if (!isPlaceholder(posterUrl)) {
    return (
      <img
        src={posterUrl!}
        alt={title}
        loading={eager ? 'eager' : 'lazy'}
        className={clsx('h-full w-full object-cover', className)}
      />
    )
  }

  const art = movieArt(title)

  return (
    <div className={clsx('on-dark relative h-full w-full overflow-hidden', className)}
         style={{ background: artBackground(title) }}
         role="img" aria-label={title}>
      {/* Chữ cái đầu phóng lớn làm hoạ tiết nền */}
      <span aria-hidden
            className="pointer-events-none absolute -right-[8%] -top-[6%] select-none font-display font-extrabold leading-none"
            style={{ fontSize: '13rem', color: '#fff', opacity: 0.07 }}>
        {art.initial}
      </span>

      {/* Chùm sáng hắt ra từ máy chiếu */}
      <svg aria-hidden viewBox="0 0 100 150" preserveAspectRatio="none"
           className="absolute inset-0 h-full w-full">
        <defs>
          <linearGradient id={beamId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={art.glow} stopOpacity="0.34" />
            <stop offset="100%" stopColor={art.glow} stopOpacity="0" />
          </linearGradient>
        </defs>
        <polygon points="50,-10 96,150 4,150" fill={`url(#${CSS.escape(beamId)})`} />
      </svg>

      <div className="absolute inset-0 grain opacity-40" aria-hidden />
      <div className="absolute inset-x-0 bottom-0 h-2/5 bg-gradient-to-t from-ink-950/85 to-transparent" aria-hidden />

      <div className="absolute inset-x-0 bottom-0 p-3.5">
        <p className="font-display text-[15px] font-extrabold leading-tight text-white/95 drop-shadow">
          {title}
        </p>
        <span className="mt-1.5 block h-[2px] w-8 rounded-full" style={{ background: art.glow }} />
      </div>
    </div>
  )
}

/**
 * Ảnh nền ngang cho hero và đầu trang chi tiết. Không có ảnh thật thì dựng
 * bằng gradient sinh từ tên phim — vẫn tạo được chiều sâu cho khối lớn.
 */
export function Backdrop({ title, backdropUrl, className }: {
  title: string
  backdropUrl?: string | null
  className?: string
}) {
  const beamId = useId()

  if (!isPlaceholder(backdropUrl)) {
    return (
      <img src={backdropUrl!} alt="" aria-hidden
           className={clsx('h-full w-full object-cover', className)} />
    )
  }

  const art = movieArt(title)

  return (
    <div className={clsx('on-dark h-full w-full', className)} aria-hidden
         style={{ background: artBackground(title) }}>
      <svg viewBox="0 0 160 90" preserveAspectRatio="none" className="h-full w-full">
        <defs>
          <linearGradient id={beamId} x1="0.5" y1="0" x2="0.2" y2="1">
            <stop offset="0%" stopColor={art.glow} stopOpacity="0.30" />
            <stop offset="100%" stopColor={art.glow} stopOpacity="0" />
          </linearGradient>
        </defs>
        <polygon points="82,-10 140,90 30,90" fill={`url(#${CSS.escape(beamId)})`} />
        <polygon points="52,-10 96,90 10,90" fill={`url(#${CSS.escape(beamId)})`} opacity="0.55" />
      </svg>
    </div>
  )
}
