import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { PrintConfig, ColumnConfig } from '../types'

export function usePrint(
  config?: PrintConfig | boolean,
  columns?: ColumnConfig[],
  data?: any[]
) {
  const printDialogVisible = ref(false)
  const printLoading = ref(false)
  const printPreviewData = ref<any[]>([])

  const printConfig = typeof config === 'boolean' 
    ? { enabled: config } 
    : (config || {})

  const showPreview = printConfig.showPreview !== false
  const printTitle = printConfig.title || '数据列表'

  /**
   * 执行打印
   */
  async function handlePrint(currentData?: any[], allData?: any[]) {
    try {
      printLoading.value = true

      // 确定要打印的数据
      let dataToPrint = printConfig.printAll ? allData : currentData
      if (!dataToPrint || dataToPrint.length === 0) {
        ElMessage.warning('没有可打印的数据')
        return
      }

      // 打印前数据处理
      if (printConfig.beforePrint) {
        dataToPrint = printConfig.beforePrint(dataToPrint)
      }

      printPreviewData.value = dataToPrint

      if (showPreview) {
        printDialogVisible.value = true
      } else {
        await executePrint(dataToPrint)
      }
    } catch (error: any) {
      ElMessage.error(`打印准备失败: ${error.message}`)
    } finally {
      printLoading.value = false
    }
  }

  /**
   * 确认打印
   */
  async function confirmPrint() {
    await executePrint(printPreviewData.value)
    printDialogVisible.value = false
  }

  /**
   * 取消打印
   */
  function cancelPrint() {
    printDialogVisible.value = false
    printPreviewData.value = []
  }

  /**
   * 执行打印逻辑
   */
  async function executePrint(dataToPrint: any[]) {
    if (!columns || columns.length === 0) {
      ElMessage.error('打印配置错误：缺少列定义')
      return
    }

    try {
      printLoading.value = true

      // 生成打印HTML
      const printHtml = generatePrintHtml(dataToPrint, columns)

      // 创建打印窗口
      const printWindow = window.open('', '_blank', 'width=800,height=600')
      if (!printWindow) {
        ElMessage.error('无法打开打印窗口，请检查浏览器弹窗设置')
        return
      }

      printWindow.document.write(printHtml)
      printWindow.document.close()

      // 等待内容加载完成后打印
      printWindow.onload = () => {
        setTimeout(() => {
          printWindow.print()
          // 打印完成后关闭窗口（可选）
          // printWindow.close()
        }, 250)
      }

      ElMessage.success('打印预览已打开')
    } catch (error: any) {
      ElMessage.error(`打印失败: ${error.message}`)
    } finally {
      printLoading.value = false
    }
  }

  /**
   * 生成打印HTML
   */
  function generatePrintHtml(dataToPrint: any[], columns: ColumnConfig[]): string {
    const defaultStyles = `
      <style>
        * {
          margin: 0;
          padding: 0;
          box-sizing: border-box;
        }
        body {
          font-family: 'Microsoft YaHei', Arial, sans-serif;
          padding: 20px;
          background: #fff;
        }
        .print-header {
          text-align: center;
          margin-bottom: 20px;
          padding-bottom: 10px;
          border-bottom: 2px solid #333;
        }
        .print-title {
          font-size: 24px;
          font-weight: bold;
          margin-bottom: 10px;
        }
        .print-meta {
          font-size: 12px;
          color: #666;
        }
        .print-table {
          width: 100%;
          border-collapse: collapse;
          margin-top: 20px;
        }
        .print-table th,
        .print-table td {
          border: 1px solid #ddd;
          padding: 8px 12px;
          text-align: left;
          font-size: 12px;
        }
        .print-table th {
          background-color: #f5f5f5;
          font-weight: bold;
          color: #333;
        }
        .print-table tbody tr:nth-child(even) {
          background-color: #fafafa;
        }
        .print-footer {
          margin-top: 30px;
          padding-top: 10px;
          border-top: 1px solid #ddd;
          text-align: center;
          font-size: 12px;
          color: #666;
        }
        @media print {
          body {
            padding: 0;
          }
          .print-header {
            page-break-after: avoid;
          }
          .print-table {
            page-break-inside: avoid;
          }
          .print-table thead {
            display: table-header-group;
          }
          .print-table tbody tr {
            page-break-inside: avoid;
            page-break-after: auto;
          }
        }
      </style>
    `

    const customStyles = printConfig.customStyles 
      ? `<style>${printConfig.customStyles}</style>` 
      : ''

    // 生成表头
    const thead = `
      <thead>
        <tr>
          ${columns.map(col => `<th>${col.label}</th>`).join('')}
        </tr>
      </thead>
    `

    // 生成表体
    const tbody = `
      <tbody>
        ${dataToPrint.map(row => `
          <tr>
            ${columns.map(col => {
              let value = row[col.prop]
              
              // 格式化器处理
              if (col.formatter) {
                value = col.formatter(row, col, value, 0)
              }
              
              // 特殊类型处理
              if (col.type === 'tag' || col.type === 'status') {
                value = Array.isArray(value) ? value.join(', ') : value
              }
              
              return `<td>${value ?? '-'}</td>`
            }).join('')}
          </tr>
        `).join('')}
      </tbody>
    `

    const currentDate = new Date().toLocaleString('zh-CN')

    return `
      <!DOCTYPE html>
      <html lang="zh-CN">
      <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>${printTitle}</title>
        ${defaultStyles}
        ${customStyles}
      </head>
      <body>
        <div class="print-header">
          <div class="print-title">${printTitle}</div>
          <div class="print-meta">
            <span>打印时间：${currentDate}</span>
            <span style="margin-left: 20px;">共 ${dataToPrint.length} 条数据</span>
          </div>
        </div>
        
        <table class="print-table">
          ${thead}
          ${tbody}
        </table>
        
        <div class="print-footer">
          <p>本页数据由系统自动生成</p>
        </div>
      </body>
      </html>
    `
  }

  return {
    printDialogVisible,
    printLoading,
    printPreviewData,
    handlePrint,
    confirmPrint,
    cancelPrint
  }
}

