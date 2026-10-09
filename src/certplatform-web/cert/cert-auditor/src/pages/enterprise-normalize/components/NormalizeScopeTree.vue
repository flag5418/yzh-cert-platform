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
 * ★★ 三条显示口径（用户 2026-10-07 裁决 + 实现取舍）
 *
 * ① **文件**：⛔ 只列「已配填写规则且已发布」的 —— 未配规则的文件**不显示**
 *    （用户裁决原话：「不显示」）。
 * ② **文件夹**：**全显示**（结构要如实）。带两个计数 ——
 *    `可规范化 N` / `共 M 个文件`，让用户一眼看懂「为什么只有这几个能跑」，
 *    ⛔ 而不是让文件夹凭空消失、用户以为系统丢了数据。
 * ③ **无可规范化文件的文件夹**：**灰显且不可勾选**（`Disabled`）——
 *    能勾但勾了没反应，比灰显更糟。
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
import { formatDateTime } from '@share/utils/format'
import {
  INSTANCE_STATE_TEXT,
  INSTANCE_STATE_TYPE,
  LOG_STATUS_TEXT,
  LOG_STATUS_TYPE,
  toPercent,
} from '@share/api/ent/enterprise-normalize'
import type { ScopeTreeNode } from '../logic'

defineProps<{
  /** ★ 变化即重挂载 —— 用于「程序化设置勾选后强制同步」（见文件头注释） */
  treeKey: string
  nodes: ScopeTreeNode[]
  /** 已勾选的标准域行 Code（只含文件，⛔ 不含文件夹） */
  checkedKeys: string[]
  loading: boolean
  /** 空态文案（不同标准 Tab 下不一样：有文件的 vs 没配规则的） */
  emptyText: string
}>()

const emit = defineEmits<{
  (e: 'update:checked', codes: string[]): void
  (e: 'run-one', file: NonNullable<ScopeTreeNode['File']>): void
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
      <el-input v-model="keyword" size="small" placeholder="搜索文件夹 / 文件名" clearable>
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

          <!-- 文件夹：两个计数，回答「为什么只有这几个能跑」 -->
          <template v-if="data.Type === 'folder'">
            <span v-if="data.FillableCount > 0" class="nst-node__meta">
              可规范化 {{ data.FillableCount }} 个
            </span>
            <span v-else class="nst-node__meta nst-node__meta--none">
              暂无可规范化文件
            </span>
            <span class="nst-node__meta nst-node__meta--dim">共 {{ data.TotalFileCount }} 个文件</span>
          </template>

          <!-- 文件：状态徽标 + 单文件同步入口（P1 的验证口，保留） -->
          <template v-else-if="data.File">
            <YzhStatusBadge
              :type="INSTANCE_STATE_TYPE[data.File.InstanceState] || 'info'"
              :text="INSTANCE_STATE_TEXT[data.File.InstanceState] || data.File.InstanceState"
            />
            <YzhStatusBadge v-if="data.File.IsLocked" type="warning" text="已锁定" />
            <span class="nst-node__meta">
              完成率 {{ toPercent(data.File.FillCompletion) }}% · 锚点
              {{ data.File.FillAnchorCount }} · 待办 {{ data.File.FillPendingCount }}
            </span>

            <!--
              ★ 上次填充的**结论 + 时间**（2026-10-09 补）。

              ⛔ 只说「待办 1 个」不说「什么时候跑的、跑成什么样」等于没说 ——
              用户没法判断「这是新结果还是三天前的旧结果」。
              ⚠️ 时间口径：后端已把 `LastFillTime`（留痕 UTC）显式标 UTC，JSON 带 Z，
                 `formatDateTime` 能正确换算成本地时间（此前少 8 小时）。
            -->
            <template v-if="data.File.LastFillStatus">
              <YzhStatusBadge
                :type="LOG_STATUS_TYPE[data.File.LastFillStatus] || 'info'"
                :text="LOG_STATUS_TEXT[data.File.LastFillStatus] || data.File.LastFillStatus"
              />
              <span v-if="data.File.LastFillTime" class="nst-node__time">
                {{ formatDateTime(data.File.LastFillTime) }}
              </span>
            </template>

            <!-- ★ 产物落点 —— 让用户知道「生成出来的文件在哪儿」 -->
            <span
              v-if="data.File.OutputPath"
              class="nst-node__path"
              :title="data.File.OutputPath"
            >
              产物 {{ data.File.OutputPath }}
            </span>

            <!--
              ★ 待办原因入口 —— ⛔ 只给「待办 1 个」这个数字等于没说。

              同一个数字背后有四种原因，指向四个**不同**的修复动作
              （模板没配数据源 / 参数未在后台定义 / 参数曾被裁决移除 / 企业没填值）。
              ⚠️ 明细来自**最近一次**填充留痕 ⇒ 文案必须写「上次」，⛔ 不能写成「现在」。
              ⚠️ `LastFillPendings` 可能为空（后端解析失败会退化为空数组）⇒ 用 `v-if` 兜住，
                 ⛔ 不给用户一个点了没反应的按钮。
            -->
            <el-popover
              v-if="data.File.LastFillPendings.length > 0"
              placement="right"
              :width="440"
              trigger="click"
              popper-class="nst-why-pop"
            >
              <template #reference>
                <el-button class="nst-node__why" type="default" link size="small">
                  为什么
                </el-button>
              </template>

              <div class="nst-why">
                <div class="nst-why__hd">
                  上次填充的待办 {{ data.File.LastFillPendings.length }} 处 —— 每条都写清了「去哪儿修」
                </div>
                <ul class="nst-why__list">
                  <li
                    v-for="(p, i) in data.File.LastFillPendings"
                    :key="i"
                    class="nst-why__item"
                  >
                    <span class="nst-why__token">{{ p.Token }}</span>
                    <span v-if="p.Location" class="nst-why__loc">（{{ p.Location }}）</span>
                    <div class="nst-why__reason">{{ p.Reason }}</div>
                  </li>
                </ul>
              </div>
            </el-popover>

            <el-button
              class="nst-node__action"
              type="primary"
              link
              size="small"
              @click.stop="emit('run-one', data.File)"
            >
              立即跑
            </el-button>
          </template>
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

.nst-node__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}

.nst-node__meta--none {
  color: var(--yzh-color-text-tertiary, #909399);
}

.nst-node__meta--dim {
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

/* 上次填充时间（留痕时间，已按 UTC→本地换算） */
.nst-node__time {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

/* 产物落点 —— 可能很长，⛔ 不给它撑破整行：单行截断 + title 看全 */
.nst-node__path {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.nst-node__action {
  flex: none;
}

.nst-node__why {
  flex: none;
}
</style>

<style>
/* ============================================================
 * ★ 待办原因弹层（`popper-class="nst-why-pop"`）
 *
 * 【为什么必须是非 scoped】`el-popover` 默认 `teleport` 到 `body` —— 弹层内容
 *   不在本组件 DOM 树里，`scoped` 的 `data-v-*` 选择器够不着 ⇒ 样式**永远不生效**。
 *   （同 `doc-fill-rule/index.vue` 的 `rtabs-pop` 做法。）
 *
 * 【为什么坚持 teleport】原因文本可能很长（如「参数曾定义、已被移除…」），
 *   留在树节点行内会把整行撑变形，甚至挤掉右侧按钮。
 *
 * ⚠️ 颜色/间距仍**全部用令牌** —— 守卫 `R18` 会扫（禁裸 hex / 裸 px）。
 * ============================================================ */
.nst-why-pop {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-3, 14px);
}

.nst-why__hd {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}

.nst-why__list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.nst-why__item {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.nst-why__token {
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-primary, #303133);
}

.nst-why__loc {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.nst-why__reason {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-regular, #606266);
}
</style>
