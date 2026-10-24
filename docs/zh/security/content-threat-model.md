# Atlas Content Threat Model

> 简体中文（权威） | [English mirror](../../en/security/content-threat-model.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

## Scope

本模型覆盖 Atlas.Blazor P7 的两种 content mode。Atlas.Core 不执行内容代码，也不依赖浏览器安全 API。

## Trust boundaries

| Mode | Trust | Boundary | Security claim |
|------|-------|----------|----------------|
| TrustedComponent | 应用信任 | 同 document、进程与 DI scope | ErrorBoundary 限制渲染失败；不隔离恶意 DOM/数据访问 |
| SandboxedFrame | 内容不受信任 | sandboxed iframe、opaque origin、versioned message seam | 阻止 parent/peer DOM 访问；只接受已验证 allowlisted messages |

## Protected assets

- parent 与其他 panel 的 DOM、输入和应用数据；
- WorkspaceId、DockItemId 与 content identity 的完整性；
- root controller、layout operation 与 focus/resize 协作的可用性；
- diagnostic channel 不泄漏 frame payload 或业务内容。

## Threats and controls

| Threat | Control | Failure semantics |
|--------|---------|-------------------|
| Frame 读取 parent/peer DOM | `sandbox="allow-scripts"`，不授予 `allow-same-origin` | 浏览器阻止访问；只影响 frame |
| sibling/parent 伪造 message | `event.source === iframe.contentWindow` | 忽略并记录 category/diagnostic ID |
| origin confusion | sandbox opaque origin 必须为 `null`；source identity 为主要绑定 | fail closed |
| stale/cross-Host identity | 精确匹配 protocol/workspace/item/generation | fail closed，不触发 operation |
| unknown command / malformed payload | command allowlist、结构验证 | 忽略；不抛出未捕获异常 |
| oversized payload / message flood | UTF-8 byte limit；每 frame 最小处理面 | 超限忽略；不记录 payload |
| frame 崩溃或拒绝响应 | per-outlet failure boundary/timeout degradation | 对应 outlet 降级，Workspace 继续工作 |
| sandbox permission escalation | public API 不接受任意 sandbox token | 固定 deny-by-default 权限集 |
| 网络数据泄漏 | 独立 origin、CSP、认证和网络策略由 consumer 负责 | 不属于 Atlas parent-side 保证 |

## Explicit non-goals

- TrustedComponent 的恶意代码隔离；
- 对 frame 服务端、CSP 响应头、认证或网络 egress 的控制；
- 跨 Group 保持任意 Blazor component instance identity；
- 通过 ErrorBoundary 恢复已损坏的第三方 component local state。

## Required evidence

协议 golden fixture、Node adversarial tests、bUnit markup/lifecycle tests，以及受支持真实浏览器中的
parent/peer DOM denial、spoof rejection、frame failure containment 和无未捕获异常证据。缺少浏览器证据时
P7 不得标记 Verified。
