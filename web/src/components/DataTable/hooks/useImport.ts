import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { ImportConfig } from '../types'

export function useImport(config?: ImportConfig | boolean) {
  const importDialogVisible = ref(false)
  const importLoading = ref(false)
  const importPreviewData = ref<any[]>([])
  const importFileList = ref<any[]>([])

  const importConfig = typeof config === 'boolean' 
    ? { enabled: config } 
    : (config || {})

  const maxRows = importConfig.maxRows || 10000
  const showPreview = importConfig.showPreview !== false

  /**
   * 处理文件上传
   */
  async function handleFileChange(file: any) {
    const rawFile = file.raw
    if (!rawFile) return false

    // 检查文件类型
    const fileType = rawFile.name.split('.').pop()?.toLowerCase()
    if (!['xlsx', 'xls', 'csv'].includes(fileType || '')) {
      ElMessage.error('只支持 Excel (.xlsx, .xls) 和 CSV (.csv) 文件')
      return false
    }

    // 检查文件大小（10MB）
    if (rawFile.size > 10 * 1024 * 1024) {
      ElMessage.error('文件大小不能超过 10MB')
      return false
    }

    try {
      importLoading.value = true
      const data = await parseExcelFile(rawFile)
      
      // 检查行数限制
      if (data.length > maxRows) {
        ElMessage.error(`导入数据不能超过 ${maxRows} 行`)
        return false
      }

      // 字段映射
      const mappedData = mapFields(data)
      importPreviewData.value = mappedData

      if (showPreview) {
        importDialogVisible.value = true
      } else {
        await confirmImport()
      }

      return false // 阻止自动上传
    } catch (error: any) {
      ElMessage.error(`文件解析失败: ${error.message}`)
      return false
    } finally {
      importLoading.value = false
    }
  }

  /**
   * 解析 Excel 文件
   */
  async function parseExcelFile(file: File): Promise<any[]> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader()
      
      reader.onload = async (e) => {
        try {
          const data = e.target?.result
          if (!data) {
            reject(new Error('文件读取失败'))
            return
          }

          // 动态导入 xlsx 库
          const XLSX = await import('xlsx')
          const workbook = XLSX.read(data, { type: 'binary' })
          const firstSheet = workbook.Sheets[workbook.SheetNames[0]]
          const jsonData = XLSX.utils.sheet_to_json(firstSheet)

          resolve(jsonData as any[])
        } catch (error) {
          reject(error)
        }
      }

      reader.onerror = () => reject(new Error('文件读取失败'))
      reader.readAsBinaryString(file)
    })
  }

  /**
   * 字段映射
   */
  function mapFields(data: any[]): any[] {
    if (!importConfig.fieldMapping) return data

    return data.map(row => {
      const mappedRow: any = {}
      Object.keys(row).forEach(key => {
        const mappedKey = importConfig.fieldMapping?.[key] || key
        mappedRow[mappedKey] = row[key]
      })
      return mappedRow
    })
  }

  /**
   * 确认导入
   */
  async function confirmImport() {
    try {
      importLoading.value = true

      if (importConfig.handler) {
        await importConfig.handler(importPreviewData.value)
      }

      ElMessage.success(`成功导入 ${importPreviewData.value.length} 条数据`)
      importDialogVisible.value = false
      importFileList.value = []
      importPreviewData.value = []
    } catch (error: any) {
      ElMessage.error(`导入失败: ${error.message}`)
    } finally {
      importLoading.value = false
    }
  }

  /**
   * 取消导入
   */
  function cancelImport() {
    importDialogVisible.value = false
    importFileList.value = []
    importPreviewData.value = []
  }

  /**
   * 下载导入模板
   */
  async function downloadTemplate() {
    if (importConfig.templateUrl) {
      // 如果提供了模板 URL，直接下载
      const link = document.createElement('a')
      link.href = importConfig.templateUrl
      link.download = '导入模板.xlsx'
      link.click()
    } else {
      // 生成默认模板
      ElMessage.info('请联系管理员获取导入模板')
    }
  }

  return {
    importDialogVisible,
    importLoading,
    importPreviewData,
    importFileList,
    handleFileChange,
    confirmImport,
    cancelImport,
    downloadTemplate
  }
}

