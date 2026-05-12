import { defineStore } from 'pinia'
import { getPublicConfig } from '../api/system'

const PUBLIC_CONFIG_CACHE_KEY = 'system-public-config-cache'

type PublicConfigCachePayload = {
	timestamp: number
	data: PublicSystemConfig
}

export interface PublicSystemConfig {
	siteName: string
	logoUrl: string
	maintenanceMode: boolean
	loginSubtitle?: string
	primaryColor?: string
	secondaryColor?: string
	loginBackground?: string
	animationEnabled?: boolean
	animationIntensity?: 'light' | 'medium' | 'strong'
	favicon?: string
	welcomeText?: string
	footerText?: string
	registrationEnabled?: boolean
	registrationMode?: string
	loginMethods?: string[]
	loginCaptchaEnabled?: boolean
	// 通知音频配置
	notificationAudioUrl?: string
	notificationAudioEnabled?: boolean
	// 备案与 SEO
	icpNumber?: string
	policeNumber?: string
	businessLicense?: string
	seoKeywords?: string
	seoDescription?: string
}

export const useSystemStore = defineStore('system', {
	state: () => ({
		loaded: false as boolean,
		siteName: '' as string,
		logoUrl: '' as string,
		maintenanceMode: false as boolean,
		loginSubtitle: '' as string,
		primaryColor: '#3b82f6' as string,
		secondaryColor: '#2563eb' as string,
		loginBackground: '' as string,
		animationEnabled: true as boolean,
		animationIntensity: 'medium' as 'light' | 'medium' | 'strong',
		favicon: '' as string,
		welcomeText: '' as string,
		footerText: '' as string,
		registrationEnabled: true as boolean,
		registrationMode: 'free' as string,
		loginMethods: ['password'] as string[],
		loginCaptchaEnabled: true as boolean,
		// 通知音频配置
		notificationAudioUrl: '' as string,
		notificationAudioEnabled: true as boolean,
		// 备案与 SEO
		icpNumber: '' as string,
		policeNumber: '' as string,
		businessLicense: '' as string,
		seoKeywords: '' as string,
		seoDescription: '' as string,
	}),
	actions: {
		applyPublicConfig(cfg: PublicSystemConfig): void {
			this.siteName = cfg?.siteName || this.siteName
			this.logoUrl = cfg?.logoUrl || this.logoUrl
			this.maintenanceMode = !!cfg?.maintenanceMode
			this.loginSubtitle = cfg?.loginSubtitle || this.loginSubtitle
			this.primaryColor = cfg?.primaryColor || this.primaryColor
			this.secondaryColor = cfg?.secondaryColor || this.secondaryColor
			this.loginBackground = cfg?.loginBackground || this.loginBackground
			this.animationEnabled = cfg?.animationEnabled ?? this.animationEnabled
			this.animationIntensity = cfg?.animationIntensity || this.animationIntensity
			this.favicon = cfg?.favicon || this.favicon
			this.welcomeText = cfg?.welcomeText || this.welcomeText
			this.footerText = cfg?.footerText || this.footerText
			this.registrationEnabled = cfg?.registrationEnabled ?? this.registrationEnabled
			this.registrationMode = cfg?.registrationMode || this.registrationMode
			this.loginMethods = cfg?.loginMethods || this.loginMethods
			this.loginCaptchaEnabled = cfg?.loginCaptchaEnabled ?? this.loginCaptchaEnabled
			// 通知音频配置
			this.notificationAudioUrl = cfg?.notificationAudioUrl || this.notificationAudioUrl
			this.notificationAudioEnabled = cfg?.notificationAudioEnabled ?? this.notificationAudioEnabled
			// 备案与 SEO
			this.icpNumber = cfg?.icpNumber || this.icpNumber
			this.policeNumber = cfg?.policeNumber || this.policeNumber
			this.businessLicense = cfg?.businessLicense || this.businessLicense
			this.seoKeywords = cfg?.seoKeywords || this.seoKeywords
			this.seoDescription = cfg?.seoDescription || this.seoDescription
		},
		readPublicConfigCache(): PublicSystemConfig | null {
			try {
				const raw = localStorage.getItem(PUBLIC_CONFIG_CACHE_KEY)
				if (!raw) return null
				const parsed = JSON.parse(raw) as PublicConfigCachePayload
				if (!parsed || !parsed.data) return null
				return parsed.data
			} catch {
				return null
			}
		},
		writePublicConfigCache(cfg: PublicSystemConfig): void {
			try {
				const payload: PublicConfigCachePayload = {
					timestamp: Date.now(),
					data: cfg
				}
				localStorage.setItem(PUBLIC_CONFIG_CACHE_KEY, JSON.stringify(payload))
			} catch {
				// 静默失败，不影响主流程
			}
		},
		async loadPublicConfig(): Promise<void> {
			const cached = this.readPublicConfigCache()
			if (cached) {
				this.applyPublicConfig(cached)
				this.loaded = true
			}

			try {
				const cfg: PublicSystemConfig = await getPublicConfig()
				this.applyPublicConfig(cfg)
				this.writePublicConfigCache(cfg)
			} catch (_) {
				// 静默失败，使用默认值
			} finally {
				this.loaded = true
			}
		},
	},
})



