import http from './http'

// 模块信息接口
export interface ModuleInfo {
  id: string
  name: string
  version: string
  enabled: boolean
  hasClient: boolean
  publisher?: string
  homepage?: string
  author?: string
  title?: string
  minAppVersion?: string
  dependencies?: Record<string, string>
  hasPages?: boolean
  installedAtUtc?: string
  createdAt?: string
  updatedAt?: string
  createdBy?: string
  updatedBy?: string
  isDevMode?: boolean
  manifestPath?: string
  testRoute?: string
  // 运行时健康快照（由后端 GetInstalled 端点一次性填充，无需前端再逐条调 status 接口）
  runtimeLoaded?: boolean
  serverDllLoaded?: boolean
  hasMenus?: boolean
  menuRegistered?: boolean
  /** 配置存储方式：file | database */
  configStorage?: 'file' | 'database'
  /** 数据库存储模式主配置文件名 */
  configPrimaryFile?: string
}

// 模块状态接口
export interface ModuleStatus {
  moduleId: string
  version: string
  enabled: boolean
  runtimeLoaded: boolean
  serverDllLoaded: boolean
  serverConfigOk: boolean
  clientPresent: boolean
  clientStatus: string
  clientLastReportAtUtc?: string
  menuRegistered: boolean
  /** install.json 是否声明了菜单（Menus.RootCode 非空）。前端据此决定要不要展示「菜单注册」状态。 */
  hasMenus: boolean
  hasErrors: boolean
  isDevMode: boolean
  clientExpected: boolean
  clientEntryAssembly?: string
}

// 安装结果接口
export interface InstallResult {
  ok: boolean
  message: string
  moduleId?: string
  version?: string
  executedSteps?: string[]
  rollbackSteps?: string[]
}

// 上传验证结果接口
export interface UploadValidationResult {
  ok: boolean
  message: string
  moduleId?: string
  moduleName?: string
  version?: string
  hasClient?: boolean
  publisher?: string
  extractedPath?: string
  hasSqlScripts?: boolean
  hasMenus?: boolean
  security?: {
    hashValid: boolean
    signatureValid: boolean
    signaturePublisher?: string
    capabilities?: string[]
    warnings?: string[]
  }
}

// 打包结果接口
export interface PackageResult {
  ok: boolean
  message: string
  fileName?: string
  fileSize?: number
  packageType?: string
  includedFiles?: number
  steps?: string[]
  downloadUrl?: string
  localPath?: string
}

// 可打包模块接口
export interface PackageableModule {
  moduleId: string
  name: string
  version: string
  path: string
  hasServer?: boolean
  hasWeb?: boolean
  hasClient?: boolean
  /** 是否为源码版模块（server 目录下存在 .csproj）。false 表示已编译 DLL 版，不能再打源码包 */
  isSourcePackage?: boolean
}

// 环境信息接口
export interface EnvironmentInfo {
  ok: boolean
  environment: string
  isDevelopment: boolean
  description: string
}

// 获取已启用模块的ID列表（无需认证，供插件系统过滤使用）
export async function getEnabledPlugins(): Promise<string[]> {
  const resp = await fetch('/api/v1/modules/enabled-plugins')
  if (!resp.ok) {
    throw new Error(`Failed to load enabled plugins: ${resp.status}`)
  }
  return await resp.json()
}

// 获取已安装模块列表
export async function getInstalledModules(): Promise<ModuleInfo[]> {
  return http.get('/v1/modules/installed')
}

// 获取模块状态
export async function getModuleStatus(moduleId: string): Promise<ModuleStatus> {
  return http.get('/v1/modules/status', { params: { moduleId } })
}

// 启用模块
export async function enableModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/enable', { moduleId })
}

// 禁用模块
export async function disableModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/disable', { moduleId })
}

// 卸载模块
export async function uninstallModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/uninstall', { moduleId })
}

// 热启用模块
export async function hotEnableModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/hot/enable', { moduleId })
}

// 热禁用模块
export async function hotDisableModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/hot/disable', { moduleId })
}

// 热重载模块
export async function hotReloadModule(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/hot/reload', { moduleId })
}

// 热卸载模块
export interface UninstallResult {
  ok: boolean
  message: string
  pendingDeleteDirs?: string[]
  hasPendingDelete?: boolean
}

export async function hotUninstallModule(moduleId: string): Promise<UninstallResult> {
  return http.post('/v1/modules/hot/uninstall', { moduleId })
}

// 检查模块是否有待删除目录
export interface PendingDeleteCheckResult {
  hasPendingDelete: boolean
  pendingDirs: string[]
  message?: string
}

export async function checkPendingDelete(moduleId: string): Promise<PendingDeleteCheckResult> {
  return http.get(`/v1/modules/pending-delete/${encodeURIComponent(moduleId)}`)
}

// 上传模块包
export async function uploadModule(file: File, onProgress?: (percent: number) => void): Promise<UploadValidationResult> {
  const formData = new FormData()
  formData.append('file', file)
  
  return http.post('/v1/modules/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (e) => {
      if (onProgress && e.total) {
        onProgress(Math.round((e.loaded * 100) / e.total))
      }
    }
  })
}

// 上传并安装模块（一步完成）
export async function uploadAndInstallModule(file: File, onProgress?: (percent: number) => void): Promise<InstallResult> {
  const formData = new FormData()
  formData.append('file', file)
  
  return http.post('/v1/modules/upload-install', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (e) => {
      if (onProgress && e.total) {
        onProgress(Math.round((e.loaded * 100) / e.total))
      }
    }
  })
}

// 确认安装已上传的模块
export async function confirmInstallModule(extractedPath: string): Promise<InstallResult> {
  return http.post('/v1/modules/confirm-install', { extractedPath })
}

// 打包模块
// exportDbSchema：从真实数据库导出表结构覆盖 install.sql
// exportDbData：在导出结构基础上再导出每表最多 100 行真实数据
// sanitizeConfig：是否对插件配置文件做脱敏处理（清空 items[].value 真实值），默认 true
export async function packageModule(
  moduleId: string,
  packageType: string = 'source',
  exportDbSchema: boolean = false,
  exportDbData: boolean = false,
  sanitizeConfig: boolean = true,
  exportClientMenus: boolean = false,
  exportDictionary: boolean = false
): Promise<PackageResult> {
  return http.post('/v1/modules/package', { moduleId, packageType, exportDbSchema, exportDbData, sanitizeConfig, exportClientMenus, exportDictionary })
}

// 获取可打包的模块列表
export async function getPackageableModules(): Promise<{ ok: boolean; modules: PackageableModule[] }> {
  return http.get('/v1/modules/packageable')
}

// 获取环境信息
export async function getEnvironmentInfo(): Promise<EnvironmentInfo> {
  return http.get('/v1/modules/environment')
}

/**
 * 开发模式专用：触发整个 API 进程的自重启，用于让 ALC 重新扫描 modules 目录、加载新插件 DLL。
 * 后端会在 800ms 后停掉自身，浏览器侧需要轮询 getEnvironmentInfo() 直到响应正常即视为重启完成。
 *
 * 返回值字段：
 * - restarting: 总是 true，表示已进入重启流程
 * - autoRelaunch: 是否成功调度了自拉起子进程；若为 false，需要外部守护（IIS / systemd / 手动）拉起
 * - message: 调度失败时的错误信息
 */
export async function restartApiProcess(): Promise<{ restarting: boolean; autoRelaunch: boolean; message?: string }> {
  return http.post('/v1/modules/restart-process')
}

// 获取模块包下载URL
export function getPackageDownloadUrl(
  moduleId: string,
  packageType: string = 'source',
  exportDbSchema: boolean = false,
  exportDbData: boolean = false,
  exportClientMenus: boolean = false,
  exportDictionary: boolean = false
): string {
  return `/api/v1/modules/package/download?moduleId=${encodeURIComponent(moduleId)}&packageType=${packageType}&exportDbSchema=${exportDbSchema}&exportDbData=${exportDbData}&exportClientMenus=${exportClientMenus}&exportDictionary=${exportDictionary}`
}

// 下载模块包（带认证）
// 必须与 packageModule 调用时保持相同的 exportDbSchema / exportDbData / sanitizeConfig，
// 否则后端会按默认值重新打包并覆盖先前生成的 ZIP。
export async function downloadPackage(
  moduleId: string,
  packageType: string = 'source',
  exportDbSchema: boolean = false,
  exportDbData: boolean = false,
  sanitizeConfig: boolean = true,
  exportClientMenus: boolean = false,
  exportDictionary: boolean = false
): Promise<ArrayBuffer> {
  const qs = `moduleId=${encodeURIComponent(moduleId)}&packageType=${encodeURIComponent(packageType)}&exportDbSchema=${exportDbSchema}&exportDbData=${exportDbData}&sanitizeConfig=${sanitizeConfig}&exportClientMenus=${exportClientMenus}&exportDictionary=${exportDictionary}`
  return http.get(`/v1/modules/package/download?${qs}`, {
    responseType: 'arraybuffer'
  })
}


// ========== 模块配置相关接口 ==========

// 配置分组
export interface ConfigGroup {
  code: string
  title: string
  desc?: string
  applyUrl?: string
}

// 配置项
export interface ConfigItem {
  group: string
  name: string
  title: string
  type: 'text' | 'password' | 'radio' | 'select' | 'textarea' | 'link' | 'file' | 'api-selector'
  content: Record<string, string>
  value: string | null
  rule?: string | null
  msg?: string | null
  tip?: string | null
  ok?: string | null
  extend?: string | null
}

// 标准化配置响应
export interface NormalizedConfig {
  groups: ConfigGroup[]
  items: ConfigItem[]
  /** 配置存储方式（后端 normalized 接口返回） */
  storage?: 'file' | 'database'
}

/** 配置项差异 */
export interface ConfigItemDiff {
  name: string
  sampleValue?: string | null
  dbValue?: string | null
}

/** 配置存储状态（数据库模式含一致性检查） */
export interface ModuleConfigStorageStatus {
  ok: boolean
  storage: 'file' | 'database'
  file?: string
  sampleExists?: boolean
  sampleItemCount?: number
  dbItemCount?: number
  hasDbData?: boolean
  isConsistent?: boolean
  missingInDb?: ConfigItemDiff[]
  extraInDb?: ConfigItemDiff[]
  valueMismatch?: ConfigItemDiff[]
  message?: string
}

// 获取模块配置文件列表
export async function getModuleConfigFiles(moduleId: string): Promise<string[]> {
  return http.get('/v1/modules/config/files', { params: { moduleId } })
}

// 获取标准化配置（用于 UI 渲染）
export async function getModuleConfigNormalized(moduleId: string, file: string): Promise<NormalizedConfig> {
  return http.get('/v1/modules/config/normalized', { params: { moduleId, file } })
}

// 保存配置并热重载
export async function saveModuleConfig(moduleId: string, file: string, content: NormalizedConfig): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/config/save-and-reload', { moduleId, file, content })
}

// 重置配置为默认值
export async function resetModuleConfig(moduleId: string, file: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/config/reset', { moduleId, file })
}

// 检查配置存储方式及数据库与样例一致性
export async function getModuleConfigStorageStatus(moduleId: string, file: string): Promise<ModuleConfigStorageStatus> {
  return http.get('/v1/modules/config/storage-status', { params: { moduleId, file } })
}

// 将样例初始配置同步到数据库
export async function syncModuleConfigToDb(moduleId: string, file: string): Promise<{ ok: boolean; message: string; syncedCount?: number; removedExtraCount?: number; isConsistent?: boolean }> {
  return http.post('/v1/modules/config/sync-to-db', { moduleId, file })
}

// 从数据库移除指定配置文件的全部配置项
export async function removeModuleConfigFromDb(moduleId: string, file: string): Promise<{ ok: boolean; message: string; removedCount?: number }> {
  return http.post('/v1/modules/config/remove-from-db', { moduleId, file })
}

// 重置模块菜单
export async function resetModuleMenus(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/reset-menus', { moduleId })
}

// 移除模块菜单（仅删除，不重建）
export async function removeModuleMenus(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/remove-menus', { moduleId })
}

// 执行模块安装 SQL 脚本
export async function runInstallSql(moduleId: string): Promise<{ ok: boolean; message: string; executedScripts?: string[] }> {
  return http.post('/v1/modules/run-install-sql', { moduleId })
}

// ========== 前端 NPM 依赖管理 ==========

// npm 依赖项
export interface NpmDepInfo {
  name: string
  requiredVersion: string
  description: string
  required: boolean
  installed: boolean
  installedVersion: string | null
}

// 查询模块的前端 npm 依赖列表
export async function getNpmDeps(moduleId: string): Promise<{ ok: boolean; deps: NpmDepInfo[]; message?: string; pluginDir?: string }> {
  return http.get('/v1/modules/npm-deps', { params: { moduleId } })
}

// 安装模块的前端 npm 依赖
export async function installNpmDeps(moduleId: string): Promise<{ ok: boolean; message: string; installed: string[]; errors?: string[] }> {
  return http.post('/v1/modules/install-npm-deps', { moduleId })
}

// ========== 供应链安全相关接口 ==========

// 能力信息
export interface CapabilityInfo {
  id: string
  name: string
  isKnown: boolean
  riskLevel: string
}

// 灰度策略
export interface GrayscalePolicy {
  channel: string
  targetTenantIds?: string[]
  startTime?: string
  endTime?: string
  autoPromote: boolean
  createdBy?: string
  createdAt?: string
}

// 快照元数据
export interface SnapshotMetadata {
  moduleId: string
  version: string
  createdAt: string
  createdBy?: string
  snapshotType: string
  fileSizeBytes: number
}

// 审计日志
export interface AuditLogEntry {
  id: string
  moduleId: string
  action: string
  level: string
  createdAtUtc: string
  message?: string
  detailsJson?: string
}

// 获取安全配置状态
export async function getSecurityStatus(): Promise<any> {
  return http.get('/v1/modules/security-status')
}

// SQL Dry-Run 预检
export async function dryRunModule(moduleId: string): Promise<{ ok: boolean; message: string; errors: string[] }> {
  return http.post('/v1/modules/dry-run', { moduleId })
}

// 获取模块能力声明
export async function getModuleCapabilities(moduleId: string): Promise<{ ok: boolean; moduleId: string; capabilities: CapabilityInfo[] }> {
  return http.get(`/v1/modules/capabilities/${encodeURIComponent(moduleId)}`)
}

// 设置灰度发布策略
export async function setGrayscalePolicy(moduleId: string, policy: Partial<GrayscalePolicy>): Promise<{ ok: boolean; message: string }> {
  return http.post('/v1/modules/grayscale', { moduleId, ...policy })
}

// 获取所有灰度策略
export async function getGrayscalePolicies(): Promise<{ ok: boolean; policies: Record<string, GrayscalePolicy> }> {
  return http.get('/v1/modules/grayscale')
}

// 移除灰度策略
export async function removeGrayscalePolicy(moduleId: string): Promise<{ ok: boolean; message: string }> {
  return http.delete(`/v1/modules/grayscale/${encodeURIComponent(moduleId)}`)
}

// 获取模块快照列表
export async function getModuleSnapshots(moduleId: string): Promise<{ ok: boolean; moduleId: string; snapshots: SnapshotMetadata[] }> {
  return http.get(`/v1/modules/snapshots/${encodeURIComponent(moduleId)}`)
}

// 从快照回滚模块
export async function rollbackModule(moduleId: string, snapshotVersion: string): Promise<{ ok: boolean; message: string }> {
  return http.post(`/v1/modules/rollback/${encodeURIComponent(moduleId)}`, { snapshotVersion })
}

// 获取模块审计日志
export async function getModuleAuditLog(params: { moduleId?: string; page?: number; pageSize?: number }): Promise<{ ok: boolean; items: AuditLogEntry[]; total: number; page: number; pageSize: number }> {
  return http.get('/v1/modules/audit-log', { params })
}


