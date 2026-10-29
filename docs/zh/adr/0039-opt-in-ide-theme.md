# ADR-0039：第一方可选 IDE 主题

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-29
- **决策人**：维护者

## Context

Atlas 的默认样式表（`wwwroot/atlas-v2/atlas.css`）使用系统颜色（`Canvas`、`CanvasText`、`Highlight`），并通过 ADR-0023 的 `--atlas-*` 变量暴露颜色、尺寸、圆角与透明度。该默认外观是 REQ-NF-009 的已发布基线。

DbcView 是 Atlas 的下游应用，其 `app.css` 已经形成一套紧凑 IDE 视觉：`--dv-*` 调色板、11px 大写面板标题、12px 标签页、竖排工具栏、4px 圆角、1px 分割线、2.125rem chrome 高度，以及 accent 下划线。该视觉的一部分只能通过覆盖 Atlas 内部选择器实现（组边框/圆角、标签页下划线、竖排工具栏、分割条细线），无法用现有 `--atlas-*` 变量表达。

SRS §1.2 将“主题定制”列为范围外；ADR-0023 明确预留了未来的静态主题预设。直接替换 `atlas.css` 的默认外观会破坏 REQ-NF-009 AC3，并影响所有下游应用。

## Decision

### 1. 交付形态

新增可选样式表 `wwwroot/atlas-v2/themes/ide.css`，在 `atlas.css` 之后链接即可生效：

```html
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/atlas.css" />
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/themes/ide.css" />
```

不链接时默认外观保持不变（ADR-0040 的 SVG 图标除外）。激活方式仅为链接，不新增组件参数、属性或 JavaScript API；主题作用于页面上所有 `.atlas-v2-workspace`。

### 2. 覆盖范围

主题只作用于 Atlas 自有工作区 chrome：组表面与边框、标签页/溢出菜单、面板标题栏/关闭按钮、工具栏、分割条与折叠按钮、弹出层、自动隐藏条目/Flyout、空/错误/缺失/冲突状态、拖拽预览与放置指示，以及顶栏/状态栏槽位表面。应用顶栏/状态栏内部内容、面板内容与命令面板仍由应用负责。槽位只提供表面与分隔线，不设置 `overflow` 裁剪，确保应用在顶栏/状态栏内的弹出层可以越过槽位边界。

主题在 workspace 上设置 `background`、`color` 与 `color-scheme: light dark`，以保证暗色模式可读性与原生滚动条/表单控件配色；内容排版（字体、字号、字重）不改变。

### 3. 明暗与保真度

主题仅通过 `prefers-color-scheme` 提供亮/暗调色板，不提供运行时切换 API。调色板与度量对照 DbcView 工作区维护：亮/暗背景、表面、边框、accent、危险/警告色、2.125rem chrome、0.3rem 分割轨道、4px 圆角、交互态 1px accent 分割线与 accent 下划线。与 DbcView 的差异：空闲 splitter 不绘制 1px 线，仅由组边框与 4.8px 间隙表达结构；悬停/拖拽/焦点时才显示 accent 线，减少多条边框叠加的噪点。

`--ide-*` 是主题私有变量，不属于 `--atlas-*` 公共契约；主题内部选择器也不是公共 API。

### 4. Token 与结构性规则的分工

主题复用现有 `--atlas-*` 变量，并新增五个单属性变量：

| 变量 | 默认值 | 用途 |
| --- | --- | --- |
| `--atlas-border-color` | `currentColor` | 组边框与主题结构性边框 |
| `--atlas-group-border-width` | `0px` | 组边框宽度 |
| `--atlas-group-radius` | `0px` | 组圆角 |
| `--atlas-tab-indicator-width` | `0px` | 激活 Tab 指示条厚度 |
| `--atlas-tab-indicator-color` | `transparent` | 激活 Tab 指示条颜色 |

默认值不产生可见效果，未链接主题时的基线不变。竖排工具栏、标签页下划线、分割条交互态 accent 线、面板标题排版与槽位边框等组合/结构效果由主题自有规则负责，不扩张变量契约。

### 5. 可访问性

主题采用 DbcView 的 WCAG AA 对比度合同：正文文本 ≥ 4.5:1，UI 指示器与焦点环 ≥ 3:1。该合同由 `ThemeContrastTests` 解析主题调色板自动验证。

### 6. 采用路径

`Atlas.Showcase` 在本次改动中链接主题并删除其 Atlas chrome 覆盖，作为仓库内演示。DbcView 的迁移作为后续独立 PR：链接主题并删除其 chrome 覆盖，其应用顶栏/状态栏/内容样式保留。

## Alternatives

- **直接替换 `atlas.css` 默认外观**：单一链接更简单，但破坏 REQ-NF-009 AC3，并对所有下游应用造成未协商的视觉破坏。不采纳。
- **仅扩张 token 契约**：无法表达 `writing-mode`、伪元素细线与标签页下划线；且会使公共变量清单膨胀。不采纳。
- **主题只写变量、不写选择器**：无法实现 DbcView 的结构性效果。不采纳。
- **属性/类名激活（如 `data-atlas-theme`）**：支持同页多主题，但增加公共 API 与文档成本；当前消费者均为单主题页面。不采纳。
- **运行时主题引擎**：需要全局状态与切换 API，超出静态样式表范围。继续延期。

## Consequences

- **契约**：新增五个 `--atlas-*` 变量，均为增量、默认不可见；主题资产路径成为稳定分发入口。
- **默认外观**：未链接主题时除 REQ-F-157 的图标变更外保持不变。
- **视觉漂移**：DbcView 迁移前，调色板在 `DbcView/app.css` 与 `ide.css` 各存一份；后续 PR 收敛。
- **Showcase**：不再现场演示“自定义皮肤”，该路径保留在 theming.md 与契约测试中。
- **范围**：仅 CSS 资产、文档与测试；无 C#/TypeScript 协议改动。

## Verification

- `CssTokenContractTests`：主题只使用文档化 token、亮/暗块存在、无 `!important`；默认样式表仍满足双向 token 契约。
- `ThemeContrastTests`：亮/暗调色板 AA 对比度。
- `tests/browser/ide-theme-fixture.html`：链接主题后的亮/暗工作区 chrome 视觉夹具。
- `Atlas.Showcase` 链接主题后 `dotnet build/test/format` 与 npm 门禁通过。

## Supersession

扩展 ADR-0023 的变量契约，不取代任何既有 ADR。REQ-NF-010 记录本决策的需求基线。
