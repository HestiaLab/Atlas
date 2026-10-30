---
name: 基础设施请求
about: 提议构建、CI、NuGet 打包、测试或工具维护
title: "infra: "
labels: infrastructure
assignees: ""
---

## 范围

描述构建、CI、打包、测试或工具变更。

## 受影响区域

- [ ] 构建（MSBuild / dotnet CLI）
- [ ] CI / GitHub Actions
- [ ] NuGet 打包
- [ ] 测试（xUnit v3 / Microsoft.Testing.Platform）
- [ ] TypeScript / JS interop 工具链
- [ ] 文档
- [ ] 依赖（NuGet / npm）

## 动机

描述维护风险、工作流缺口或质量问题。

## 验证

列出应能证明变更有效的检查。

```sh
dotnet build
dotnet test
```

## 风险或回滚

描述风险、兼容性顾虑或回滚路径。

## 兼容性

描述对受支持 TFM、浏览器或下游消费者的任何影响。
