# 主题开发指南

本指南介绍如何开发、扩展与维护主题。

## 新增主题
1. 在 `themes/` 下创建文件夹，例如 `themes/high-contrast/`
2. 创建以下文件：
   - `variables.css`（必需）：在 `:root` 中定义全部 `--admin-*` 令牌
   - 可选：`layout.css`、`components.css`、`animations.css`、`overrides.css`、`pages.css`
3. 在 `theme-config.json` 中注册主题（名称、入口、元数据）
4. 确保令牌覆盖 `common` 与业务 CSS 的所有使用场景，功能样式内不要写十六进制颜色

## 令牌优先
- 调色板与语义令牌统一放在 `variables.css`
- 功能样式只使用 `var(--admin-*)`
- 需要新的颜色层次时，新增语义令牌，不内联十六进制

## 规则放在哪里？
- `common/`：通用、无色的结构辅助与工具类
- `themes/<name>/layout.css`：因主题不同而异的结构性调整
- `themes/<name>/components.css`：对 Element Plus 或自研组件的差异化
- `themes/<name>/overrides.css`：Teleport（Dropdown/Popper）及特异性敏感覆盖
- `themes/<name>/pages.css`：页面级

## 集成 Element Plus
- 优先在 `variables.css` 中做变量映射（如 `--el-color-primary`）
- 对 popper/dropdown 使用全局选择器；此处不要使用 `:deep()`

## 热切换与预加载
- `theme-manager.ts` 会优先加载 `variables.css`，再加载其它分片
- 通过 `switchTheme('light'|'dark')` 切换：替换 CSS link，并为兼容性切换 `.dark` 类
- 为避免 FOUC，在 `main.ts` 中 `await initTheme()`，在应用挂载前完成变量加载

## 测试清单
- 明/暗切换无颜色闪烁
- 侧边栏菜单 hover/active 在两套主题下表现一致
- Tabs、popper、dropdown 能正确读取变量，覆盖生效
- 新增功能 CSS 中无硬编码颜色

## 贡献主题
- 从现有主题复制令牌并调整取值
- 校验令牌是否覆盖所有 UI 状态；不足则提出新增语义令牌
- 提交 `theme-config.json` 与新主题目录的变更

