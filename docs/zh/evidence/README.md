# 验证证据索引

> [English](../../en/evidence/README.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


本目录保存 Atlas 在特定阶段、分支、构建产物和环境中的验证记录。Evidence 用于审计“当时验证了什么”，不等同于当前分支持续成立的测试结果。

## 阶段证据

| 文档 | 范围 |
|------|------|
| [Content Catalog and Runtime Binding](./content-catalog-runtime-binding.md) | 统一 Content Catalog（DI + Route + Map）、类型化绑定、跨 Panel 协作、interaction attach 重试 |
| [P5 Drag and Dock](./p5-drag-and-dock.md) | 拖拽、投放、交互协议和相关验证 |
| [P6 Persistence](./p6-persistence.md) | XML、restore、preset 和持久化验证 |
| [P7 Content Lifetime and Security](./p7-content-lifetime-security.md) | 内容生命周期、SandboxedFrame 和安全边界 |
| [P8 Requirement Audit](./p8-requirement-audit.md) | 正式需求实施状态审计 |
| [P8 System Hardening](./p8-system-hardening.md) | 系统级 hardening、可访问性和边界验证 |

## Consumer 证据

- [Atlas Demo Consumer Verification](./atlas-demo-consumer-verification.md)：独立 NuGet consumer 的 pack、restore、build、test 和运行边界。

## 使用规则

1. 先检查证据记录的日期、branch/commit、package、bundle 和环境。
2. 当前状态以实际代码、当前测试输出和 [需求追溯矩阵](../requirements-traceability.md) 为准。
3. 历史浏览器截图或人工操作不能替代当前自动化测试。
4. 新 evidence 必须记录实际命令、输出、环境和未覆盖限制。
5. 失败或未执行的检查必须明确写出，不能用计划结果代替。
6. 涉及 JavaScript 的证据应确认使用的是当前 `npm run build:js` 产物；涉及 Demo 的证据应确认没有复用旧 NuGet/browser cache。

测试层和复验方式见 [测试指南](../../zh/guides/testing-guide.md)。