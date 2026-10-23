# ADR-0033：分割几何唯一真值源

> [English](../../en/adr/0033-split-geometry-single-source.md) | 简体中文
>
> 状态：Accepted（维护者已于 2026-10-23 批准）。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0021、ADR-0032；质量问题 CORE-P2-16

## 背景

分割约束有两套实现：

- `SplitLayoutResolver`（Core）计算可行比例区间、min/max 钳制与无解边界；
- `split-geometry.ts`（TypeScript）从 DOM dataset 值推导比例 min/max，
  供指针与键盘交互使用。

Core resolver 当前没有生产调用方（CORE-P2-15），而用户实际走的是 TypeScript
路径。两套实现在边界情况（单侧 max、无解、取整）可能漂移，导致提交的
`SplitBasis` 与视觉位置不一致。

## 决策

**Core 语义是参考实现；TypeScript 保持低延迟运行时实现；两者由共享黄金向量
锁定。**

1. 在 `tests/fixtures/split-geometry/` 定义带版本、语言中立的夹具，描述：
   - 约束（min/max 像素、容器尺寸、当前 basis）；
   - 期望的可行区间、钳制结果与无解结果。
2. C# resolver 测试与 JavaScript 几何测试消费同一夹具。修改夹具必须在同一
   PR 中同步更新两侧实现。
3. `ResizeSplitPlanner` 在提交前拒绝静态无效的 basis；extent 只在渲染时已知，
   因此运行时可行性检查由 resolver 与对齐夹具承担。操作构造函数在公共边界
   拒绝非有限、越界与 default basis，非法 basis 无法进入快照。
4. TypeScript 仍在本地计算预览；拖拽期间不回调 .NET。一致性由夹具保证，
   不依赖运行时桥接。
5. 取整属于契约：夹具断言整数像素/比例结果，而不仅是可行区间。

## 备选方案

### 只保留 TypeScript 实现

运行时只有一套实现，但 Core 与 headless 消费方失去几何校验能力，也无法
服务端/headless 使用。不采用。

### 只保留 Core 实现并从 JS 调用

单一代码路径，但给指针移动增加互操作延迟并复杂化预览渲染。交互热路径
不采用。

### 保留两套实现但不共享夹具

即现状；漂移只能等用户报告视觉不一致。不采用。

### 从单一源生成共享代码

长期有吸引力，但 .NET/TypeScript 代码生成增加构建复杂度与新工具链。可在
夹具契约稳定后再评估。

## 影响

- 边界行为只定义一次，并在两种语言中以相同方式测试。
- 几何语义变更需要更新夹具与两侧实现；CI 在只有一侧改变行为时失败。
- 非法提交 basis 会在进入快照前被 Core 拒绝。
- 无需公共 API 变更；`SplitLayoutResolver` 仍是 headless 工具（ADR-0032）。
- 不改变 JS 协议。

## 验证

- 共享夹具覆盖：区间内值、下限钳制、上限钳制、双侧钳制、单侧 max、无解、
  最小容器、整数取整。
- C# 与 JS 测试运行同一夹具文件并断言相同输出。
- `ResizeSplitOperation` 构造函数在提交前以类型化失败拒绝 default、非有限与
  越界 basis。
- 既有 resize/equalize/undo 测试保持通过。

## 取代关系

无。实现 ADR-0021 的对齐意图，并依赖 ADR-0032 保留 headless resolver 的决定。
