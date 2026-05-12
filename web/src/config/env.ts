// 统一管理前端环境配置（API 地址等）

export const ENV_MODE = (import.meta as any).env?.MODE || process.env.NODE_ENV || 'development'

// 本地开发后端源地址（Vite 代理与需要直连后端的场景共用）
// 可通过 VITE_DEV_BACKEND_ORIGIN 覆盖，例如 http://127.0.0.1:5001
const ENV_DEV_BACKEND_ORIGIN = (import.meta as any).env?.VITE_DEV_BACKEND_ORIGIN
export const DEV_BACKEND_ORIGIN: string =
	ENV_DEV_BACKEND_ORIGIN !== undefined ? String(ENV_DEV_BACKEND_ORIGIN) : 'http://localhost:5288'

// 本地调试与生产环境的默认 API 地址（可按需修改）
// 开发环境使用相对路径，走 Vite 代理，避免 CORS
export const DEV_API_BASE_URL = '/api'
// 生产环境使用相对路径（SPA与API同源部署时无需跨域）
export const PROD_API_BASE_URL = '/api'

// 支持通过 Vite 环境变量覆盖（.env / .env.production 中配置 VITE_API_BASE_URL）
const ENV_API = (import.meta as any).env?.VITE_API_BASE_URL

export const API_BASE_URL: string =
	ENV_API
		? String(ENV_API)
		: (ENV_MODE === 'production' ? PROD_API_BASE_URL : DEV_API_BASE_URL)

// 资源/附件基础URL（用于拼接 /uploads/... 等静态资源路径）
// 开发环境走 Vite 代理（同源），生产环境同源部署时为空字符串
// 可通过 VITE_UPLOADS_BASE_URL 环境变量覆盖（如需 CDN 或独立域名）
const ENV_UPLOADS = (import.meta as any).env?.VITE_UPLOADS_BASE_URL
export const UPLOADS_BASE_URL: string =
	ENV_UPLOADS !== undefined ? String(ENV_UPLOADS) : ''


