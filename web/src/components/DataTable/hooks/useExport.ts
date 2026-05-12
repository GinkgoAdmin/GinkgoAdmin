export function useExport() {
  function exportCsv(rows: any[], columns: { prop: string; label: string }[], fileName = 'export.csv') {
    const header = columns.map(c=>`"${c.label}"`).join(',')
    const body = rows.map(r=> columns.map(c=>`"${(r as any)[c.prop] ?? ''}"`).join(',')).join('\n')
    const csv = header + '\n' + body
    const blob = new Blob([\uFEFF + csv], { type: 'text/csv;charset=utf-8;' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = fileName
    link.click()
    URL.revokeObjectURL(link.href)
  }
  return { exportCsv }
}


