/**
 * 页面 Logic 组合式封装（CP 阶段）
 *
 * 统一 onMounted → init → nextTick → 注入 refs 的样板流程（CP-5）：
 * - useSingleTable(LogicClass)：注入 tableRef
 * - useTreeTable(LogicClass)：注入 tableRef + treeTableRef
 * - useCheckTree(LogicClass)：左树选择 + 勾选保存（注入 API 由 Logic 构造器处理）
 * - useLinkTable(LogicClass)：左树选择 + 勾选即存
 *
 * 类型推导（CP-7）：LogicClass 用构造签名约束，IDE 提示完整。
 */

import { nextTick, onMounted, ref, type Ref } from 'vue'
import type { SingleTableCore } from '../logic/SingleTableCore'
import type { TreeTableCore } from '../logic/TreeTableCore'
import type { AssociationTreeCore } from '../logic/AssociationTreeCore'

type LogicCtor<L> = new (...args: any[]) => L

/** 单表页（CP-1） */
export function useSingleTable<L extends SingleTableCore<any>>(
  LogicClass: LogicCtor<L>,
  ...args: ConstructorParameters<LogicCtor<L>>
): { logic: L; tableRef: Ref<any> } {
  const logic = new LogicClass(...args) as L
  const tableRef = ref<any>(null)

  onMounted(async () => {
    await logic.init()
    await nextTick()
    logic.setTableRef(tableRef.value)
  })

  return { logic, tableRef }
}

/** 左树右表页（CP-2） */
export function useTreeTable<L extends TreeTableCore<any>>(
  LogicClass: LogicCtor<L>,
  ...args: ConstructorParameters<LogicCtor<L>>
): { logic: L; tableRef: Ref<any>; treeTableRef: Ref<any> } {
  const logic = new LogicClass(...args) as L
  const tableRef = ref<any>(null)
  const treeTableRef = ref<any>(null)

  onMounted(async () => {
    await logic.init()
    await nextTick()
    logic.setTableRef(tableRef.value)
    logic.setTreeTableRef(treeTableRef.value)
  })

  return { logic, tableRef, treeTableRef }
}

/** 勾选授权页（CP-3）：左树 + 勾选树 */
export function useCheckTree<L extends AssociationTreeCore>(
  LogicClass: LogicCtor<L>,
  ...args: ConstructorParameters<LogicCtor<L>>
): { logic: L } {
  const logic = new LogicClass(...args) as L

  onMounted(async () => {
    await logic.init()
  })

  return { logic }
}

/** 勾选关联页（CP-4）：左树 + 勾选即存表格 */
export function useLinkTable<L extends AssociationTreeCore>(
  LogicClass: LogicCtor<L>,
  ...args: ConstructorParameters<LogicCtor<L>>
): { logic: L } {
  const logic = new LogicClass(...args) as L

  onMounted(async () => {
    await logic.init()
  })

  return { logic }
}
