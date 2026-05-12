import http from './http'
import type { PublicSystemConfig } from '../stores/system'
import { resolveResourcePath, fetchResourceConfig } from '../utils/resourceUrl'

// 系统配置项 DTO
export interface SettingDto {
	key: string
	value?: string
	type?: string
	description?: string
	class?: string
	version?: number
}

// 字典分类 DTO
export interface DictionaryCategoryDto {
	id: string
	code: string
	name: string
	enabled: boolean
	category?: string
	sourceType?: string
	description?: string
}

// 字典条目 DTO
export interface DictionaryItemDto {
	id: string
	categoryId: string
	itemKey: string
	itemValue: string
	order?: number
	enabled?: boolean
	parentId?: string
}

// 分页结果
export interface PagedResult<T> {
	total: number
	page: number
	pageSize: number
	items: T[]
}

// 将后端 /api/v1/settings 返回的白名单配置映射到前端结构
export async function getPublicConfig(): Promise<PublicSystemConfig> {
	// 预加载 OSS 资源配置，确保 resolveFileUrl 可以正确拼接 OSS 地址
	await fetchResourceConfig().catch(() => {})
	const list = await http.get<any, Array<{ key: string; value?: string }>>('/v1/settings')
	const map = new Map<string, string>((list || []).map(it => [it.key, it.value ?? '']))
	return {
		siteName: map.get('Site.Name') || '',
		logoUrl: resolveResourcePath(map.get('Site.Logo') || ''),
		maintenanceMode: (map.get('Site.Maintenance.Enabled') || map.get('Site.Maintenance.Enable') || '').toLowerCase() === 'true',
		loginSubtitle: map.get('Site.Subtitle') || '',
		primaryColor: map.get('Site.Theme.PrimaryColor') || '#3b82f6',
		secondaryColor: map.get('Site.Theme.SecondaryColor') || '#2563eb',
		loginBackground: resolveResourcePath(map.get('Site.Login.LeftPanelBackground') || ''),
		animationEnabled: (map.get('Site.Animation.Enabled') || 'true').toLowerCase() === 'true',
		animationIntensity: (map.get('Site.Animation.Intensity') || 'medium') as 'light' | 'medium' | 'strong',
		favicon: resolveResourcePath(map.get('Site.Branding.Favicon') || ''),
		welcomeText: map.get('Site.Login.WelcomeText') || '',
		footerText: map.get('Site.Footer.Text') || '',
		// registrationEnabled 直接从 registrationMode 推导，避免两个字段初始值不一致导致的逻辑冲突
		registrationMode: map.get('Registration.Mode') || 'free',
		registrationEnabled: (map.get('Registration.Mode') || 'free') !== 'disabled',
		loginMethods: (() => { try { return JSON.parse(map.get('Registration.LoginMethods') || '["password"]') } catch { return ['password'] } })(),
		loginCaptchaEnabled: (map.get('Registration.LoginCaptcha') || 'true').toLowerCase() === 'true',
		// 通知音频配置
		notificationAudioUrl: resolveResourcePath(map.get('Notification.Audio.Url') || ''),
		notificationAudioEnabled: (map.get('Notification.Audio.Enabled') || 'true').toLowerCase() === 'true',
		// 备案与 SEO（与后台 SystemConfig.vue 保存的 Key 严格一致）
		// 兼容历史 Key（Site.ICP.Number / Site.ICP.PoliceNumber）作为兜底
		icpNumber: map.get('Site.ICP') || map.get('Site.ICP.Number') || '',
		policeNumber: map.get('Site.PoliceICP') || map.get('Site.ICP.PoliceNumber') || '',
		businessLicense: map.get('Site.BusinessLicense') || map.get('Site.ICP.BusinessLicense') || '',
		seoKeywords: map.get('Site.SEO.Keywords') || '',
		seoDescription: map.get('Site.SEO.Description') || '',
	}
}

// 获取所有系统配置（管理端使用，需要授权）
export async function getAllSettings(): Promise<SettingDto[]> {
	return await http.get<any, SettingDto[]>('/v1/settings/all')
}

// 批量保存系统配置
export async function saveBatchSettings(settings: SettingDto[]): Promise<void> {
	await http.post('/v1/settings/batch', settings)
}

// 新增或更新单个配置
export async function upsertSetting(setting: SettingDto): Promise<void> {
	await http.post('/v1/settings', setting)
}

// 删除配置（如果后端支持）
export async function deleteSetting(key: string, className?: string): Promise<void> {
	await http.delete('/v1/settings', { params: { key, class: className } })
}

// 发送测试邮件
export async function sendTestEmail(to: string): Promise<void> {
	await http.post('/v1/settings/test-email', { to })
}

// 获取字典分类列表
export async function getDictionaryCategories(page: number = 1, pageSize: number = 200, keyword?: string): Promise<PagedResult<DictionaryCategoryDto>> {
	return await http.get<any, PagedResult<DictionaryCategoryDto>>('/v1/dictionaries/categories', {
		params: { page, pageSize, keyword }
	})
}

// 获取字典条目列表
export async function getDictionaryItems(categoryId: string, page: number = 1, pageSize: number = 2000): Promise<PagedResult<DictionaryItemDto>> {
	return await http.get<any, PagedResult<DictionaryItemDto>>('/v1/dictionaries/items', {
		params: { categoryId, page, pageSize }
	})
}

// 保存单个系统配置
export async function saveSetting(setting: SettingDto): Promise<void> {
	await http.post('/v1/settings', setting)
}

// 系统操作日志
export interface AdminOpLogItem {
	id: string
	userId?: string
	action: string
	resource: string
	ip?: string
	userAgent?: string
	result?: string
	elapsedMs?: number
	dataJson?: string
	moduleCN?: string
	featureCN?: string
	reviewCN?: string
	createdAt: string
	userName?: string | null
	displayName?: string | null
	email?: string | null
	phone?: string | null
}

export interface AdminLogFilter {
	module?: string
	feature?: string
	type?: 'normal' | 'error' | 'unknown' | string
	keyword?: string
	dateRange?: [string, string]
}

export async function getOpLogs(
	page: number = 1,
	pageSize: number = 20,
	filter?: AdminLogFilter,
	userId?: string
): Promise<PagedResult<AdminOpLogItem>> {
	const params: Record<string, any> = { page, pageSize }
	if (userId) {
		params.userId = userId
	}
	if (filter) {
		const payload: Record<string, any> = {
			module: (filter.module || '').trim() || undefined,
			feature: (filter.feature || '').trim() || undefined,
			type: (filter.type || '').trim() || undefined,
			keyword: (filter.keyword || '').trim() || undefined,
			dateRange:
				Array.isArray(filter.dateRange) &&
				filter.dateRange.length === 2 &&
				filter.dateRange[0] &&
				filter.dateRange[1]
					? [filter.dateRange[0], filter.dateRange[1]]
					: undefined
		}
		params.filter = JSON.stringify(payload)
	}
	return await http.get<any, PagedResult<AdminOpLogItem>>('/v1/logs', { params })
}

