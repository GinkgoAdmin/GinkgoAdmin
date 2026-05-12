# Ginkgo 管理后台主题架构

本文档说明基于 Vue 3 + Vite + Element Plus 的“主题优先（variables‑first）”样式架构与约定。

## 目标
- 变量优先：功能样式只消费 CSS 变量（不直接写十六进制颜色）
- 职责清晰：区分 common（通用）/ theme（主题差异）/ overrides（组件覆盖）
- 热切换无闪烁：主题按需动态加载，初始化优先加载 variables
- 易扩展：新增主题仅需提供 variables，必要时少量 overrides

## 目录结构

```
web/src/styles/admin/
├─ theme-manager.ts           # 统一初始化/切换 + 动态加载 CSS
├─ theme-config.json          # 主题元数据与入口
├─ common/                    # 各主题共享（尽量无颜色）
│  ├─ base.css
│  ├─ utilities.css
│  ├─ responsive.css
│  └─ fonts.css
└─ themes/
   ├─ light/
   │  ├─ variables.css        # :root 定义本主题令牌
   │  ├─ layout.css           # 布局层差异（可选）
   │  ├─ components.css       # 组件层差异（可选）
   │  ├─ animations.css
   │  ├─ overrides.css        # Element Plus/Teleport 等覆盖
   │  └─ pages.css
   └─ dark/
      ├─ variables.css        # 暗色主题令牌
      ├─ layout.css
      ├─ components.css
      ├─ animations.css
      ├─ overrides.css
      └─ pages.css
```

应用仅在入口一次性引入 Tailwind + common，全量主题 CSS 由 `theme-manager.ts` 按需注入。

## 加载顺序与职责
1) Tailwind + Element Plus 基础样式（main.ts 全局导入）
2) Common 通用样式/结构（main.ts 全局导入）
3) 主题 CSS（由 theme-manager 注入：variables 优先，其后为可选模块）

- 必须先加载 variables，避免颜色 FOUC
- 布局/组件尽量只消费变量；主题分片只补差异

## 迁移策略
- 阶段一：拆分 variables（已完成），保留现有全局布局消费变量
- 阶段二：逐步将 `.dark` 结构/差异迁移到 `themes/dark/overrides.css`
- 阶段三：将页面/组件特定的主题差异迁入对应文件

保持 common/ 中立且无颜色。如需引用颜色，优先新增变量；若为主题特有规则，迁至主题目录。

## Element Plus 与 Teleport
- 将 Teleport（Dropdown/Popper/Tooltip）覆盖放在 `themes/*/overrides.css`（或确属通用时放 common）
- 全局 CSS 禁用 Vue SFC 的 `:deep()`，使用真实选择器（已完成重构）

