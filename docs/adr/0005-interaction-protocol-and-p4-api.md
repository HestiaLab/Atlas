# ADR-0005：Interaction Protocol v1 与 P4 公共 API

- 状态：Accepted
- 日期：2026-07-31
- 决策者：维护者（已授权本阶段门禁与 API 决策）

## Context

P4 需要在 Blazor Host 与浏览器端控制器之间建立 splitter pointer、keyboard command、
focus 和生命周期协议。HLD §10.2、§10.3、§10.7 与 §10.8 要求公共 API、跨语言协议和
失败语义在实现前冻结，同时保持高频 pointer preview 不进入 .NET。

P3 已提供 workspace projection、稳定的 Host/Item DOM identity、content resolution、
Changed 订阅和 ownership/disposal 基础。Core 已提供 `ResizeSplitOperation` 与
`SplitLayoutResolver`，但尚无表达 splitter 双击等分意图的 operation。

## Decision

### Interaction Protocol v1

`src/Atlas.Blazor/ClientScripts/protocol.ts` 是 v1 协议的规范来源。控制器 identity 包含
`protocolVersion`、`hostId`、`workspaceId` 和 `generation`。协议冻结以下消息：

- `ResizeSplitCommit`：一次终止性 resize 提交；
- `KeyboardCommandIntent`：一次语义化键盘命令；
- root controller 的 `attach`、`update` 和 `dispose` 生命周期。

未知版本、非法字段或已过期 generation 必须被拒绝，不得改变 workspace。

### Controller ownership

每个 interactive Host 拥有一个 root controller。控制器使用 root event delegation；
pointer move 只更新浏览器 preview，产生零次 .NET 调用，pointer up/cancel 最多产生一次
终止性 callback。模块级注册表只用于拒绝同一 workspace 的重复 attachment；所有交互
状态均属于 controller instance，dispose 后 callback 数量必须保持为零。

### P4 Blazor API

冻结以下 additive API：

- `AtlasWorkspaceHost.EnableInteractions`，默认 `true`；
- `AtlasWorkspaceHost.SplitterOptions`；
- `AtlasWorkspaceHost.KeyboardCommands`；
- `AtlasWorkspaceHost.SplitterPresentation`，供 programmatic mode 解析显示属性；
- `AtlasSplitDefinition.IsLocked`、`SplitterClass`、
  `CanCollapseFirst`、`CanCollapseSecond`；
- `AtlasSplitterOptions`、`AtlasSplitterPresentation`；
- `AtlasKeyboardCommand`、`AtlasKeyboardChord`、
  `AtlasKeyboardBinding`、`AtlasKeyboardCommandMap`。

自定义 splitter class 仅接受 CSS class token，不接受任意 markup 或脚本。

### Core equalize operation

新增 `EqualizeSplitOperation`，并将 `LayoutOperationKind.EqualizeSplit` 固定为数值 `17`。
它遵循 operation engine 的成功、NoChange、typed failure、undo/redo 与事件合同。

### Keyboard semantics

默认命令使用 `KeyboardEvent.code`：

- `Control+Tab` / `Control+Shift+Tab`：下一个/上一个 tab；
- `Control+KeyW` / `Control+F4`：关闭 active document；
- `Alt+Arrow*`：方向性 focus navigation。

可编辑目标默认不拦截命令，除非 binding 显式标记为 global。Splitter 的
Arrow/Home/End 在 TypeScript 中预览，并在 keyup 或 debounce 终止点提交一次，
从而形成一个 history entry。

### Scope boundary

P4 不实现 drag/drop、默认 docking policy、持久化 schema 或新的 content
lifetime/security 能力。`DEV-GATE-002` 保持 Open，直至 P5 需要其证据。

## Alternatives

- 每次 pointer move 调用 .NET：违反 `REQ-NF-004` 与 DES-INT-004。
- 直接把 DOM event 传入 Core：破坏 Core/Blazor 双包边界。
- 用多个局部 controller：增加 listener 泄漏与跨 Host 串扰风险。
- 用连续 resize operation 模拟 preview：污染 history，并违反单次终止提交语义。

## Consequences

- TypeScript 负责高频输入与瞬时 preview，Core 仍是唯一持久状态 authority。
- P4 引入 additive public API，必须同步 PublicAPI baseline。
- Host 必须在首次 render 后 attach，并在参数变化时 update，在 dispose 时释放 controller。
- declarative/programmatic 两种模式共享同一 presentation 与交互协议。

## Verification

- TypeScript 单元测试覆盖协议校验、pointer preview、单次 commit、cancel 和 dispose。
- Core 单元测试覆盖 equalize 的成功、NoChange、typed failure 与 undo/redo。
- bUnit 覆盖 attach/update/dispose、锁定 splitter、presentation、keyboard/focus callback
  以及多 Host 隔离。
- 浏览器级测试证明 pointermove 的 .NET 调用次数为零、终止提交一次且 resize 后 DOM
  与 workspace 一致。
- P4 Phase Exit Gate 运行完整 restore、npm、build、test、format 和 Release pack。

## Supersession

修改协议版本、消息 union、默认键盘语义或上述公共 API 时，必须由新的 Accepted ADR
取代本 ADR。

键盘 resize 的提交频率、input kind 与 Core history 合并条款已由
[ADR-0006](./0006-keyboard-resize-history-coalescing.md) 取代。
