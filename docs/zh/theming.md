# Atlas 样式与主题定制

> [English](../en/theming.md) | 简体中文

Atlas 以单个样式表（`_content/Atlas.Blazor/atlas-v2/atlas.css`）分发，并通过文档化的、以 `--atlas-` 为前缀的 CSS 自定义属性暴露其视觉样式（REQ-NF-009、ADR-0023）。你通过覆盖这些变量定制外观——不需要 `!important`，不需要内部选择器。

## 1. 链接样式表

```html
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/atlas.css" />
```

这会加载默认主题。默认值使用 CSS 系统颜色（`Highlight`、`Canvas`、`CanvasText`），由浏览器解析为平台界面颜色——例如在 Windows/Chrome 上 `Highlight` 解析为系统强调色，`Canvas`/`CanvasText` 解析为浅色模式的白底黑字。只有当浏览器配置为跟随系统明暗模式时，它们才会随之变化；具体解析值取决于浏览器与操作系统设置。

## 2. 覆盖变量（推荐）

在你的样式表中声明覆盖，**放在** Atlas 链接之后。默认值定义在 `.atlas-v2-workspace` 元素上，因此任何匹配工作区容器（或其祖先）的选择器都会生效。

### 案例：修改强调色

```css
/* app.css —— 在 atlas.css 之后加载 */
.atlas-v2-workspace {
  --atlas-accent: #d33;
}
```

放置区域高亮、焦点环与拖拽预览边框现在使用 `#d33`。

### 案例：深色表面 + 更宽的分割条

```css
.atlas-v2-workspace {
  --atlas-accent: #4c9aff;
  --atlas-surface: #1e1e1e;
  --atlas-text: #d4d4d4;
  --atlas-splitter-size: 0.375rem;
  --atlas-splitter-opacity: 0.35;
  --atlas-splitter-opacity-hover: 0.6;
}
```

### 案例：只重设某个实例

```css
#my-dock .atlas-v2-workspace {
  --atlas-accent: #2e7d32;
}
```

## 3. 变量契约

以下清单是公共 API：未来版本可以新增变量或调整默认值，但绝不删除。

### 颜色

| 变量 | 默认值 | 使用位置 |
| --- | --- | --- |
| `--atlas-accent` | `Highlight` | 放置区域高亮、焦点环、拖拽预览边框 |
| `--atlas-surface` | `Canvas` | 拖拽预览背景 |
| `--atlas-text` | `CanvasText` | 拖拽预览标题文字 |
| `--atlas-drag-preview-shadow` | `rgb(0 0 0 / 28%)` | 拖拽预览阴影 |

### 尺寸

| 变量 | 默认值 | 使用位置 |
| --- | --- | --- |
| `--atlas-splitter-size` | `0.25rem` | 分割条厚度 |
| `--atlas-splitter-hit-area` | `0.25rem` | 分割条指针热区扩展 |
| `--atlas-chrome-height` | `2rem` | Tab 条 / 面板标题栏 / 空区域高度 |
| `--atlas-gap` | `0.375rem` | 拖拽预览内部间距 |
| `--atlas-padding-compact` | `0.375rem 0.625rem` | 拖拽预览内边距 |
| `--atlas-radius` | `0.25rem` | 常规圆角 |
| `--atlas-radius-sm` | `0.125rem` | 小圆角（区域高亮） |
| `--atlas-border-width` | `1px` | 细边框（toolbar 分隔线、拖拽预览） |
| `--atlas-focus-ring-width` | `2px` | 焦点环与放置区域 outline 宽度 |
| `--atlas-drag-preview-max-width` | `min(20rem, calc(100vw - 1rem))` | 拖拽预览上限 |

### 层级

| 变量 | 默认值 |
| --- | --- |
| `--atlas-layer-content` | `0` |
| `--atlas-layer-chrome` | `10` |
| `--atlas-layer-drag` | `30` |

### 透明度

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

## 4. 深度定制（复制样式表）

如果变量覆盖不够，将 `atlas.css` 复制到你的项目中，修改后链接你的副本而非包内资产：

```html
<link rel="stylesheet" href="css/atlas.css" />
```

这提供对布局与结构的完全控制，但你的副本将停止跟随 Atlas 升级——未来版本可能改变类名或结构。除非需要结构性修改，否则优先使用变量覆盖。

## 5. 兼容性说明

- 默认值与 ADR-0023 之前的基线逐像素一致。
- 变量作用域为 `.atlas-v2-workspace`；在任意祖先上的覆盖通过 CSS 继承传播。
- `currentColor` 文本/边框值未 token 化：它们有意跟随宿主应用的文字颜色。
- 删除变量是破坏性变更；契约是增量的。
