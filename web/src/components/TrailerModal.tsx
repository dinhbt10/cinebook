import { useEffect } from 'react'
import { X } from 'lucide-react'

/** Lấy mã video YouTube từ các dạng link thường gặp. */
function youtubeId(url: string): string | null {
  const m = url.match(/(?:youtu\.be\/|v=|\/embed\/|\/shorts\/)([\w-]{11})/)
  return m ? m[1] : null
}

/**
 * Cửa sổ xem trailer. Trước đây trường TrailerUrl có trong cơ sở dữ liệu
 * nhưng giao diện chưa dùng tới, nên trang phim không có gì để xem ngoài chữ.
 */
export function TrailerModal({ title, url, onClose }: {
  title: string; url: string | null; onClose: () => void
}) {
  useEffect(() => {
    if (!url) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [url, onClose])

  if (!url) return null
  const id = youtubeId(url)

  return (
    <div onClick={onClose} role="dialog" aria-modal aria-label={`Trailer ${title}`}
         style={{ background: 'var(--color-scrim)' }}
         className="fixed inset-0 z-50 flex items-center justify-center p-4 backdrop-blur-md">
      <div onClick={(e) => e.stopPropagation()}
           className="w-full max-w-4xl animate-rise overflow-hidden rounded-2xl border border-ink-700 bg-ink-900">
        <div className="flex items-center justify-between gap-4 border-b border-ink-800 px-5 py-3.5">
          <p className="truncate font-display font-bold">{title}</p>
          <button onClick={onClose} aria-label="Đóng"
                  className="rounded-lg p-1.5 text-faint transition-colors hover:bg-ink-800 hover:text-cream">
            <X size={18} />
          </button>
        </div>

        <div className="aspect-video w-full bg-black">
          {id ? (
            <iframe
              className="h-full w-full"
              src={`https://www.youtube-nocookie.com/embed/${id}?autoplay=1&rel=0`}
              title={`Trailer ${title}`}
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; picture-in-picture"
              allowFullScreen
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 text-sm text-faint">
              <p>Không nhúng được trailer này.</p>
              <a href={url} target="_blank" rel="noreferrer"
                 className="text-brand-400 underline underline-offset-4">Mở ở tab mới</a>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
