# ADR-0018：统一 Content Catalog 与运行时绑定

> 简体中文（权威） | [English mirror](../../en/adr/0018-unified-content-catalog-and-runtime-binding.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

- Status: Accepted
- Date: 2026-08-03
- Decision maker: Atlas Maintainer（批准实施方案并要求开始执行）
- Related: ADR-0004, ADR-0012, ADR-0013, ADR-0017, REQ-F-006~008,
  REQ-F-070, REQ-F-119~120, REQ-F-122, REQ-F-128, REQ-NF-006,
  DES-RND-001~005, DES-ERR-001

## 背景

Atlas 使用 `ContentReference(Kind, ResourceId)` 持久化内容身份，并使用
`DockItemId` 标识一个具体视图。布局恢复、Undo/Redo、同一业务资源的多个视图、缺失内容
placeholder 和跨 Group remount 都依赖这个边界。

现有 Blazor 投影有两条互斥路径。Host 的 `ContentTemplate` 非空时，它渲染全部 trusted
content，DI Content Registry 完全不参与；否则由 DI Registry 按 Kind 解析组件。Template 支持
普通 Razor binding，但鼓励应用维护中央 Kind switch。DI 路径支持模块化启动注册，但只支持窄的
`AtlasContentContext` 参数。两条路径都无法形成一个同时容纳 Host-local composition 和模块注册的
确定性 Catalog。

获取当前 `DynamicComponent.Instance`、直接写 Parameter 或调用 `StateHasChanged` 会绕过 Blazor
由父 Renderer 拥有的参数生命周期，并在 unmount、LRU 淘汰、跨 Group remount、close 和 Host
dispose 后保留不稳定的组件实例。重新公开 `Dictionary<string, object?>` 参数协议则会恢复 V1
已经移除的运行时绑定风险。

应用还需要 Panel 之间协作。比如“Panel A 清空 Panel B 的表格”在 Panel B 未挂载时仍然应有意义；
直接调用组件实例无法提供该生命周期保证。

## 决策

1. 保持 `ContentReference.Kind`、`ContentReference.ResourceId` 和 `DockItemId` 不变：
   - Kind 选择一个内容 Renderer；
   - Kind + ResourceId 标识应用资源或业务会话；
   - DockItemId 标识一个具体 View/Outlet。
   同一 Kind 和 ResourceId 可以对应多个视图。
2. 每个 `AtlasWorkspaceHost` 从以下来源构造一个 Effective Content Catalog：
   - 启动期 DI Registration（`AddAtlasContent<TComponent>(kind)`）；
   - Host-local 声明式 `AtlasContentRoute` Descriptor；
   - immutable Host-local 函数式 `AtlasContentMap`。
   它们是 Catalog Source，不是相互 fallback 的渲染管线。
3. 一个 Effective Catalog 中一个 Kind 只有一个 owner。启动期 DI 重复注册在 Host 或 per-Item
   Outlet 尚不存在时，于 composition-root 构造阶段失败。涉及 Route 或 Map 的冲突只使该 Kind
   无效，并生成确定性的安全诊断；无关 Kind 保持可用。不得通过注册顺序选择 winner，也不提供
   隐式 override。Host-local ownership 冲突使用新增的
   `AtlasHostErrorCode.ContentRegistrationConflict = 7` 诊断。
4. Catalog 只保存 Registration/Renderer Descriptor，不把组件实例、业务资源模型、per-view
   state、Callback、Delegate 或 Fragment 作为可独立查询的 Catalog 数据保存。但 Host-local
   Renderer Descriptor 可以在 closure 中捕获 Callback、Delegate、Fragment、父级 receiver 或
   local state。这些捕获是非持久化且由 Host 拥有的 Runtime Binding state，不是 Catalog 中的
   业务资源。
5. DI Registration 仍是 composition-root 启动能力。本 ADR 不定义 runtime
   Register/Unregister/Replace、plugin unload、assembly unload 或 hot replacement。
6. 声明式 Route 使用普通 Razor component syntax，因此参数和 Callback 由 Blazor 正常应用。
7. 函数式 Map 通过强类型 component-member expression 选择可写 Blazor Parameter，并从
   `AtlasContentContext` 计算值。普通值、`EventCallback`、`EventCallback<T>`、Delegate、
   `RenderFragment` 和 `RenderFragment<T>` 保持强类型。公共 API 不接受任意 parameter
   dictionary、字符串 parameter name 或裸组件实例。
8. 所有 Parameter 由所属 Blazor Renderer 通过正常 `SetParametersAsync` 生命周期应用。Atlas
   不公开 `Get<TComponent>()`、可变 Parameter access 或 `StateHasChanged`。
9. Trust Policy 先于 Catalog 解析。`SandboxedFrame` 保持最高优先级，DI、Route、Map、共享状态
   或组件命令都不能绕过。一次投影只执行一次 Resolver。Resolver exception 成为 per-outlet
   `ContentRenderFailed` failure，并阻止该次投影继续执行 Catalog 解析或 Binding Factory。
10. Host 将 Route/Map 变化暂存为一个 Source generation，在 Route 收集完成后原子提交；不把新
    Map 与旧 Route 混用。相同 structural Descriptor 的 value-only update 保留组件 identity，并提供
    完整 bound Parameter set。组件类型、Binding set、Route 声明或 Source owner 变化会递增 Renderer
    revision 并使 Outlet remount。Host-local Descriptor 可能捕获父组件、callback receiver、Delegate、
    Fragment 或 local state，因此受 Host ownership 管理。Outlet 释放 retired generation 后，Atlas
    释放其引用；Host dispose 释放剩余全部 Atlas-owned 引用。实际回收还要求应用/父组件释放其持有的
    旧 Map 或 Fragment。Outlet 保持 ADR-0012 的 lazy mount；remount 重新应用当前 Binding，不保留
    旧组件引用。
11. Panel 协作按语义 ownership 选择机制：
    - 明确 parent/child 交互使用 Parameter 和 `EventCallback<T>`；
    - 业务数据变化使用按 `WorkspaceId` 和/或 ResourceId 分区的应用自有 State/Coordinator；
    - mounted-only UI capability 将来可以使用按 DockItemId 寻址、单独设计的受限强类型 command
      port；
    - Atlas 不引入全局 string/object event bus。
    第一版实现验证共享状态路径，不公开组件 command API。
12. Snapshot、Persistence、Undo 和 Redo 继续只保存布局身份和 `ContentReference` 值。运行时
    Binding 和应用状态在 restore 后由应用 composition root 重建。
13. Kind 缺失或发生 Host-local 冲突时保留 Item、布局位置和 `ContentReference`；Outlet 显示
    安全 unavailable placeholder。Trust/Sandbox Resolver、Renderer、Callback 和 Parameter Factory
    failure 按 Outlet 隔离。UI 诊断只包含通用摘要和
    不透明 `DiagnosticId`；exception message、type 和 stack 只进入日志。每次独立失败生成新的
    DiagnosticId，对应 UI 与日志使用同一个值，Retry 后再次失败生成新值，且该值不编码 Workspace、
    Resource、Item、异常或其他敏感身份。
14. 在测试和 consumer Demo 完成迁移后删除尚未 shipped 的 `ContentTemplate` API。Atlas 在首个
    stable public API 前不长期维护两套内容投影合同。

## 备选方案

### 保留 ContentTemplate 并增加 Provider fallback

不采用。`RenderFragment<AtlasContentContext>` 无法表达是否处理了某 Kind；若向公共模板加入
continuation-style fallback，该 continuation 可能漏调、多调或延迟调用，导致 ownership 和 failure
semantics 不明确。

### Host-local Registration 覆盖 DI Registration

不采用。隐式优先级会隐藏无效注册，并使行为依赖来源顺序。显式 Override API 需要单独的替换和
迁移语义，初版 Catalog 不需要它。

### 获取已挂载组件并直接设置 Parameter

不采用。未挂载时组件不存在，且实例会随生命周期转换而变化。直接属性赋值无法保持正常的父级
Parameter 生命周期，并可能被下一次渲染覆盖。

### 公共 Dictionary<string, object?> 参数包

不采用。它把 Parameter name 和 type 延迟到运行时，削弱 trimming/AOT 分析，并重建 V1
failure mode。内部为 `DynamicComponent` 生成的 Map 只是强类型 Binding 的实现细节。

### 使用 Workspace Domain Event 作为应用 Event Bus

不采用。现有 Domain Event 描述 Atlas 布局变更。应用业务消息和状态不属于 Atlas.Core 或
Workspace History。

### 立即增加 Runtime Plugin Registry

不采用。线程安全替换、Outlet invalidation、state migration、unload、assembly lifetime 和
multi-Host isolation 需要独立 ADR。

## 影响

- Host-local Razor composition 与模块化 DI Registration 可以为不同 Kind 共存，并受同一个解析
  authority 管理。
- 函数式配置获得强类型外部 Parameter，而无需公开组件实例。
- 现有未 shipped 的 `ContentTemplate` API 将 source-breaking 删除；迁移时需要显式 Route
  Registration 或 `AtlasContentMap`。
- 应用拥有业务状态及其 Persistence。Atlas 提供 `WorkspaceId`、ResourceId 和 DockItemId 作为
  分区身份，但不持久化 State 对象。
- 目标 View 未挂载时仍可清空数据，因为操作修改 Shared State，而不是组件实例。
- 重复 DI ownership 在 composition 阶段确定性失败；Host-local Kind 冲突会明确暴露并按 Kind
  隔离，而不是被静默遮蔽。
- active Host-local Descriptor 可能在 retirement 或 Host dispose 前保留捕获对象；Atlas 在该边界
  释放其引用，但 application-owned Map/Fragment 引用可以延长对象生命周期。
- mounted-only component command API 不在本次实现范围内；公开前必须另行冻结 failure/lifetime
  contract。

## 验证

- Catalog 测试覆盖 DI-only、Route-only、Map-only、mixed-source、missing、composition-fatal
  duplicate DI 和 Host-local per-Kind conflict。
- Binding 测试覆盖普通值、Callback、Delegate、Fragment、重渲染值、正常 Parameter lifecycle、
  稳定 Item identity、Binding 删除、组件类型替换和完整 Parameter-set delivery。
- 架构测试拒绝公共任意 Parameter Dictionary 和裸组件实例访问。
- Public API contract 测试冻结 Route/Map/Binding 的名称、namespace、generic constraint、
  validation/default/replacement 语义、non-public constructor、自动
  `AtlasContentComponent.Content` 注入、`ContentRegistrationConflict = 7` 及 `ContentTemplate` 删除。
- Source-generation 测试覆盖 atomic Route/Map commit、失败收集、Route→Map/Map→Route 切换及
  structural-revision remount，且不出现瞬时 missing/conflict diagnostic。
- 生命周期测试覆盖未挂载内容、bounded KeepAlive LRU eviction、cross-Group remount、close 和
  Host dispose。
- Sandbox 测试证明 Resolver/Binder 无法绕过 SandboxedFrame、每次投影最多执行一次 Resolver，
  且 Resolver exception 不进入 Catalog 或 Binding Factory 求值。
- Diagnostic 测试证明一个不透明值关联每次失败的 UI 与日志，Retry 生成新值，且不编码敏感身份。
- 独立 NuGet consumer Demo 证明 Panel A 可以在 Table Panel 未挂载时清空应用状态，Table Panel
  挂载后读取已清空状态。
- 完成前必须通过 Public API baseline、solution build/test/format、Node gate、Release pack、
  isolated consumer restore/build/test/format 和真实浏览器 console 检查。

## 替代关系

取代 ADR-0004 中 `ContentTemplate` 优先、Provider 次之的内容解析合同，以及把启动期 DI Registry
描述为 runtime plugin registry 的表述。ADR-0004 的 Host ownership 和 reconciliation 决策保持
有效。本 ADR 扩展 ADR-0012 的内容生命周期/状态保持决策和 ADR-0013 的 trust boundary，不改变
这两项决策。
