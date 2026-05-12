import http from './http'

// 定时任务列表项
export interface ScheduledTaskItem {
	id: string
	taskKey: string
	displayName: string
	group: string | null
	cronExpression: string
	isEnabled: boolean
	lastRunAt: string | null
	nextRunAt: string | null
	lastResult: string | null
	lastElapsedMs: number | null
	description: string | null
	executionType: string | null
	executionTarget: string | null
	definitionType: string | null
	executionSource: string | null
	actionKey: string | null
	configJson: string | null
	source: string | null
	createdAt: string
	updatedAt: string | null
}

// 更新任务配置
export interface UpdateScheduledTaskInput {
	isEnabled: boolean
	cronExpression: string
	description?: string | null
}

// 执行日志列表项
export interface ScheduledTaskLogItem {
	id: string
	taskKey: string
	startedAt: string
	finishedAt: string | null
	success: boolean
	errorMessage: string | null
	elapsedMs: number | null
	triggerType: string | null
	detailsJson: string | null
}

// 创建动态任务输入
export interface CreateDynamicTaskInput {
	displayName: string
	group?: string | null
	cronExpression: string
	description?: string | null
	isEnabled: boolean
	executionSource: string
	configJson: string
}

// 执行提供器信息
export interface ExecutionProviderInfo {
	sourceKey: string
	displayName: string
	icon: string | null
	description: string | null
	order: number
	supportsTest: boolean
	formDefinition: {
		fields: ExecutionFormField[]
	}
}

// 表单字段定义
export interface ExecutionFormField {
	name: string
	label: string
	type: string
	required: boolean
	defaultValue: any
	placeholder: string | null
	description: string | null
	options: { label: string; value: string }[] | null
	dependsOn: string | null
	minValue: number | null
	maxValue: number | null
	rows: number | null
	multiple: boolean
}

// 可调用动作信息
export interface InvocableActionInfo {
	actionKey: string
	displayName: string
	category: string
	description: string | null
	source: string | null
	parameters: any[] | null
}

// 执行结果
export interface ActionExecutionResultDto {
	success: boolean
	message: string | null
	data: any
}

// 获取所有定时任务
export const getScheduledTasks = () =>
	http.get<{ items: ScheduledTaskItem[]; total: number }>('/v1/scheduled-tasks')

// 获取任务详情
export const getScheduledTaskByKey = (taskKey: string) =>
	http.get<ScheduledTaskItem>(`/v1/scheduled-tasks/${encodeURIComponent(taskKey)}`)

// 更新任务配置
export const updateScheduledTask = (taskKey: string, data: UpdateScheduledTaskInput) =>
	http.put(`/v1/scheduled-tasks/${encodeURIComponent(taskKey)}`, data)

// 手动触发任务
export const triggerScheduledTask = (taskKey: string) =>
	http.post(`/v1/scheduled-tasks/${encodeURIComponent(taskKey)}/trigger`)

// 获取任务执行日志
export const getScheduledTaskLogs = (taskKey: string, page = 1, pageSize = 20) =>
	http.get<{ items: ScheduledTaskLogItem[]; total: number }>(`/v1/scheduled-tasks/${encodeURIComponent(taskKey)}/logs`, { params: { page, pageSize } })

// 创建动态任务
export const createDynamicTask = (data: CreateDynamicTaskInput) =>
	http.post<ScheduledTaskItem>('/v1/scheduled-tasks', data)

// 删除动态任务
export const deleteDynamicTask = (taskKey: string) =>
	http.delete(`/v1/scheduled-tasks/${encodeURIComponent(taskKey)}`)

// 获取所有执行提供器
export const getExecutionProviders = () =>
	http.get<{ items: ExecutionProviderInfo[] }>('/v1/scheduled-tasks/execution-providers')

// 获取所有可调用动作
export const getInvocableActions = () =>
	http.get<{ items: InvocableActionInfo[] }>('/v1/scheduled-tasks/invocable-actions')

// 测试执行
export const testExecute = (executionSource: string, configJson: string) =>
	http.post<ActionExecutionResultDto>('/v1/scheduled-tasks/test-execute', { executionSource, configJson })
