# ADR-0037：打开条目时可选标识符与工作区生成 GUID

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-24
- **决策人**：Atlas Maintainer（2026-10-24 批准）
- **相关**：ADR-0002、ADR-0029；REQ-F-028 AC1

## 背景

REQ-F-028 AC1 要求开发者在调用打开标签页接口且未指定标识时由系统生成全局
唯一标识。当前 Core 只提供 `OpenItemOperation(DockItem, ...)`：`DockItem`
构造强制要求非默认 `DockItemId`，命令门面同样只接受已构造条目，因此调用方
必须先自行生成标识。验收报告 DEF-02 将该能力标记为 No Evidence。

直接给 `OpenItemOperation` 增加可选标识符参数会破坏已冻结的构造函数与
`Item` 属性契约；仅让调用方自行生成 GUID 也不能满足“由系统生成并从结果
取得标识”的要求。

## 决策

新增增量公共表面：

1. `OpenItemRequest`：描述条目属性，`Id` 为可空 `DockItemId?`；构造时复用
   `DockItem` 的校验规则（种类、能力组合、内容与 document state），避免两套
   规则漂移。
2. `OpenItemRequestOperation`：映射到既有 `LayoutOperationKind.OpenItem`，
   规划阶段解析标识符：调用方提供非默认标识时原样使用；否则由工作区引擎
   生成 `Guid.NewGuid().ToString("N")`。规划完成后把实际生效的标识符写入
   `ResolvedItemId`（新增时为生成/指定值，去重命中时为既有条目标识符）。
3. `OpenItemResult`：携带 `ItemId`、`Created` 与 `ChangeSet`，使调用方无需
   解析变更集即可关闭或激活本次打开的条目。
4. `AtlasWorkspaceCommands.OpenItemFromRequestAsync(...)`：命令门面入口。
   使用独立方法名而非重载，避免 RS0026（公共 API 不允许同名可选参数重载）。

约束与兼容性：

- 既有 `DockItem`、`OpenItemOperation`、`AtlasWorkspaceCommands.OpenItemAsync`
  签名与语义保持不变；四种去重模式的判定顺序与结果不变。
- 生成仅发生在规划阶段且只使用 GUID，不引入工作区作用域计数器或全局状态。
- 并发打开可能因乐观并发返回 `StaleOperation`；成功结果的标识符仍然全局
  唯一，调用方可按既有策略重试。
- `ItemOpened` 领域事件继续携带实际条目标识符，作为结果之外的第二种来源。

## 备选方案

### 让调用方自己生成 GUID

无需公共 API 变更，但系统无法保证唯一性，也不满足“未指定时生成”的 AC。

### 给 `OpenItemOperation` 增加可空标识重载

会让冻结类型的构造与 `Item` 属性语义分叉（规划前不存在 `DockItem`），且
与既有显式 ID 路径产生两套校验入口。不采用。

### 复用 `OpenItemAsync` 方法名新增重载

公共 API 分析器 RS0026 会阻止同名可选参数重载；且调用点区分度低。采用
`OpenItemFromRequestAsync`。

## 后果

- 公共 API 增量：`OpenItemRequest`、`OpenItemRequestOperation`、
  `OpenItemResult` 与一个命令方法；已加入 `PublicAPI.Unshipped.txt`。
- 不改变 JS 交互协议、DOM metadata、XML schema 或包拓扑。
- 低层调用方可直接 `ExecuteAsync(OpenItemRequestOperation)` 并读取
  `ResolvedItemId`；推荐使用命令门面取得 `OpenItemResult`。

## 验证

- Core：未指定标识生成合法 GUID、结果与 `ItemOpened` 事件一致、生成标识可
  继续 Activate/Close、并发只产生唯一标识、显式标识保留、去重返回既有标识。
- PublicAPI analyzer：新增表面全部登记在 `Unshipped` 基线。
- 既有 `OpenItemOperationTests`、`WorkspaceFacadeTests` 与去重/历史测试保持
  通过。

## 取代关系

无。为 ADR-0029 受支持操作集中的 `OpenItem` 增加可选标识变体，不改变其
枚举数值或既有操作类。
