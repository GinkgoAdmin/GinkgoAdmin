/**
 * 编辑器测试工具
 */

export interface EditorTestResult {
  success: boolean
  message: string
  details?: any
}

/**
 * 测试jQuery是否加载
 */
export function testJQuery(): EditorTestResult {
  try {
    if (typeof window !== 'undefined' && window.$ && window.jQuery) {
      return {
        success: true,
        message: 'jQuery已成功加载',
        details: {
          version: window.$.fn.jquery || 'unknown'
        }
      }
    } else {
      return {
        success: false,
        message: 'jQuery未加载或不可用'
      }
    }
  } catch (error) {
    return {
      success: false,
      message: 'jQuery测试失败',
      details: error
    }
  }
}

/**
 * 测试Bootstrap是否加载
 */
export function testBootstrap(): EditorTestResult {
  try {
    // 检查Bootstrap CSS
    const bootstrapCSS = document.querySelector('link[href*="bootstrap"]')
    
    // 检查Bootstrap JS
    const hasBootstrapJS = typeof window !== 'undefined' && 
                          window.$ && 
                          window.$.fn && 
                          window.$.fn.modal
    
    if (bootstrapCSS && hasBootstrapJS) {
      return {
        success: true,
        message: 'Bootstrap已成功加载',
        details: {
          css: !!bootstrapCSS,
          js: hasBootstrapJS
        }
      }
    } else {
      return {
        success: false,
        message: 'Bootstrap未完全加载',
        details: {
          css: !!bootstrapCSS,
          js: hasBootstrapJS
        }
      }
    }
  } catch (error) {
    return {
      success: false,
      message: 'Bootstrap测试失败',
      details: error
    }
  }
}

/**
 * 测试Summernote是否加载
 */
export function testSummernote(): EditorTestResult {
  try {
    if (typeof window !== 'undefined' && 
        window.$ && 
        window.$.fn && 
        window.$.fn.summernote) {
      return {
        success: true,
        message: 'Summernote已成功加载',
        details: {
          version: window.$.summernote?.version || 'unknown'
        }
      }
    } else {
      return {
        success: false,
        message: 'Summernote未加载或不可用'
      }
    }
  } catch (error) {
    return {
      success: false,
      message: 'Summernote测试失败',
      details: error
    }
  }
}

/**
 * 运行所有编辑器依赖测试
 */
export function runAllEditorTests(): Record<string, EditorTestResult> {
  return {
    jquery: testJQuery(),
    bootstrap: testBootstrap(),
    summernote: testSummernote()
  }
}

/**
 * 等待依赖加载完成
 */
export function waitForDependencies(
  dependencies: string[] = ['jquery', 'bootstrap', 'summernote'],
  timeout: number = 10000
): Promise<boolean> {
  return new Promise((resolve) => {
    const startTime = Date.now()
    
    const checkDependencies = () => {
      const results = runAllEditorTests()
      const allLoaded = dependencies.every(dep => results[dep]?.success)
      
      if (allLoaded) {
        resolve(true)
        return
      }
      
      if (Date.now() - startTime > timeout) {
        resolve(false)
        return
      }
      
      setTimeout(checkDependencies, 100)
    }
    
    checkDependencies()
  })
}

/**
 * 创建测试用的Summernote实例
 */
export function createTestSummernoteInstance(container: HTMLElement): Promise<any> {
  return new Promise((resolve, reject) => {
    if (!window.$ || !window.$.fn.summernote) {
      reject(new Error('Summernote未加载'))
      return
    }
    
    try {
      const $container = window.$(container)
      const instance = $container.summernote({
        height: 200,
        placeholder: '测试编辑器...',
        toolbar: [
          ['style', ['bold', 'italic']],
          ['para', ['ul', 'ol']],
          ['insert', ['link']]
        ],
        callbacks: {
          onInit: () => {
            resolve(instance)
          },
          onError: (error: any) => {
            reject(error)
          }
        }
      })
    } catch (error) {
      reject(error)
    }
  })
}

/**
 * 销毁测试用的Summernote实例
 */
export function destroyTestSummernoteInstance(container: HTMLElement): void {
  try {
    if (window.$ && container) {
      window.$(container).summernote('destroy')
    }
  } catch (error) {
    // silently ignored
  }
}