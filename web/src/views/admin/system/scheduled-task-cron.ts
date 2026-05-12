export interface CronFormValue {
  mode: 'daily' | 'weekly' | 'monthly' | 'interval'
  hour: number
  minute: number
  weekDay: number
  monthDay: number
  intervalMinutes: number
}

export function parseCronToForm(cron: string | null | undefined): CronFormValue {
  const text = String(cron || '').trim()
  const parts = text.split(/\s+/)
  if (parts.length !== 5) {
    return { mode: 'daily', hour: 3, minute: 0, weekDay: 1, monthDay: 1, intervalMinutes: 60 }
  }

  const [minute, hour, day, month, week] = parts
  if (/^\*\/\d+$/.test(minute) && hour === '*' && day === '*' && month === '*' && week === '*') {
    return {
      mode: 'interval',
      hour: 0,
      minute: 0,
      weekDay: 1,
      monthDay: 1,
      intervalMinutes: Number(minute.replace('*/', '')) || 60
    }
  }

  if (/^\d+$/.test(minute) && /^\d+$/.test(hour) && day === '*' && month === '*' && week === '*') {
    return {
      mode: 'daily',
      hour: Number(hour),
      minute: Number(minute),
      weekDay: 1,
      monthDay: 1,
      intervalMinutes: 60
    }
  }

  if (/^\d+$/.test(minute) && /^\d+$/.test(hour) && day === '*' && month === '*' && /^\d+$/.test(week)) {
    return {
      mode: 'weekly',
      hour: Number(hour),
      minute: Number(minute),
      weekDay: Number(week),
      monthDay: 1,
      intervalMinutes: 60
    }
  }

  if (/^\d+$/.test(minute) && /^\d+$/.test(hour) && /^\d+$/.test(day) && month === '*' && week === '*') {
    return {
      mode: 'monthly',
      hour: Number(hour),
      minute: Number(minute),
      weekDay: 1,
      monthDay: Number(day),
      intervalMinutes: 60
    }
  }

  return { mode: 'daily', hour: 3, minute: 0, weekDay: 1, monthDay: 1, intervalMinutes: 60 }
}

export function buildCronFromForm(form: CronFormValue): string {
  const minute = clamp(form.minute, 0, 59)
  const hour = clamp(form.hour, 0, 23)
  const weekDay = clamp(form.weekDay, 0, 6)
  const monthDay = clamp(form.monthDay, 1, 31)
  const intervalMinutes = clamp(form.intervalMinutes, 1, 1440)

  switch (form.mode) {
    case 'interval':
      return `*/${intervalMinutes} * * * *`
    case 'weekly':
      return `${minute} ${hour} * * ${weekDay}`
    case 'monthly':
      return `${minute} ${hour} ${monthDay} * *`
    case 'daily':
    default:
      return `${minute} ${hour} * * *`
  }
}

export function cronToHuman(cron: string | null | undefined): string {
  const form = parseCronToForm(cron)
  const hh = String(form.hour).padStart(2, '0')
  const mm = String(form.minute).padStart(2, '0')
  const weekMap = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']
  switch (form.mode) {
    case 'interval':
      return `每 ${form.intervalMinutes} 分钟执行一次`
    case 'weekly':
      return `每周 ${weekMap[form.weekDay] || '周一'} ${hh}:${mm} 执行`
    case 'monthly':
      return `每月 ${form.monthDay} 日 ${hh}:${mm} 执行`
    case 'daily':
    default:
      return `每天 ${hh}:${mm} 执行`
  }
}

function clamp(num: number, min: number, max: number) {
  if (Number.isNaN(num)) return min
  return Math.min(max, Math.max(min, Number(num)))
}
