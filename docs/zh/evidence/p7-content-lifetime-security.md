# P7 内容生命周期与安全验证证据

> 简体中文（历史证据，记录当时验证结果）；本文为唯一维护版本（ADR-0036）。


> 历史证据说明：承载本页浏览器验证的 `Atlas.Demo` 已按 ADR-0016 删除。以下结果记录对应提交
> 当时的已执行证据；当前 solution 不再提供该浏览器入口。

- 日期：2026-07-31
- 分支：`chore/dev-gate-001-baseline`
- 状态：Verified
- 入口：P3 Verified；DEV-GATE-003/004 Closed（ADR-0012/0013）
- 需求：REQ-F-027~029、REQ-F-031~032、REQ-F-070、REQ-F-120、REQ-F-128、
  REQ-F-147、REQ-NF-001、REQ-NF-003、REQ-NF-006~007
- 设计：DES-ARCH-003~004、DES-INT-001~002、DES-NFR-001、DES-NFR-003~004、
  DES-PRJ-002~003、DES-RND-005、DES-RND-007

## Accepted boundaries

- ADR-0012：跨 Group 保持的是 `DockItemId`、`ContentReference` 和可恢复的外置业务/视图状态，
  不承诺任意 Blazor component instance identity。
- ADR-0013：TrustedComponent 是同 DOM/DI 的协作合同，只提供 ErrorBoundary；恶意脚本隔离只由
  显式 SandboxedFrame 承担。
- Workspace-wide keep-alive、任意 sandbox token、P6 schema 和 P7 之外的协议均未引入。

## Automated evidence

| Layer | Result | Evidence |
|-------|--------|----------|
| Core | 262 passed | solution `dotnet test --no-build --no-restore`；Atlas.Core 仍无 Blazor/JS 依赖 |
| Blazor | 41 passed | Default unmount、cross-Group remount/state restore、bounded LRU、same-Group reuse、hidden/aria-hidden/inert、close/eviction disposal、fixed iframe policy、PublicAPI baseline |
| Node/controller | 16 passed | sandbox source/origin/version/workspace/item/generation/size/command fail-closed；既有 resize/keyboard/drag regression |
| Protocol | passed | `sandbox-protocol-v1.json` 同时由 TypeScript 与 C# tests 读取；DOM metadata manifest 冻结 frame attributes |
| Build | passed | Node.js 24.14.0；npm 11.6.2；`npm ci`、typecheck、test:js、build:js；solution restore/build/format |
| Pack | passed | Core/Blazor Release nupkg；Blazor 包含 `atlas.css`、`atlas.js` 与 source map |

`npm ci` audit 报告 0 vulnerabilities。已知非阻断 warning 仅为 repository 已接受的 .NET 6 EOL
SDK warning 与两个 dev nupkg 缺少 readme。

## Real browser evidence

验证入口：Atlas.Demo `http://127.0.0.1:8767/`，通过 Codex in-app browser 的实际 Pointer/DOM/
iframe browsing context 执行。

### Lifetime and focus

1. 激活 `first`，输入 `preserved-state`，记录 instance
   `2ca0543a0fce43c0922258d8505f8393`。
2. 切换到 `second` 后，`first` outlet 同时具有 `hidden`、`aria-hidden="true"`、`inert`；
   `document.activeElement` 不在 hidden outlet 内。
3. 再次激活 `first`，instance ID 未变化且输入仍为 `preserved-state`，证明同 Group keep-alive。
4. 容量为 2 时依次激活 sandbox 和 second：outlet 从 `[first, sandbox]` 变为
   `[second, sandbox]`；second 新 instance
   `ce109b7d0a7743c587bd50c7e86b5432` 与旧 instance
   `fa0631fc10b14dee839bdec9d058ad8a` 不同，证明 bounded eviction/remount。

### Security boundary

- 实际 iframe 属性为 `sandbox="allow-scripts"`、`referrerpolicy="no-referrer"`，没有
  `allow-same-origin`；controller 接受严格 ready message 后设置 `data-atlas-sandbox-ready="true"`。
- sandbox child 尝试读取 parent DOM，浏览器抛出 `SecurityError`，child 记录
  `data-parent-dom-blocked="true"`。
- child 发送 allowlist 外 command，root 记录诊断 reason `command`；frame 保持 ready。
- child 发送大于 4096 bytes 的消息，root 记录诊断 reason `size`；frame 与 Workspace 继续工作。
- 浏览器 console 的 warn/error 列表为空。

## Phase Exit decision

Entry Gate、行为测试、API/protocol freeze、真实浏览器生命周期与安全 E2E、全量 build/test/format 和
双包 pack 全部通过。公共安全声明与实际边界一致，因此 P7 标记为 Verified。DEV-GATE-006 保持
Evidence Ready，未擅自关闭；P8 范围未提前实现。
