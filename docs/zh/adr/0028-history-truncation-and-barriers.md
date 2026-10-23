# ADR-0028：历史截断警告与屏障语义

> [English](../../en/adr/0028-history-truncation-and-barriers.md) | 简体中文
>
> 状态：Accepted（维护者已于 2026-10-23 批准）。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0003、ADR-0006、ADR-0011；HLD DES-OP 历史规则；质量问题 CORE-P1-03

## 背景

`WorkspaceHistoryOptions` 提供 `MaxEntries`（默认 100）、
`ApproximateMaxBytes` 与 `ResizeCoalescingWindow`。HLD 规定：

- 新 entry 先淘汰最旧 entry，直到满足预算；
- 若单个 entry 仍无法放入预算，操作照常提交，但成为**历史屏障**，
  并返回带 `HistoryNotRecorded` 警告的成功结果；
- 旧 entry 不得在新状态之上伪装成可撤销；
- restore/reset 是显式屏障，会同时提交新的空历史。

当前实现执行 `MaxEntries`，并在超过 `ApproximateMaxBytes` 时淘汰最旧补丁。
单个补丁超过预算时仍会保留，使新状态至少可撤销一次
（`AtlasWorkspace.cs` 的历史追加逻辑），但没有暴露警告，也未实现屏障规则。
调用方无法区分“已提交且可撤销”与“已提交但历史被截断”。

工作区结果类型属于冻结的操作契约，因此警告模型必须是增量的，且不得改变
既有结果含义。

## 决策

用增量公共 API 明确并暴露 HLD 的历史契约：

1. **超大 entry 形成屏障。** 淘汰旧 entry 后，若新补丁仍超过
   `ApproximateMaxBytes`，清空保留历史、提交布局状态，并把变更集标记为
   “未记录”。新状态成为屏障：旧 entry 不得再叠加到其上。
2. **在变更集上暴露结果。** 为公共 `LayoutChangeSet` 增加两个只读属性：
   - `bool HistoryRecorded`：提交成功但补丁未保留时为 `false`；
   - `bool IsHistoryBarrier`：restore/reset 提交及超大 entry 提交为 `true`。
   既有构造函数与属性保持不变；普通提交的默认值分别为 `true` 与 `false`。
3. **不再静默保留超大补丁。** 保留大于配置预算的 entry 与预算契约矛盾；
   移除当前“至少保留一条”的回退。显式配置 `null` 时保留无界行为。
4. **restore/reset 仍为屏障。** 它们已提交新的空历史；新增
   `IsHistoryBarrier` 只是让该语义可见，不改变行为。

该变更在源码层是增量的，不改变操作签名、XML schema 或事件顺序。

## 备选方案

### 仅用回调观测

增加可选 `Action<HistoryTruncation>`（类似 `SubscriberFaultSink`）可避免修改
`LayoutChangeSet`。不采用：HLD 将结果视为操作结果的一部分；只检查结果的
调用方仍会漏掉警告，且回调会引入额外顺序面。

### 超大 entry 直接抛异常

预算严格，但 HLD 要求操作仍提交。不采用。

### 保留当前“保留一条超大 entry”行为

成本低，但违反配置预算且屏障未定义。不采用。

### 在 `WorkspaceResult` 上增加通用 Warnings 集合

范围过大，会改变所有调用方的冻结结果形态。本决策不采用；通用警告集合
可另行讨论。

## 影响

- 消费方可从变更集判断历史是否被截断。
- `ApproximateMaxBytes` 成为保留补丁的硬预算。
- 超大提交后立即可用的撤销不可用；redo/历史行为保持确定。
- 未配置字节预算的默认路径不变。
- 公共 API 增加两个只读属性，需记录到 `PublicAPI.Unshipped.txt`。

## 验证

- 单元测试：超大 entry 清空历史，`HistoryRecorded = false`、
  `IsHistoryBarrier = true`；普通提交保持默认值。
- 恢复测试：restore/reset 变更集报告 `IsHistoryBarrier = true`。
- 预算测试：从旧到新淘汰；保留总量不超预算；`MaxEntries` 仍独立生效。
- 撤销/重做、合并、事件顺序与并发测试保持通过。

## 取代关系

无。实现 HLD 已声明的历史规则，并补充 ADR-0006 的合并边界；不取代
ADR-0003 或 ADR-0011。
