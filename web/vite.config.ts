import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

/** Cổng cố định của giao diện. Máy chủ chạy nhiều dự án nên mỗi dự án giữ đúng một cổng. */
const FE_PORT = 5111

/** Cổng của API .NET, nơi proxy chuyển tiếp mọi lời gọi backend tới. */
const API = 'http://localhost:5099'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  server: {
    port: FE_PORT,
    // Cổng bận thì báo lỗi và dừng, KHÔNG tự nhảy sang 5112.
    // Nhảy cổng âm thầm là nguồn gốc của lỗi khó hiểu: đường quay lại của cổng
    // thanh toán và cấu hình chuyển tiếp cổng đều trỏ cứng vào FE_PORT.
    strictPort: true,
    proxy: {
      // Gọi API qua proxy để không phải bận tâm CORS, và để mọi thứ của giao diện
      // đi chung một cổng — kể cả đường VNPay đưa khách quay lại.
      '/api': API,
      '/mock-gateway': API,
    },
  },
  preview: {
    port: FE_PORT,
    strictPort: true,
    proxy: {
      '/api': API,
      '/mock-gateway': API,
    },
  },
  build: {
    // Trên Vercel phải xuất vào thư mục con của chính dự án (web/dist) — Vercel không
    // nhận output nằm ngoài. Khi build ở máy thì vẫn xuất thẳng vào wwwroot của API
    // để chạy gộp một tiến trình như trước.
    outDir: process.env.VERCEL ? 'dist' : '../src/CineBook.Api/wwwroot',
    emptyOutDir: true,
  },
})
