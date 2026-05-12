# Web宿主附录

## 1. 功能定位
本附录用于补充说明 Web 宿主的统一 HTTP、状态管理、插件注入、系统管理页入口和后台/门户双令牌模式，作为各功能分册的端侧补充。

## 2. 核心能力
- `web/src/api/http.ts` 统一处理 `/api` 前缀、后台令牌、门户令牌、401/403 和业务错误钩子。
- `web/src/stores` 提供后台认证、门户认证、系统配置、菜单、标签页等公共状态。
- `web/src/views/admin/system` 集中了大多数主框架系统管理页。
- `web/src/views/web` 集中了门户首页（`web/src/views/web/index`）、登录、用户中心等门户页。
- `web/src/plugins/core` 提供 Web 插件管理器、依赖加载器、钩子系统和资产加载能力。

## 3. 使用场景
- 新增后台系统页或门户页。
- 接入后台 API、文件接口、系统设置接口或插件钩子。
- 为主框架能力补充前端交互，而不改动业务模块/插件页。

## 4. 使用方法
- 所有 API 优先通过 `@/api/http` 封装，避免直接用裸 `fetch`。
- 新后台系统页应优先放在 `web/src/views/admin/system` 或宿主既有目录下。
- 新门户页应优先放在 `web/src/views/web` 下，且复用 `webAuth` Store 与统一 `http`。
- 需要动态扩展时，通过 `web/src/plugins/core` 走插件机制而不是直接改壳。
- 后台与门户共享同一后端，但根据路径使用不同令牌键。

## 5. 快速调用
- 登录：`web/src/api/auth.ts`
- 系统配置：`web/src/api/system.ts`
- 模块管理：`web/src/api/module.ts`
- 后台系统页：`web/src/views/admin/system/*.vue`
- 门户页：`web/src/views/web/*`

## 6. 二次开发
- 新增请求必须复用统一 `http` 实例。
- 新增系统列表页应尽量对齐既有 `DataTable` 风格。
- 新增插件页时应优先通过插件宿主注入，而不是直接改主框架菜单结构。

## 7. 关键入口
- HTTP：`web/src/api/http.ts`
- 系统 API：`web/src/api`
- 后台系统页：`web/src/views/admin/system`
- 门户页：`web/src/views/web`
- 插件宿主：`web/src/plugins/core`
- 状态：`web/src/stores`

## 8. 注意事项
- 后台与门户令牌键不同（`auth-token` / `web_user_token`），不要混用。
- 页面功能必须与后端接口闭环，不要只做静态页面。
- Web 插件采用「按路径懒加载 + `loadPolicy: "always"` 横切兜底 + 门户共享容器兜底」的三轨装载：
  - 注册 `http:biz-error`、`slot:login-actions` / `slot:admin-login-actions`、`portal:standalone-route`、`auth:login`/`auth:logout` 等需要在静态登录页/未登录态生效的钩子，必须在该插件 `module.json` 中显式声明 `"loadPolicy": "always"`，否则进入 `/web/login`、`/admin/login` 时钩子根本不会被装载。
  - 注册 `portal:user-menu` 等"门户共享容器插槽"钩子时，无需 `loadPolicy: "always"`，但承担该容器的静态路由（如会员中心 `/web/user*`）必须在 `meta` 中带 `portalShellNeedsPlugins: true`，由 `router/index.ts` 的 `ensurePortalPluginsLoaded()` 触发首次门户全量兜底装载。
  - 详见 [插件开发规范与流程](./插件开发规范与流程.md) 第 9 节。
