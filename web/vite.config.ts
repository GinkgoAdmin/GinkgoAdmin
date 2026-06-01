import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { DEV_BACKEND_ORIGIN } from './src/config/env'

const webRoot = __dirname
const webNodeModules = path.resolve(webRoot, './node_modules')

export default defineConfig({
  plugins: [vue()],
  base: '/',
  resolve: {
    alias: {
      '@': path.resolve(webRoot, './src'),
      vue: path.resolve(webNodeModules, './vue/dist/vue.runtime.esm-bundler.js'),
      axios: path.resolve(webNodeModules, './axios/index.js'),
      'element-plus': path.resolve(webNodeModules, './element-plus'),
      '@element-plus/icons-vue': path.resolve(webNodeModules, './@element-plus/icons-vue')
    }
  },
  optimizeDeps: {
    include: [
      'vue',
      'vue-router',
      'pinia',
      'axios',
      'element-plus',
      'element-plus/es/locale/lang/zh-cn',
      '@element-plus/icons-vue',
      // Three.js 及其 jsm 模块提前预打包，避免首次访问 3D 页时 Vite 按需重优化退出 504
      'three',
      'three/examples/jsm/controls/OrbitControls.js',
      'three/examples/jsm/controls/TransformControls.js',
      // MapLibre GL JS 提前预打包，避免首次访问 Geo 编辑器时 Vite 按需重优化退出 504
      'maplibre-gl'
    ]
  },
  build: {
    rollupOptions: {
      output: {
        banner: '/*! GinkgoAdmin | https://www.ginkgoadmin.com | Copyright \u00a9 2026 GinkgoAdmin. All rights reserved. */',
        manualChunks: {
          'vendor-vue': ['vue', 'vue-router', 'pinia'],
          'vendor-element': ['element-plus', '@element-plus/icons-vue'],
          'vendor-axios': ['axios']
        }
      }
    },
    chunkSizeWarningLimit: 1000
  },
  server: {
    host: '127.0.0.1',
    port: 5174,
    proxy: {
      // SSE 流式对话：开发模式下前端直连后端（见 aicore.ts），此条仅作备用
      '/api/aicore/chat/stream': {
        target: DEV_BACKEND_ORIGIN,
        changeOrigin: true
      },
      '/api': {
        target: DEV_BACKEND_ORIGIN,
        changeOrigin: true
      },
      '/hubs': {
        target: DEV_BACKEND_ORIGIN,
        ws: true,
        changeOrigin: true
      },
      '/uploads': {
        target: DEV_BACKEND_ORIGIN,
        changeOrigin: true
      },
      '/resource': {
        target: DEV_BACKEND_ORIGIN,
        changeOrigin: true
      },
      '/v1': {
        target: DEV_BACKEND_ORIGIN,
        changeOrigin: true
      }
    }
  }
})
