/**
 * 插件 npm 依赖自动安装脚本
 * 
 * 运行时机：npm install 完成后自动执行（postinstall 钩子）
 * 
 * 功能：
 * 1. 扫描 src/plugins/installed/ 下所有已安装插件的 module.json 和 plugin.json
 * 2. 收集所有声明的 npmDependencies
 * 3. 检测 node_modules 中是否已存在
 * 4. 对缺失的依赖使用当前包管理器（pnpm/yarn/npm）的 --no-save 模式安装（不写入主框架 package.json）
 */

const fs = require('fs')
const path = require('path')
const { execSync } = require('child_process')

const webRoot = path.resolve(__dirname, '..')

/**
 * 检测当前项目使用的包管理器
 * 优先通过运行时环境变量判断（pnpm/yarn 运行 scripts 时会注入 npm_config_user_agent），
 * 其次通过 lock 文件判断。
 */
function detectPackageManager() {
  const userAgent = process.env.npm_config_user_agent || ''
  if (userAgent.includes('pnpm')) return 'pnpm'
  if (userAgent.includes('yarn')) return 'yarn'
  if (fs.existsSync(path.join(webRoot, 'pnpm-lock.yaml'))) return 'pnpm'
  if (fs.existsSync(path.join(webRoot, 'yarn.lock'))) return 'yarn'
  return 'npm'
}
const pluginsDir = path.join(webRoot, 'src', 'plugins', 'installed')

/**
 * 从 JSON 文件中解析 npmDependencies
 */
function parseNpmDeps(jsonPath) {
  try {
    const content = fs.readFileSync(jsonPath, 'utf-8')
    const data = JSON.parse(content)
    const deps = data.npmDependencies || []
    return deps
      .filter(d => d && d.name)
      .map(d => ({
        name: d.name,
        version: d.version || ''
      }))
  } catch {
    return []
  }
}

/**
 * 主逻辑
 */
function main() {
  if (!fs.existsSync(pluginsDir)) {
    console.log('[install-plugin-deps] 插件目录不存在，跳过')
    return
  }

  // 收集所有插件的 npm 依赖（去重）
  const depsMap = new Map() // name -> version
  const dirs = fs.readdirSync(pluginsDir, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .map(d => path.join(pluginsDir, d.name))

  for (const dir of dirs) {
    // 优先读取 module.json，否则读 plugin.json
    const moduleJson = path.join(dir, 'module.json')
    const pluginJson = path.join(dir, 'plugin.json')
    // module.json 和 plugin.json 都可能声明 npmDependencies，两个文件都要扫描并合并
    const filesToScan = [moduleJson, pluginJson].filter(f => fs.existsSync(f))
    for (const jsonFile of filesToScan) {
      for (const dep of parseNpmDeps(jsonFile)) {
        if (!depsMap.has(dep.name)) {
          depsMap.set(dep.name, dep.version)
        }
      }
    }
  }

  if (depsMap.size === 0) {
    console.log('[install-plugin-deps] 没有插件声明 npm 依赖')
    return
  }

  // 检测缺失的依赖
  const missingDeps = []
  for (const [name, version] of depsMap) {
    const pkgDir = path.join(webRoot, 'node_modules', name)
    if (!fs.existsSync(pkgDir)) {
      const spec = version ? `${name}@${version}` : name
      missingDeps.push(spec)
    }
  }

  if (missingDeps.length === 0) {
    console.log(`[install-plugin-deps] 所有插件依赖均已安装 (共 ${depsMap.size} 个)`)
    return
  }

  // 安装缺失的依赖
  // 注意：pnpm 和 yarn 均不支持 --no-save，只有 npm 原生支持。
  // 在 CI 环境中对 package.json 的临时修改不会被提交，因此对 pnpm/yarn 直接执行 add 即可。
  const pm = detectPackageManager()
  const specs = missingDeps.join(' ')
  const installCmd = pm === 'pnpm'
    ? `pnpm add ${specs}`
    : pm === 'yarn'
      ? `yarn add ${specs}`
      : `npm install ${specs} --no-save`

  console.log(`[install-plugin-deps] 使用 ${pm} 安装 ${missingDeps.length} 个插件依赖: ${specs}`)
  try {
    execSync(installCmd, {
      cwd: webRoot,
      stdio: 'inherit'
    })
    console.log('[install-plugin-deps] 插件依赖安装完成')
  } catch (err) {
    console.error('[install-plugin-deps] 插件依赖安装失败:', err.message)
    process.exit(1)
  }
}

main()
