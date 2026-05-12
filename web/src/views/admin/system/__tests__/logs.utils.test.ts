import { describe, expect, it } from 'vitest'
import {
  buildLogFilterOptions,
  buildLogPreviewText,
  getLogResultMeta,
  type SystemLogRow
} from '../logs.utils'

const rows: SystemLogRow[] = [
  {
    id: '1',
    action: 'GET',
    resource: '/api/v1/settings',
    moduleCN: '系统配置',
    featureCN: '获取公开配置',
    result: 'OK',
    reviewCN: '系统配置-获取公开配置-成功',
    createdAt: '2026-04-10 15:35:27'
  },
  {
    id: '2',
    action: 'POST',
    resource: '/api/v1/users',
    moduleCN: '用户管理',
    featureCN: '新增用户',
    result: 'ERROR',
    reviewCN: '用户管理-新增用户-失败',
    dataJson: '{"message":"邮箱重复"}',
    createdAt: '2026-04-10 15:36:01'
  },
  {
    id: '3',
    action: 'GET',
    resource: '/api/v1/settings/all',
    moduleCN: '系统配置',
    featureCN: '获取全部配置(管理端)',
    result: 'Ok',
    reviewCN: '系统配置-获取全部配置(管理端)-成功',
    createdAt: '2026-04-10 15:36:32'
  }
]

describe('system logs utils', () => {
  it('为正常和错误结果返回不同的颜色语义', () => {
    expect(getLogResultMeta('OK')).toMatchObject({
      label: '正常',
      tagType: 'success',
      dotClass: 'is-success'
    })

    expect(getLogResultMeta('ERROR')).toMatchObject({
      label: '错误',
      tagType: 'danger',
      dotClass: 'is-danger'
    })
  })

  it('根据当前日志构建功能和类型筛选项', () => {
    const options = buildLogFilterOptions(rows)

    expect([...options.features.map(item => item.value)].sort()).toEqual([
      '获取全部配置(管理端)',
      '获取公开配置',
      '新增用户'
    ].sort())

    expect(options.types.map(item => item.value)).toEqual(['normal', 'error'])
  })

  it('优先展示中文审计摘要，其次回退到接口和附加信息', () => {
    expect(buildLogPreviewText(rows[0])).toBe('系统配置-获取公开配置-成功')
    expect(buildLogPreviewText(rows[1])).toContain('邮箱重复')
  })
})
