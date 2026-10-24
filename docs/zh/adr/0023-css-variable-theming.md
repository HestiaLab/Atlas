# ADR-0023：CSS 变量主题契约

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-08-06
- **决策人**：维护者

## Context

Atlas 以单个样式表（`wwwroot/atlas-v2/atlas.css`，286 行）作为 Razor Class Library 静态资产分发。使用者需显式链接：

```html
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/atlas.css" />
```

当前样式表只暴露五个内部变量（`--atlas-splitter-size`、`--atlas-layer-content`、`--atlas-layer-chrome`、`--atlas-layer-drag`）；所有颜色、圆角、内边距与透明度值均为硬编码（`Highlight`、`Canvas`、`CanvasText`、`currentColor`、`rgb(0 0 0 / 28%)`、`color-mix(...)`、`0.25rem`、`2rem` 等）。Application Developer（应用程序开发者）需要自定义外观时没有任何受支持的机制：唯一方式是覆盖内部类选择器并依赖 `!important`，这很脆弱且每次升级都会破坏。SRS 中没有任何需求覆盖视觉定制。

业界实践（MudBlazor `--mud-*`、Radzen `--rz-*`、Bootstrap 5、Ant Design tokens）一致地把 CSS 自定义属性作为主题接缝：零运行时成本、无 JavaScript 依赖、无需改动构建链，且升级安全（库升级新增变量或调整默认值不会破坏既有覆盖，只要不删除变量）。

## Decision

### 1. 样式表 token 化

`atlas.css` 中每一个硬编码的视觉值都通过文档化的 `--atlas-*` 自定义属性表达。默认值声明在 `.atlas-v2-workspace` 作用域上，使其不会泄漏到组件外部，且使用者可以全局覆盖（任意 `.atlas-v2-workspace`）或按实例覆盖（后代选择器，如 `#my-dock .atlas-v2-workspace`）。

### 2. 变量契约（目标清单）

使用语义化名称而非实现化名称（`--atlas-accent`，而非 `--atlas-blue`）。清单一经发布即为公共 API：变量可以新增或调整默认值，但绝不可删除。

颜色：

| 变量 | 默认值 | 使用位置 |
| --- | --- | --- |
| `--atlas-accent` | `Highlight` | 放置区域高亮、焦点环、拖拽预览边框 |
| `--atlas-surface` | `Canvas` | 拖拽预览背景 |
| `--atlas-text` | `CanvasText` | 拖拽预览标题文字 |
| `--atlas-drag-preview-shadow` | `rgb(0 0 0 / 28%)` | 拖拽预览阴影 |
| `--atlas-group-bg` | `transparent` | 组表面背景 |
| `--atlas-chrome-bg` | `transparent` | Tab 条 / 面板标题栏 / Toolbar 背景 |
| `--atlas-tab-text` | `inherit` | Tab 标签文字颜色 |
| `--atlas-tab-text-active` | `inherit` | 激活 Tab 标签文字颜色 |
| `--atlas-tab-bg-active` | `transparent` | 激活 Tab 背景 |
| `--atlas-tab-bg-hover` | `transparent` | 悬停 Tab 背景 |
| `--atlas-splitter-color` | `currentColor` | 分割条颜色 |

尺寸：

| 变量 | 默认值 | 使用位置 |
| --- | --- | --- |
| `--atlas-splitter-size` | `0.25rem` | 分割条厚度（已有） |
| `--atlas-splitter-hit-area` | `0.25rem` | 分割条指针热区扩展 |
| `--atlas-chrome-height` | `2rem` | Tab 条 / 面板标题栏 / 空区域高度 |
| `--atlas-gap` | `0.375rem` | 拖拽预览内部间距 |
| `--atlas-padding-compact` | `0.375rem 0.625rem` | 拖拽预览内边距 |
| `--atlas-radius` | `0.25rem` | 常规圆角 |
| `--atlas-radius-sm` | `0.125rem` | 小圆角（区域高亮） |
| `--atlas-border-width` | `1px` | 细边框（toolbar 分隔线、拖拽预览） |
| `--atlas-focus-ring-width` | `2px` | 焦点环与放置区域 outline 宽度 |
| `--atlas-drag-preview-max-width` | `min(20rem, calc(100vw - 1rem))` | 拖拽预览上限 |

层级（已有，保留）：

| 变量 | 默认值 |
| --- | --- |
| `--atlas-layer-content` | `0` |
| `--atlas-layer-chrome` | `10` |
| `--atlas-layer-drag` | `30` |

透明度（重构时统一）：

| 变量 | 默认值 | 使用位置 |
| --- | --- | --- |
| `--atlas-splitter-opacity` | `0.18` | 分割条闲置 |
| `--atlas-splitter-opacity-hover` | `0.4` | 分割条悬停 / 调整中 |
| `--atlas-splitter-opacity-focus` | `0.5` | 分割条 focus-visible |
| `--atlas-splitter-opacity-disabled` | `0.08` | 分割条禁用 |
| `--atlas-drop-highlight-alpha-soft` | `15%` | 通用（整块）放置高亮混色比例 |
| `--atlas-drop-highlight-alpha-strong` | `22%` | 区域形状放置高亮混色比例 |
| `--atlas-toolbar-divider-opacity` | `0.25` | Toolbar 分隔线 |
| `--atlas-drag-preview-opacity` | `0.92` | 拖拽预览 |

最终清单在实现时锁定；新增必须保持默认外观与改动前基线一致。

### 3. 契约保证

- **默认值不变**：无覆盖时渲染与基线一致（真实浏览器验证）。
- **无需 `!important` 路径**：使用者只覆盖变量。
- **两条文档化定制路径**：(a) 在使用者自己的样式表中覆盖变量（推荐）；(b) 将样式表复制到使用者项目内修改（深度定制，停止跟随升级）。

## Alternatives

- **C# 主题/选项对象（`AtlasTheme`）**。暂不采纳：它最终仍要产出 CSS 变量，且增加第二处真相源，可能与样式表漂移；运行时主题切换也不是当前需求。变量契约保留了这条路径——选项对象将来只是 `--atlas-*` 值的生产者。
- **SCSS 源 + 多主题产物**。不采纳：引入 Sass 构建链、使 RCL 静态资产分发复杂化、并成倍增加产物。仅当 Atlas 发展为完整主题生态（Bootstrap 规模）时再评估，且应建立在变量契约之上。
- **结构/皮肤样式分离**。在当前规模下不采纳：两个链接文件带来的协调成本大于收益，直到样式表显著增长。该分离未来仍可在不破坏变量契约的前提下实现。

## Consequences

- **行为**：无功能变化；纯样式接缝。
- **契约**：变量清单成为已发布 API；未来版本不得删除变量。
- **文档**：主题指南（`docs/zh/theming.md`）记录变量清单、两条定制路径与工作示例。
- **测试**：默认外观 golden 覆盖（真实浏览器）加变量覆盖检查（`--atlas-accent` 改变放置高亮/焦点）。
- **范围**：仅 `atlas.css`——无 C#、无 TypeScript、无协议改动。

## Verification

- `AtlasWorkspaceHostTests`/bUnit 保持绿（无 C# 改动）。
- Node 测试保持绿（无协议改动）。
- 真实浏览器 fixture：默认外观不变；`--atlas-accent` 覆盖改变放置区域高亮、焦点环与拖拽预览边框，无需 `!important`。
- `dotnet build/test/format` + `npm typecheck/test:js` 全量门禁。

## Supersession

新决策；无 ADR 被取代。它是未来主题预设（`--atlas-*` 覆盖块）与可选运行时主题对象的使能决策。
