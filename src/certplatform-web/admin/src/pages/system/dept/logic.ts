/**
 * DeptTreeLogic - 部门管理 Logic（Vol TreeTable 适配版）
 *
 * 继承 TreeTableLogic，对接 Vol 框架的 TreeTable API：
 * - getTreeTableRootData: 加载根节点
 * - getTreeTableChildrenData: 懒加载子节点
 * - getPageData: 分页查询（表格）
 * - save: 新增/修改
 * - delete: 删除
 *
 * 字段映射：
 * - DepartmentId → code
 * - DepartmentName → name
 * - ParentId → parentCode
 * - HasChildren → !isLeaf
 *
 * 局部刷新策略（核心）：
 * - 新增/编辑/删除后，只刷新右侧表格，不重刷整棵树
 * - 树结构使用 treeOps 不可变变换进行增量更新
 */

import { ref, reactive } from 'vue'
import { TreeTableLogic } from '@share/logic'
import type { TreeNode, TreeConfig } from '@share/types/tree'
import { treeOps } from '@share/utils/treeOps'
import type { SysDept, DeptTreeNode } from '@share/api/system-dept'
import { getDeptRootNodes, getDeptChildren, getDeptPage, addDept, updateDept, deleteDept } from '@share/api/system-dept'

// ========================================================
// 配置
// ========================================================

/** 部门树字段映射配置 */
const deptTreeConfig: TreeConfig<SysDept> = {
  codeField: 'departmentId',
  nameField: 'departmentName',
  parentCodeField: 'parentId',
  typeField: 'departmentType',
  rootParentCode: null,
  lazy: true,
  relateField: 'parentId',
  allowDeleteWithChildren: false,
  maxLevel: 10,
  onlyLeafSelectable: false,
  checkStrictly: true
}

// ========================================================
// DeptTreeLogic 类
// ========================================================

export class DeptTreeLogic extends TreeTableLogic<SysDept> {
  // ──── 配置 ────

  controllerName = 'Sys_Department'

  treeConfig = deptTreeConfig

  relation = { relateField: 'parentId' }

  // ──── 表格列配置 ────

  columns = [
    { prop: 'departmentName', label: '部门名称', minWidth: 180 },
    { prop: 'departmentCode', label: '部门编码', width: 150 },
    { prop: 'departmentType', label: '部门类型', width: 120 },
    {
      prop: 'enable',
      label: '状态',
      width: 80,
      formatter: (v: number) => (v === 1 ? '启用' : '禁用')
    },
    { prop: 'remark', label: '备注', minWidth: 200 }
  ]

  searchFields = [
    { prop: 'departmentName', label: '部门名称', type: 'text' as const }
  ]

  // ──── 弹窗状态 ────

  /** 新增弹窗可见性 */
  showAddDialog = ref(false)

  /** 编辑弹窗可见性 */
  showEditDialog = ref(false)

  /** 当前编辑的节点 */
  editingNode = ref<TreeNode<SysDept> | null>(null)

  /** 当前选中作为父节点的节点（用于新增） */
  parentNodeForAdd = ref<TreeNode<SysDept> | null>(null)

  // ──── 表单数据 ────

  /** 新增表单 */
  addForm = reactive({
    departmentName: '',
    departmentCode: '',
    departmentType: '',
    enable: 1,
    remark: ''
  })

  /** 编辑表单 */
  editForm = reactive({
    departmentId: '',
    departmentName: '',
    departmentCode: '',
    departmentType: '',
    enable: 1,
    remark: ''
  })

  // ──── 计算属性 ────

  /** 表格列定义别名 */
  get tableColumns() {
    return this.columns
  }

  /** 表格数据别名 */
  get tableRows() {
    return this.rows.value
  }

  /** 加载状态别名 */
  get tableLoading() {
    return this.loading.value
  }

  /** 分页别名 */
  get tablePagination() {
    return this.pagination
  }

  // ========================================================
  // 实现抽象方法
  // ========================================================

  /** 加载树数据 */
  async loadTree(parentCode?: string | null): Promise<DeptTreeNode[]> {
    if (parentCode === undefined || parentCode === null) {
      return getDeptRootNodes()
    } else {
      return getDeptChildren(parentCode)
    }
  }

  /** 后端树节点 DTO → TreeNode */
  entityToTreeNode(dto: DeptTreeNode, level: number): TreeNode<SysDept> {
    // 兼容 PascalCase 和 camelCase 两种格式
    const departmentId = dto.departmentId ?? (dto as any).DepartmentId
    const departmentName = dto.departmentName ?? (dto as any).DepartmentName
    const parentId = dto.parentId ?? (dto as any).ParentId
    const hasChildren = dto.hasChildren ?? (dto as any).hasChildren ?? (dto as any).HasChildren
    const enable = dto.enable ?? (dto as any).Enable
    const remark = dto.remark ?? (dto as any).Remark

    return {
      code: departmentId,
      name: departmentName,
      parentCode: parentId ?? null,
      nodeType: undefined,
      isLeaf: !hasChildren,
      extra: {
        icon: '📁',
        level,
        enable,
        remark
      },
      children: [],
      raw: dto as any
    }
  }

  /** TreeNode → 后端实体 DTO */
  treeNodeToEntity(node: TreeNode<SysDept>): Partial<SysDept> {
    return {
      departmentId: node.code,
      departmentName: node.name,
      parentId: node.parentCode ?? undefined,
      departmentType: node.nodeType,
      enable: node.extra?.enable
    }
  }

  // ========================================================
  // 树操作 - Vol API 适配
  // ========================================================

  /** 加载根节点（覆盖基类） */
  async initTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const rootItems = await getDeptRootNodes()
      this.treeData.value = rootItems.map(item => {
        const node = this.entityToTreeNode(item, 0)
        node.parentCode = null
        return node
      })
    } finally {
      this.treeLoading.value = false
    }
  }

  /** 懒加载子节点（覆盖基类，适配 el-tree load 函数签名） */
  loadChildren(parentNode: TreeNode<SysDept>, resolve: (data: TreeNode<SysDept>[]) => void): void {
    getDeptChildren(parentNode.code).then(items => {
      const children = items.map(item => {
        const node = this.entityToTreeNode(item, (parentNode.extra?.level ?? 0) + 1)
        node.parentCode = parentNode.code
        return node
      })
      resolve(children)
    }).catch(() => {
      resolve([])
    })
  }

  // ========================================================
  // 表格数据加载
  // ========================================================

  /** 带树条件的分页查询 */
  async loadDataWithTreeCondition(node: TreeNode): Promise<void> {
    this.loading.value = true
    try {
      const res = await getDeptPage({
        page: this.pagination.page,
        rows: this.pagination.pageSize,
        parentId: node.code
      })
      this.rows.value = res.rows
      this.pagination.total = res.total
    } finally {
      this.loading.value = false
    }
  }

  /** 不带树条件的查询 */
  async loadData(): Promise<void> {
    this.loading.value = true
    try {
      const res = await getDeptPage({
        page: this.pagination.page,
        rows: this.pagination.pageSize
      })
      this.rows.value = res.rows
      this.pagination.total = res.total
    } finally {
      this.loading.value = false
    }
  }

  /** 刷新当前表格（保持当前选中节点和分页） */
  async refreshTable(): Promise<void> {
    if (this.selectedNode.value) {
      await this.loadDataWithTreeCondition(this.selectedNode.value)
    } else {
      await this.loadData()
    }
  }

  // ========================================================
  // 新增操作 - 局部刷新
  // ========================================================

  /** 打开新增弹窗 */
  openAddDialog(parentNode: TreeNode<SysDept> | null = null): void {
    this.parentNodeForAdd.value = parentNode
    // 重置表单
    Object.assign(this.addForm, {
      departmentName: '',
      departmentCode: '',
      departmentType: '',
      enable: 1,
      remark: ''
    })
    this.showAddDialog.value = true
  }

  /** 提交新增（局部刷新） */
  async handleAdd(): Promise<void> {
    const data: Partial<SysDept> = {
      departmentName: this.addForm.departmentName,
      departmentCode: this.addForm.departmentCode,
      departmentType: this.addForm.departmentType,
      enable: this.addForm.enable,
      remark: this.addForm.remark,
      parentId: this.parentNodeForAdd.value?.code
    }

    // 后端保存
    const saved = await addDept(data)

    // 构造新节点
    const newNode: TreeNode<SysDept> = {
      code: saved.departmentId ?? '',
      name: this.addForm.departmentName,
      parentCode: data.parentId ?? null,
      nodeType: this.addForm.departmentType,
      isLeaf: true,
      extra: {
        icon: '📁',
        level: (this.parentNodeForAdd.value?.extra?.level ?? -1) + 1,
        enable: this.addForm.enable,
        remark: this.addForm.remark
      },
      children: []
    }

    // 增量更新树（不可变变换）
    this.treeData.value = treeOps.addNode(
      this.treeData.value,
      this.parentNodeForAdd.value?.code ?? null,
      newNode
    )

    // ★ 局部刷新：如果当前选中节点是新增节点的父节点，刷新表格
    const parentCode = this.parentNodeForAdd.value?.code ?? null
    const selectedCode = this.selectedNode.value?.code ?? null
    if (parentCode === selectedCode) {
      await this.loadDataWithTreeCondition(this.selectedNode.value!)
    } else if (parentCode === null && selectedCode === null) {
      // 新增根节点且当前无选中
      await this.loadData()
    }

    this.showAddDialog.value = false
  }

  // ========================================================
  // 编辑操作 - 局部刷新
  // ========================================================

  /** 打开编辑弹窗 */
  openEditDialog(node: TreeNode<SysDept>): void {
    this.editingNode.value = node
    Object.assign(this.editForm, {
      departmentId: node.code,
      departmentName: node.name,
      departmentCode: (node.raw as SysDept)?.departmentCode ?? '',
      departmentType: node.nodeType ?? '',
      enable: node.extra?.enable ?? 1,
      remark: node.extra?.remark ?? ''
    })
    this.showEditDialog.value = true
  }

  /** 提交编辑（局部刷新） */
  async handleEdit(): Promise<void> {
    if (!this.editingNode.value) return

    const data: Partial<SysDept> = {
      departmentId: this.editForm.departmentId,
      departmentName: this.editForm.departmentName,
      departmentCode: this.editForm.departmentCode,
      departmentType: this.editForm.departmentType,
      enable: this.editForm.enable,
      remark: this.editForm.remark,
      parentId: this.editingNode.value.parentCode ?? undefined
    }

    // 后端保存
    await updateDept(data)

    // 增量更新树节点
    this.treeData.value = treeOps.updateNode(
      this.treeData.value,
      this.editingNode.value.code,
      {
        name: this.editForm.departmentName,
        nodeType: this.editForm.departmentType,
        extra: {
          ...this.editingNode.value.extra,
          enable: this.editForm.enable,
          remark: this.editForm.remark
        }
      }
    )

    // ★ 局部刷新表格
    if (this.selectedNode.value) {
      await this.loadDataWithTreeCondition(this.selectedNode.value)
    }

    this.showEditDialog.value = false
    this.editingNode.value = null
  }

  // ========================================================
  // 删除操作 - 局部刷新（增量删除）
  // ========================================================

  /** 删除单个部门（增量删除，局部刷新表格） */
  async handleDelete(node: TreeNode<SysDept>): Promise<boolean> {
    // 检查是否有子节点
    if (!this.treeConfig.allowDeleteWithChildren && node.children.length > 0) {
      const { ElMessage } = await import('element-plus')
      ElMessage.warning('该部门包含子部门，无法删除')
      return false
    }

    const { ElMessageBox } = await import('element-plus')
    await ElMessageBox.confirm(`确定删除部门 "${node.name}" 吗？`, '删除确认', {
      type: 'warning'
    })

    // 后端删除
    await deleteDept([node.code])

    // ★ 增量删除：从树中移除节点（不可变变换）
    const { tree } = treeOps.removeSubtree(this.treeData.value, node.code)
    this.treeData.value = tree

    // 如果删除的是当前选中节点，清空选中
    if (this.selectedNode.value?.code === node.code) {
      this.selectedNode.value = null
    }

    // ★ 局部刷新表格
    await this.refreshTable()

    return true
  }

  // ========================================================
  // 批量删除 - 局部刷新
  // ========================================================

  /** 批量删除部门 */
  async handleBatchDelete(nodes: TreeNode<SysDept>[]): Promise<boolean> {
    if (nodes.length === 0) {
      const { ElMessage } = await import('element-plus')
      ElMessage.warning('请先选择要删除的部门')
      return false
    }

    const { ElMessageBox } = await import('element-plus')
    await ElMessageBox.confirm(`确定删除选中的 ${nodes.length} 个部门吗？`, '批量删除', {
      type: 'warning'
    })

    const codes = nodes.map(n => n.code)

    // 后端批量删除
    await deleteDept(codes)

    // ★ 增量删除：逐个移除
    let currentTree = this.treeData.value
    for (const code of codes) {
      const result = treeOps.removeSubtree(currentTree, code)
      currentTree = result.tree
    }
    this.treeData.value = currentTree

    // 清空选中
    this.selectedNode.value = null

    // ★ 局部刷新表格
    await this.refreshTable()

    return true
  }

  // ========================================================
  // 工具方法
  // ========================================================

  /** 获取勾选节点的 code 列表 */
  getCheckedCodes(): string[] {
    return this.checkedNodes.value.map(n => n.code)
  }

  /** 根据 code 查找树节点 */
  findNodeByCode(code: string): TreeNode<SysDept> | null {
    const find = (nodes: TreeNode<SysDept>[]): TreeNode<SysDept> | null => {
      for (const node of nodes) {
        if (node.code === code) return node
        if (node.children.length > 0) {
          const found = find(node.children)
          if (found) return found
        }
      }
      return null
    }
    return find(this.treeData.value)
  }
}

// ========================================================
// 导出
// ========================================================

export default DeptTreeLogic
