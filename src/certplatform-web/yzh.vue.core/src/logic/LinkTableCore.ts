/**
 * LinkTableCore - 勾选关联表格内核（AS-3，类型B：list/save）
 *
 * 语义：左树选节点 → 右表加载 list（行含 Linked 标志）→ checkbox 勾选即 save。
 *
 * 约定：
 * - list(nodeCode) → 行数组（含 Linked: boolean 与 linkedKeyField 对应的业务键）
 * - save({ [leftKey]: nodeCode, [linkedKeyField]: code, Linked }) 逐条提交
 * - 差集保存：与「变更前快照」比对，只提交 added / removed
 * - 乐观更新：先更新本地 Linked，失败回滚并提示（AS-5）
 */

import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { TreeNode } from '../types/tree'
import { AssociationTreeCore } from './AssociationTreeCore'

/** list/save 型 API 约定（页面注入，内核不拼端点） */
export interface LinkTableApi {
  /** 左树根节点 */
  treeRoot: () => Promise<TreeNode[]>
  /** 某节点的关联行列表（含 Linked 标志） */
  list: (nodeCode: string) => Promise<any[]>
  /** 保存单条关联（Linked=true/false） */
  save: (payload: Record<string, any>) => Promise<any>
}

export abstract class LinkTableCore extends AssociationTreeCore {
  protected linkApi: LinkTableApi

  /** 左侧节点的主键字段名（默认 Code） */
  protected get leftKeyField(): string {
    return 'Code'
  }

  /** 右表行中承载关联键的字段名（如 StandardCode / PhaseCode） */
  protected get linkedKeyField(): string {
    return 'Code'
  }

  /** 右表列配置（页面绑定） */
  abstract columns: Array<{ prop: string; label: string; minWidth?: number; width?: number }>

  /** 搜索关键字（页面可绑定本地过滤） */
  searchKey = ref('')

  /** 关联行数据（含 Linked） */
  get linkRows(): any[] {
    return this.associationData.value
  }

  /** 构造：linkApi 负责树/列表/保存；baseApi 可传 null 走空实现 */
  constructor(linkApi: LinkTableApi) {
    super({
      getTreeRoot: linkApi.treeRoot,
      getTreeChildren: async () => [],
      getAssociations: linkApi.list,
      add: async () => ({ Updated: 0 }),
      remove: async () => ({ Updated: 0 }),
      getAll: async () => [],
    })
    this.linkApi = linkApi
  }

  /** 节点选择 → list + 同步勾选快照 */
  override async handleNodeSelect(node: TreeNode): Promise<void> {
    if (!node || !node.Code) return
    this.selectedNode.value = node
    this.loading.value = true
    try {
      const data = await this.linkApi.list(node.Code)
      this.associationData.value = data
    } catch (e: any) {
      ElMessage.error(e.message || '加载关联数据失败')
      this.associationData.value = []
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 差集保存：与「变更前 Linked 快照」比对后逐条提交。
   *
   * 由页面的 selection-change 调用：
   *   onSelectionChange(selection) → 差集计算 → saveLinked()
   *
   * @param currentCodes 当前勾选的 code 集合
   * @param itemName 行显示名（失败提示用）
   */
  async saveLinkedDiff(currentCodes: Set<string>, itemName: (row: any) => string): Promise<void> {
    const node = this.selectedNode.value
    if (!node) return

    const prevCodes = new Set(
      this.associationData.value.filter((r) => r.Linked).map((r) => r[this.linkedKeyField]),
    )

    const toAdd: any[] = []
    const toRemove: any[] = []
    for (const row of this.associationData.value) {
      const key = row[this.linkedKeyField]
      const now = currentCodes.has(key)
      const before = prevCodes.has(key)
      if (now && !before) toAdd.push(row)
      if (!now && before) toRemove.push(row)
    }

    // 乐观更新本地状态
    for (const row of this.associationData.value) {
      row.Linked = currentCodes.has(row[this.linkedKeyField])
    }

    const payloads = [
      ...toAdd.map((row) => this.buildSavePayload(node, row, true)),
      ...toRemove.map((row) => this.buildSavePayload(node, row, false)),
    ]
    if (payloads.length === 0) return

    const failures: string[] = []
    for (const payload of payloads) {
      try {
        await this.linkApi.save(payload)
      } catch {
        // 回滚该条的本地状态
        const row = this.associationData.value.find(
          (r) => r[this.linkedKeyField] === payload[this.linkedKeyField],
        )
        if (row) row.Linked = !row.Linked
        failures.push(itemName(row ?? payload))
      }
    }
    if (failures.length > 0) {
      ElMessage.error(`保存失败：${failures.join('、')}`)
    }
  }

  /** 组装 save 请求体（子类可覆盖以适配后端字段） */
  protected buildSavePayload(node: TreeNode, row: any, linked: boolean): Record<string, any> {
    return {
      [this.leftKeyField]: node.Code,
      [this.linkedKeyField]: row[this.linkedKeyField],
      Linked: linked,
    }
  }
}

export default LinkTableCore
