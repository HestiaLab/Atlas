# ADR-0040：SVG chrome 图标与公开 AtlasIcon

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-29
- **决策人**：维护者

## Context

Atlas 自有 chrome 按钮此前使用文本字形：标签页溢出 `»`、面板关闭 `×`、分割条折叠 `‹`/`›`。这些字形依赖系统字体，尺寸与基线在不同平台不一致；分割条折叠在块轴（上下分割）上仍显示左右箭头，方向语义错误。DbcView 的应用按钮使用内联 SVG 图标（`currentColor`、16×16 viewBox、1.5 描边），视觉更稳定。

REQ-NF-009 AC3 要求默认外观与基线一致。把字形替换为图标是默认视觉变更，需要单独记录并刷新基线。

## Decision

### 1. 公开图标组件

新增公开 C# 组件 `Atlas.Blazor.Components.AtlasIcon` 与枚举 `AtlasIconName`：

- 枚举值：`Close`、`ChevronUp`、`ChevronDown`、`ChevronLeft`、`ChevronRight`；已有值不得删除或重排。
- 参数：`Name`（必填）、`Size`（默认 16）、`CssClass`（可选）。
- 渲染：内联 `<svg>`，`viewBox="0 0 16 16"`、`fill="none"`、`stroke="currentColor"`、`stroke-width="1.5"`、圆角线帽/连接，基础类 `atlas-v2-icon`。
- 可访问性：始终 `aria-hidden="true"`、`focusable="false"`；可访问名称由承载图标的控件提供。

图标路径为 Atlas 自有、手工维护的最小集合，不引入图标字体或第三方依赖。

### 2. 默认 chrome 图标

- 标签页溢出按钮：`ChevronDown`。
- 面板关闭按钮：`Close`。
- 分割条折叠按钮：按分割轴选择左右或上下箭头；折叠状态切换后箭头指向展开方向。
- 按钮原有的 `aria-label`/`title` 保持不变。

图标成为所有消费者的默认 chrome 外观，不提供 `UseChromeIcons` 等开关。

### 3. 基线刷新

本决策有意改变默认外观。REQ-NF-009 AC3 的“基线”更新为当前发行版基线，并在 SRS 中显式记录 ADR-0040 是唯一已记录的默认视觉变更。

## Alternatives

- **仅用 CSS 美化字形**：无 C#/API 变更，但跨平台字体差异与块轴方向问题仍在。不采纳。
- **CSS mask 图标**：默认外观同样改变，且图标不属于组件 DOM，消费者无法复用。不采纳。
- **opt-in 参数**：默认外观不变，但 DbcView/Showcase 需要同时链接主题并设置参数，破坏“链接即生效”的简单路径；还新增永久 API。不采纳。
- **内部图标组件**：避免公共 API 承诺，但消费者工具栏仍需自带图标集，无法复用 Atlas 的视觉语言。维护者选择公开组件。
- **移植 DbcView 的完整 DbcIcon 目录**：会引入 database/upload 等应用语义图标，扩大库的职责。不采纳；仅保留 chrome 所需的五个图标。

## Consequences

- **默认外观**：chrome 按钮从文本字形变为 SVG；这是 ADR-0023 基线以来唯一记录的默认视觉变更。
- **公共 API**：`AtlasIcon`/`AtlasIconName` 进入 `PublicAPI.Unshipped`，受公共 API 分析器与 semver 约束。
- **可访问性**：装饰性 `aria-hidden` 由组件保证；按钮名称继续由既有 `aria-label` 提供。
- **依赖**：无新增 NuGet/npm 包；图标以内联路径分发。
- **需求**：新增 REQ-F-157；REQ-NF-009 AC3 基线表述同步更新。

## Verification

- `AtlasIconTests`：五个枚举的路径、SVG 属性、装饰性 aria、`Size`/`CssClass`。
- `AtlasWorkspaceHostTests`/`TabOverflowTests`：溢出、关闭与折叠按钮渲染 SVG；可访问名称保留；inline/block 方向与折叠状态箭头正确。
- 公共 API 分析器通过 `PublicAPI.Unshipped` 校验。
- `tests/browser/ide-theme-fixture.html` 与 Showcase 真实浏览器检查图标外观。
- `dotnet build/test/format` 全量门禁通过。

## Supersession

新决策；不取代任何既有 ADR。与 ADR-0039 同批交付，但两者可独立回退。
