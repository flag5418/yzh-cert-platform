<script setup lang="ts">
/**
 * 规范化范围树（勾选）—— 文件夹 › 文件
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 为什么是「文件夹树 + 勾选」而不是列表
 *
 * 用户 2026-10-07 原话：「针对不同的标准，不同的文件夹，文件，或针对该阶段，
 * 选择需要进行规范化的文件，进行规范化处理」——
 * ⇒ 选择粒度是**三档**（标准 / 文件夹 / 文件），而 `el-tree` 的父子联动天然给出
 *   「勾一个文件夹 = 勾它下面全部可规范化文件」，⛔ 不需要自己写递归勾选。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：**这个组件只做「选择」，不再兼职「展示结果」**
 *
 * 用户原话（否掉旧版）：
 *   · 「我们不应该在界面上显示什么是标准化文件数量，哪些是已经定义了填写规则的
 *      文档数量，这样，让审核员非常奇怪，不理解」
 *   · 「我们当前就不用理会所有的标准文档，我们只考虑标准文档中已经设置规则的」
 *
 * ⇒ 本轮从本组件**删掉**四样东西：
 *   ① 文件夹上的 `可规范化 N 个` / `共 M 个文件` 两个计数
 *      （空文件夹已在 `create-logic.ts` 的 `toFolderNode` 里整棵剪掉）
 *   ② 文件行上的 `完成率 / 锚点 / 待办`、`上次填充` 徽标与时间、`产物` 路径
 *   ③ `为什么` 弹层（待办原因明细）—— 它回答的是「结果为什么不对」，
 *      属于**审核页**的问题，放在「选择要跑什么」这一步只会让用户困惑
 *   ④ `立即跑` 按钮（同步执行单个文件）—— 用户点名的「奇怪按钮」；
 *      单个文档要不要跑，由**范围勾选**决定，⛔ 不需要一个绕开范围的旁路入口
 *
 * 保留：`已锁定` 徽标 —— 它**影响本次能不能跑**（锁定的文档会被编排器跳过），
 *      属于「选择时必须知道」的信息，⛔ 不是结果展示。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 勾选状态为什么由父级持有（而不是本组件内部）
 *
 * 选择要**跨标准 Tab 保持**（用户可能同时选 9001 和食品的文件，一次入队）。
 * 而每个 Tab 渲染的是**独立的一棵 `el-tree`** ⇒ 状态必须上提到 `logic`，
 * 本组件只负责「渲染 + 回传」。
 *
 * ⚠️ `:key` 绑定 `treeKey`：程序化改选择（全选 / 清空）后，`el-tree` 的
 *    `default-checked-keys` **不会**对已挂载实例生效 ⇒ 只能靠换 key 强制重挂载。
 *    ⛔ 不要改成 `setCheckedKeys()`：它会再触发 `check` 事件，与父级形成回环。
 */
import { ref, watch } from 'vue'
import { Search } from '@element-plus/icons-vue'
import { YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import type { ScopeTreeNode } from '../create-logic'

defineProps<{
  /** ★ 变化即重挂载 —— 用于「程序化设置勾选后强制同步」（见文件头注释） */
  treeKey: string
  nodes: ScopeTreeNode[]
  /** 已勾选的标准域行 Code（只含文件，⛔ 不含文件夹） */
  checkedKeys: string[]
  loading: boolean
  /** 空态文案（不同标准 Tab 下不一样） */
  emptyText: string
}>()

const emit = defineEmits<{
  (e: 'update:checked', codes: string[]): void
}>()

const treeRef = ref()
const keyword = ref('')

/** el-tree 的字段映射（含 disabled —— 灰显不可勾选靠它，⛔ 不要用 CSS 假装） */
const TREE_PROPS = { label: 'Name', children: 'Children', disabled: 'Disabled' } as const

/** 搜索：父节点可见性由 el-tree 自身按「子节点有命中」推导，无需手写递归 */
function filterTreeNode(value: string, data: ScopeTreeNode): boolean {
  if (!value) return true
  return String(data?.Name ?? '').toLowerCase().includes(value.toLowerCase())
}

watch(keyword, (v) => treeRef.value?.filter(v))

/** 勾选变化 → 只把「文件叶子」交给父级（文件夹 key 是 `F:` 前缀，⛔ 不能混进去） */
function onCheck(): void {
  const keys: string[] = treeRef.value?.getCheckedKeys(true) ?? []
  emit('update:checked', keys.filter((k) => k.startsWith('S:')).map((k) => k.slice(2)))
}
</script>

<template>
  <div class="nst">
    <div v-if="nodes.length > 0" class="nst__search">
      <el-input v-model="keyword" size="small" placeholder="搜索文件夹 / 文档名" clearable>
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
    </div>

    <el-tree
      v-if="nodes.length > 0"
      :key="treeKey"
      ref="treeRef"
      class="nst__tree"
      :data="nodes"
      node-key="Key"
      :props="TREE_PROPS"
      show-checkbox
      default-expand-all
      :expand-on-click-node="false"
      :check-on-click-node="true"
      :filter-node-method="filterTreeNode"
      :default-checked-keys="checkedKeys.map((c) => `S:${c}`)"
      @check="onCheck"
    >
      <template #default="{ data }">
        <div
          class="nst-node"
          :class="{
            'nst-node--folder': data.Type === 'folder',
            'nst-node--muted': data.Disabled,
          }"
        >
          <span class="nst-node__name">{{ data.Name }}</span>

          <!-- 文件：只保留「影响本次能不能跑」的锁定态（⛔ 不展示结果类信息） -->
          <YzhStatusBadge
            v-if="data.Type === 'file' && data.File?.IsLocked"
            type="warning"
            text="已锁定"
          />
        </div>
      </template>
    </el-tree>

    <el-skeleton v-else-if="loading" :rows="4" animated />

    <YzhEmptyState v-else :title="emptyText" />
  </div>
</template>

<style scoped>
.nst {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.nst__search {
  max-width: 320px;
}

.nst__tree {
  background: transparent;
}

.nst-node {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
  flex-wrap: wrap;
}

.nst-node__name {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
}

.nst-node--folder .nst-node__name {
  font-weight: var(--yzh-font-weight-semibold, 600);
}

.nst-node--muted .nst-node__name {
  color: var(--yzh-color-text-tertiary, #909399);
  font-weight: var(--yzh-font-weight-normal, 400);
}
</style>
