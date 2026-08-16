# 开发 Atlas：一个完整示例

> [English](../../en/guides/developing-atlas.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本指南端到端走一遍 Atlas 的真实改动：选择需求、找到所属设计、修改代码、并用测试证明。它与[开发指南](./development-guide.md)（解释规则）互补，展示一个完整改动实际长什么样。场景做了简化，但严格遵循仓库期望的工作流。

## 1. 从需求开始，而不是从代码开始

每个功能改动都必须追溯到 [`docs/en/SRS.md`](../SRS.md) 中的 `REQ-F-*` 或 `REQ-NF-*` 需求。假设你需要修改关闭文档标签页后如何选择下一个标签页。

按此顺序阅读：

```text
SRS 需求（REQ-F-xxx）
→ 需求追溯（当前状态）
→ HLD 设计条目（DES-xxx）
→ 相关 ADR
→ 所属代码和测试
```

对于关闭/选择改动，`docs/requirements-traceability.md` 指向 `CloseItemOperationTests` 和 `Atlas.Core/Operations/Engine` 中的所属 planner。关闭选择的 HLD 条目是 §4 中的 `DES-OP-001` / `DES-OP-004`。

## 2. 找到所属模块

Atlas 强制分层所有权：

- `Atlas.Core` — 领域模型、操作、不变量、持久化。无 Blazor、无 JS。
- `Atlas.Blazor` — 渲染、内容承载、CSS、JS interop。依赖 Core。
- `wubing7755/DbcView` — 独立的 NuGet consumer showcase，框架从不引用它。

关闭/选择的行为改动属于 `Atlas.Core`。planner 位于 `src/Atlas.Core/Operations/Engine/`，操作请求对象位于 `src/Atlas.Core/Operations/`。

## 3. 先写失败测试

仓库对行为改动使用 TDD。在现有测试旁添加回归测试：

```csharp
// tests/Atlas.Core.Tests/Operations/CloseItemOperationTests.cs
[Fact]
public async Task Closing_active_document_activates_the_adjacent_tab()
{
    // Arrange: 工作区含标签页 [a, b, c]，a 为激活
    // Act: 关闭 a
    // Assert: b 现在被选中，revision 恰好前进一次
}
```

运行并确认失败：

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 4. 做出最小所属改动

修改 planner，使候选快照选择相邻标签页。把改动限制在 `Atlas.Core` 内，暂不触碰渲染：

```csharp
// Atlas.Core/Operations/Engine/...Planner.cs
var next = currentIndex + 1 < count ? items[currentIndex + 1] : items[currentIndex - 1];
```

重跑定向测试直到通过：

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 5. 运行完整本地 Gate

标准 Gate 与 CI 运行同一组命令（本地为 Debug）：

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build --no-restore
dotnet format Atlas.sln --verify-no-changes --no-restore
```

如果你改动 `ClientScripts/*.ts`，不要跳过 `npm run build:js` —— csproj 不会自动调用 npm，不重建 bundle 打包会静默带上旧 JavaScript。

## 6. 在同一提交更新追溯

如果改动影响实现或验证状态，在同一提交更新 `docs/en/requirements-traceability.md`：该需求的行必须列出新的测试证据，并保持状态诚实（每个 AC 都有证据前为 `In Progress`，之后才能 `Verified`）。

## 7. 当改动跨边界

假设行为还需要新的 Razor 投影。那么：

1. 在 `tests/Atlas.Blazor.Tests` 添加断言 DOM 合同的 bUnit 测试。
2. 如果 selector 变化，同步 `ClientScripts/dom-metadata-manifest.json`。
3. 运行 Blazor 测试：
   ```sh
   dotnet test tests/Atlas.Blazor.Tests/Atlas.Blazor.Tests.csproj
   ```
4. 为 bUnit 无法证明的指针几何或 RenderTree diff 行为添加真实浏览器验证（见测试指南 §5）。

## 8. 公共 API 改动需要更多

如果你新增或修改公共符号：

- 更新对应的 `PublicAPI.Unshipped.txt` baseline。
- 添加编译时或行为 contract test。
- 检查是否需要 ADR（公共 API、包结构、协议、持久化兼容性或安全边界）。
- 更新受影响的 SRS/HLD/ADR/追溯段落。
- 打包单个包（ADR-0019）并通过 consumer showcase 验证：

```sh
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages
```

独立的 `wubing7755/DbcView` 仓库随后对打包的包（或已发布的 nuget.org 版本）执行 restore/build/test/format。

## 9. 用 Conventional Commits 提交

```text
feat(core): activate adjacent tab when closing an active document
fix(splitter): clamp position within container bounds
docs(traceability): update REQ-F-029 evidence for adjacent selection
```

不要把无关清理混入同一提交。

## 10. 应避免的常见错误

- 只读 HLD 计划名称而不检查当前代码。
- 在 Blazor 或 TypeScript 中维护第二份 committed 布局状态。
- 绕过操作 planner 修补单个失败路径。
- 把 bUnit 通过当作真实浏览器行为证明。
- 修改公共 API 却忘记 PublicAPI baseline。
- 修改实现却把追溯留在旧状态。

## 接下来去哪里

- 本工作流背后的规则：[开发指南](./development-guide.md)
- 测试分层和命令：[测试指南](./testing-guide.md)
- 仓库规则：[`CONTRIBUTING.md`](../../../CONTRIBUTING.md)
- 架构：[HLD](../HLD.md)
