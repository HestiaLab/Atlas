# Atlas 安全文档

> 简体中文（权威） | [English mirror](../../en/security/README.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

本目录记录 Atlas 的内容信任模型和安全边界。仓库级漏洞报告、受支持版本和依赖策略见 [`SECURITY.md`](../../../SECURITY.md)。

## 当前文档

- [Content Threat Model](./content-threat-model.md)：Trusted Blazor content、SandboxedFrame、消息协议、攻击面和验证责任。
- [ADR-0013](../../zh/adr/0013-content-trust-and-sandboxed-frame.md)：内容信任与 SandboxedFrame 的已接受架构决策。

## 必须保持的边界

1. `TrustedComponent` 与 Atlas 共享 document、进程和 DI scope，不是安全沙箱。
2. 只有受限制的 `SandboxedFrame` 路径可以承担恶意内容隔离合同。
3. iframe sandbox、origin/source、protocol version、Workspace/Item identity、payload size 和 command allowlist 必须验证。
4. JavaScript interop 和持久化输入都视为不可信输入。
5. XML parser 必须禁用 DTD、外部实体和外部资源解析，并执行 size/depth/count limits。
6. 错误 UI 不向 DOM 暴露 exception message、stack trace 或敏感 `ResourceId`。
7. 新增权限、跨 frame command、共享 global registry 或新的安全承诺需要 threat-model 更新和架构审查。

## 修改安全相关代码时

依次检查：

1. [SRS 非功能需求](../SRS.md)
2. [HLD §8](../HLD.md#8-错误处理与质量属性)
3. [ADR-0013](../../zh/adr/0013-content-trust-and-sandboxed-frame.md)
4. [Content Threat Model](./content-threat-model.md)
5. `tests/Atlas.Blazor.Tests/SandboxedFrameTests.cs`
6. `tests/browser/p7-sandbox-fixture.html`

安全测试通过只说明已覆盖的合同成立，不应把 TrustedComponent 描述成已隔离的不可信内容。