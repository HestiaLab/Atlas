# ADR-0013：Content Trust 与 SandboxedFrame 安全边界

- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确批准 P7 方案）
- 关联：REQ-NF-006、DES-NFR-004、OI-006、DEV-GATE-004

## Context

Blazor `RenderFragment`/`DynamicComponent` 与 Atlas 共享 document、进程和 DI scope。`ErrorBoundary`
可以限制渲染异常影响，但不能阻止可信组件主动读取其他 DOM 或应用共享数据。把该路径称为安全沙箱
会产生错误的产品承诺。

## Decision

- `TrustedComponent` 保持默认 content mode，提供错误隔离但明确不构成恶意脚本安全边界。
- 不受信任内容必须显式使用 `SandboxedFrame`。Frame 使用独立 browsing context、
  `sandbox="allow-scripts"`，不授予 `allow-same-origin`、top navigation、popup、form 或 parent DOM 能力。
- REQ-NF-006 的跨面板恶意脚本隔离 AC 适用于 `SandboxedFrame`；TrustedComponent 只承担异常隔离 AC。
- Frame 内容应由 consumer 部署在独立 origin 并配置 CSP；Atlas 不宣称能从 parent 强制第三方响应 CSP。
- C#/TypeScript protocol 在实现前冻结为 version 1，并提供 golden fixture。接收方必须验证：
  `event.source`、opaque origin（`null`）、protocol version、Workspace/Item identity、payload byte limit
  与 command allowlist。未知、伪造、超限或 stale 消息 fail closed，忽略并产生不含 payload 的诊断。
- Frame 崩溃、拒绝响应或协议错误只降级对应 outlet，不回滚 Core operation、不影响其他内容。
- `SandboxedFrame` public API 只暴露必要的 source/title/message-size 配置；不得开放任意 sandbox token。

## Alternatives

- 强制所有内容进入 iframe：破坏任意 Blazor component composition 与既有 TrustedComponent 合同。
- 把 ErrorBoundary 当作安全沙箱：无法阻止同 DOM 主动访问。
- 允许 consumer 任意添加 sandbox token：容易组合出 `allow-scripts + allow-same-origin` 的逃逸边界。

## Consequences

Atlas 明确区分协作型可信组件和安全隔离型 frame。安全声明只覆盖 Atlas 控制的 frame seam；网络、
认证、frame 服务端与 CSP 配置仍由应用负责。

## Verification

- threat model 审查与 public API baseline。
- TypeScript golden/protocol tests 覆盖 source/origin/identity/version/size/allowlist/stale rejection。
- bUnit 覆盖固定 sandbox/referrer policy 与无危险权限。
- 真实浏览器 E2E 验证 frame 不能读取 parent/peer DOM，伪造消息被拒绝且其他 outlet 可继续工作。

## Supersession

无。新增 frame capability、消息 command、协议版本或 sandbox 权限需要 superseding ADR。
