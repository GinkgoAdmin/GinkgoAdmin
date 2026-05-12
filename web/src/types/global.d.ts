// 全局类型声明

declare global {
  interface Window {
    // jQuery相关
    $: any
    jQuery: any
    
    // Summernote相关
    summernote: any
    
    // Monaco Editor相关
    monaco: any
    
    // Quill相关
    Quill: any
    
    // 其他编辑器相关
    CodeMirror: any
    
    // Socket.IO相关
    io: any
    
    // Markdown解析器
    marked: any
    
    // 代码高亮
    hljs: any
  }
}

export {}