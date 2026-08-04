# 内容目录与运行时绑定验证证据

- 日期：2026-08-03
- 分支：`feature/content-catalog`
- 提交：`44502b4`（冻结合同）→ `ca100bc`（catalog + routes）→
  `c1b549b`（类型化函数式 map）→ `f6e5d11`（移除 template）→
  `6649436`（Demo 跨 Panel 协作）→ `523a394`（interaction attach 重试修复）
- 包版本：`0.0.0-dev`
- 范围：`src/Atlas.Blazor` 与 `samples/Atlas.Demo` 中的统一 Content Catalog
  （DI + Route + Map）、运行时绑定、跨 Panel 应用状态协作与 interaction
  attach 可靠性

## 需求与设计追踪

| 需求集 | 设计 | 当前证据 |
|---|---|---|
| REQ-F-006~008（内容标识关联；运行时绑定） | DES-RND-001~003 | DI `AddAtlasContent<T>(kind)`、Host-local `AtlasContentRoute`、不可变 `AtlasContentMap`；针对值、`EventCallback`/`EventCallback<T>`、委托、片段的类型化绑定 |
| REQ-F-119~120（缺失/冲突与 per-outlet 失败隔离） | DES-RND-004、DES-ERR-001 | 确定性的 per-Kind `ContentRegistrationConflict`；不相关 Kind 继续渲染；仅含 DiagnosticId 的通用安全失败 |
| REQ-F-122（composition-fatal DI 重复与 host-local 冲突区分） | DES-RND-005 | 重复 startup DI 在 composition root 失败；Host-local Route/Map 冲突为 per-Kind 诊断 |
| REQ-F-128（安全错误摘要） | DES-ERR-001 | 用户可见标记中不含异常类型/消息/堆栈或敏感值 |
| 跨 Panel 协调（应用拥有的状态） | ADR-0018 | Panel A（Route）clear 回调修改父组件拥有的集合；未挂载的 Panel B（Map）在重新挂载时读到清空后的值 |

## 自动化门禁

| 门禁 | 结果 |
|---|---|
| `Atlas.sln` restore/build | 通过；0 警告 0 错误 |
| `Atlas.sln` test | Passed; 362/362 (Core 266 + Blazor 96) |
| `Atlas.sln` format verify | 通过（`dotnet format --verify-no-changes` exit 0） |
| TypeScript typecheck + bundle | 通过；`wwwroot/atlas-v2/atlas.js` 重建（40,669 字节，含 `resolveHostElement` 重试与幂等 attach） |
| Core/Blazor Release pack | 通过 |
| `Atlas.Demo.sln` restore/build/test/format | 通过；12/12 测试；预期的 NETSDK1138（批准目标 net6.0） |
| Demo package-boundary tests | 通过；2/2（无源码 ProjectReference） |
| `git diff --check` | 通过 |

包 SHA-256 值：

- `Atlas.Core.0.0.0-dev.nupkg`：`EABA206DE7D4516D0291C7A6CEDF7F25DA15E18B098F8E35147BF8B5917CBE4B`
- `Atlas.Blazor.0.0.0-dev.nupkg`：`CD78B1928ABF16AD81EB455482B6A48AA8B086109DA827D8EFB7E972EC01E2E6`

## 纯包消费

`samples/Atlas.Demo` 通过本地 `NuGet.Config` + `.packages` 缓存从
`artifacts/packages` 还原；每次框架变更后先删除 `atlas.blazor`/`atlas.core`
缓存条目再还原。

- `Atlas.Demo.csproj` 不含 `ProjectReference`（grep 计数为 0）。
- `obj/project.assets.json` 报告无项目引用；唯一的 Atlas 库是
  `Atlas.Blazor/0.0.0-dev` 与 `Atlas.Core/0.0.0-dev`。
- 打包的 `Atlas.Blazor` nupkg 含 `lib/net6.0/Atlas.Blazor.dll` 与
  `staticwebassets/atlas-v2/atlas.js`（+ `.map`、`.css`）且为当前 bundle。

## 真实浏览器验证

内置 Chromium 浏览器访问由 `dotnet run --urls http://127.0.0.1:527x` 服务的
Demo WASM 应用（每次运行换端口以避免陈旧 asset/bundle 缓存）。

| 检查 | 证据 |
|---|---|
| 初次加载渲染工作区 | 完整 workspace DOM；存在 `data-atlas-host-id` |
| 无 host-error banner | 多次导航中 `.atlas-v2-host-error` 均不存在 |
| Panel B 未挂载时 Panel A Clear | 点击 Clear 后显示 "0 products" |
| Panel B 以清空状态挂载 | `products-table` 渲染 0 行，`products-empty` 可见 |
| 控制台 | 验证运行中零 JS 错误 |
| Interaction attach | `ATLAS-INTERACTION-ATTACH-FAILED` 不再出现；修复后在多次刷新中均未观察到错误 banner（修复前稳定复现 `Cannot read properties of null (reading 'ownerDocument')` / `ATLAS-INTERACTION-ATTACH-REJECTED`） |

## 未覆盖限制

- Splitter 拖拽验证使用合成 pointer 事件；controller 已 attach（无
  host-error banner、`data-atlas-reduced-motion` 已投影），但本次运行未测量
  物理指针拖拽提交。
- Phase 6 独立审查期间补充的 F-128 per-occurrence DiagnosticId 与 F-122
  重复 DI composition 测试由 bUnit 覆盖（`ContentLifecycleTests`、
  `ContentCatalogTests`）；错误 UI 渲染未在真实浏览器中单独复验（常规
  Demo 流程除外）。
- `NETSDK1138`（net6.0 EOL）警告符合预期且保持不变。
- 已删除 Demo host 的历史 P7/P8 浏览器证据仍然有效，本记录不重写它们。
