/**
 * 前台门户（/web）配置
 * 控制是否启用前台门户访问
 */

// 是否启用前台门户（/web 路径）
// 设为 false 时，访问 /web 下的所有页面将被重定向到后台登录页
// 可通过环境变量 VITE_PORTAL_ENABLED 覆盖（值为 'true' 或 'false'）
const ENV_PORTAL = true

export const portalEnabled: boolean =
  ENV_PORTAL !== undefined ? String(ENV_PORTAL) === 'true' : true
