import { describe, expect, it } from 'vitest'
import {
  buildMediaInsertHtml,
  isSafeMediaUrl,
  normalizeMediaItems,
} from './editorMediaInsert'

describe('editorMediaInsert', () => {
  it('将多张图片转换为连续图片 HTML，并转义替代文本', () => {
    const html = buildMediaInsertHtml('image', [
      { url: 'https://cdn.example.com/a.png', alt: '封面 <主图>' },
      { url: '/uploads/2026/04/b.png', name: 'b.png' },
    ])

    expect(html).toContain('<img src="https://cdn.example.com/a.png" alt="封面 &lt;主图&gt;" style="max-width:100%;" />')
    expect(html).toContain('<img src="/uploads/2026/04/b.png" alt="b.png" style="max-width:100%;" />')
  })

  it('将多段视频转换为连续 video HTML', () => {
    const html = buildMediaInsertHtml('video', [
      { url: 'https://cdn.example.com/a.mp4' },
      { url: '/uploads/2026/04/b.mp4' },
    ])

    expect(html).toContain('<video src="https://cdn.example.com/a.mp4" controls style="max-width:100%;">您的浏览器不支持视频播放</video>')
    expect(html).toContain('<video src="/uploads/2026/04/b.mp4" controls style="max-width:100%;">您的浏览器不支持视频播放</video>')
  })

  it('将多个附件转换为连续链接，并使用文件名作为默认显示文本', () => {
    const html = buildMediaInsertHtml('file', [
      { url: 'https://cdn.example.com/a.pdf', name: '说明.pdf' },
      { url: '/uploads/2026/04/b.docx', text: '下载文档' },
    ])

    expect(html).toContain('<a href="https://cdn.example.com/a.pdf" target="_blank" download="说明.pdf">说明.pdf</a>')
    expect(html).toContain('<a href="/uploads/2026/04/b.docx" target="_blank" download="下载文档">下载文档</a>')
  })

  it('只允许 http、https 和系统资源绝对路径', () => {
    expect(isSafeMediaUrl('https://example.com/a.png')).toBe(true)
    expect(isSafeMediaUrl('http://example.com/a.png')).toBe(true)
    expect(isSafeMediaUrl('/uploads/2026/04/a.png')).toBe(true)
    expect(isSafeMediaUrl('/resource/2026/04/a.png')).toBe(true)
    expect(isSafeMediaUrl('javascript:alert(1)')).toBe(false)
    expect(isSafeMediaUrl('data:text/html,<script>alert(1)</script>')).toBe(false)
    expect(isSafeMediaUrl('2026/04/a.png')).toBe(false)
  })

  it('从多行输入解析多项地址并过滤空行', () => {
    expect(normalizeMediaItems('https://a.example/a.png\n\n/uploads/b.png')).toEqual([
      { url: 'https://a.example/a.png' },
      { url: '/uploads/b.png' },
    ])
  })
})
