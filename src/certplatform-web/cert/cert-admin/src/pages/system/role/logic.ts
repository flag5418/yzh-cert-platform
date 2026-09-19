/**
 * RolePageLogic - 角色管理 Logic（树形结构）
 *
 * 数据访问规则：
 * - res.data：ApiResponse 顶层（camelCase）
 * - TreeNode：el-tree 内部约定小写（node.code, node.name）
 * - formData：PascalCase key（与 formFields prop 一致）
 *
 * 架构：
 * - 角色树（Sys_Role），懒加载 + 增删改 + 启用/禁用
 * - 右侧显示选中角色详情
 * - 表单字段从后端 /treepconfig 自动派生，不硬编码
 */

import { TreeTableLogic, type TreeNode } from '@yzh-core'
import { reactive, ref } from 'vue'

// ========================================================
// Logic
// ========================================================

export class RolePageLogic extends TreeTableLogic<any> {
  controllerName = 'Role'

  // ──── 弹窗状态 ────
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  parentNode = ref<TreeNode | null>(null)
  editingNode = ref<TreeNode | null>(null)

  /**
   * 表单数据：PascalCase key（与 formFields prop 一致）
   */
  formData = reactive<Record<string, any>>({})

  // ========================================================
  // CRUD
  // ========================================================

  /** 打开新增弹窗 */
  openAddDialog(parent: TreeNode | null = null): void {
    this.dialogMode.value = 'add'
    this.parentNode.value = parent
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    // 从 NewEntity 模板初始化（后端 config 提供）
    const tmpl = (this.config.value?.NewEntity as any) || {}
    Object.assign(this.formData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
      IsValid: 1,
      ParentCode: parent?.code ?? null,
    })
    this.dialogVisible.value = true
  }

  /** 打开编辑弹窗 */
  openEditDialog(node: TreeNode): void {
    this.dialogMode.value = 'edit'
    this.editingNode.value = node
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    const extra = (node.extra as any) || {}
    Object.assign(this.formData, {
      Code: node.code,
      RoleName: node.name,
      ParentCode: node.parentCode,
      IsValid: extra.IsValid ?? 1,
      OrderNo: extra.OrderNo ?? null,
    })
    this.dialogVisible.value = true
  }

  /** 提交表单 */
  async submitForm(): Promise<void> {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        await this.addTreeNode(this.parentNode.value, this.formData)
      } else {
        await this.updateTreeNode(
          this.editingNode.value!,
          this.formData.RoleName ?? '',
          this.formData,
        )
      }
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }
}

export default RolePageLogic
