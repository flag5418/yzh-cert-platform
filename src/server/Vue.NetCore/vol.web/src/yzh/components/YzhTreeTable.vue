<!--
  YZH Framework V2.1 —— 左树右表基类组件

  设计场景：
  - ISO标准管理（左侧机构 → 右侧该机构的ISO标准）
  - 任何"主从浏览"场景（分类 → 列表、部门 → 人员、仓库 → 物料）

  布局结构：
  ┌──────────────────────────────────────────────┐
  │  [工具栏: 新增 | 刷新 | 导入 | 导出]          │
  ├──────────┬───────────────────────────────────┤
  │          │  [搜索区]                          │
  │  左侧树  ├───────────────────────────────────┤
  │  (机构)   │  [表格列: 数据列表]                │
  │          │  ...                               │
  │  ▼ A机构 │  ...                               │
  │  ▼ B机构 │  ...                               │
  │  ▶ C机构 │  [分页]                            │
  └──────────┴───────────────────────────────────┘

  核心特性：
  1. 左侧树选中节点变化时，右侧自动过滤 + 自动翻到第1页
  2. 新增/编辑时，自动将左侧选中节点的 key 填入 filterField
  3. 复用 YzhCrudTable 的全部 CRUD 能力（增量刷新、字典、导出等）
  4. 支持外部注入 treeData（也可内部请求）
-->
<template>
  <div class="yzh-tree-table">
    <!-- ====== 0. 头部插槽 ====== -->
    <slot name="treeTableHeader"></slot>

    <div class="yzh-tree-table__body">
      <!-- ====== 左侧：树/列表 ====== -->
      <div class="yzh-tree-table__tree" :style="{ width: treeWidth }">
        <div class="yzh-tree-table__tree-header">
          <span class="yzh-tree-table__tree-title">{{ treeTitle }}</span>
          <el-button
            v-if="showTreeRefresh"
            link
            type="primary"
            size="small"
            @click="handleRefreshTree"
          >
            <RefreshRight /> 刷新
          </el-button>
        </div>

        <!-- 树形展示 -->
        <el-tree
          ref="treeRef"
          :data="treeData"
          :props="treeProps"
          node-key="key"
          :highlight-current="true"
          :expand-on-click-node="false"
          :default-expand-all="defaultExpandAll"
          current-node-key=""
          @node-click="handleTreeNodeClick"
          v-loading="treeLoading"
        >
          <template #default="{ node, data }">
            <div class="yzh-tree-table__tree-node">
              <!-- 自定义树节点渲染 -->
              <slot name="treeNode" :node="node" :data="data">
                <span class="yzh-tree-table__tree-node-label" :class="{ 'is-active': selectedTreeKey === data[keyField] }">
                  {{ data[labelField] }}
                </span>
                <!-- 节点计数 Badge -->
                <el-badge
                  v-if="showNodeCount && data._count !== undefined"
                  :value="data._count"
                  :type="selectedTreeKey === data[keyField] ? 'primary' : 'info'"
                  class="yzh-tree-table__tree-node-count"
                />
              </slot>
            </div>
          </template>
        </el-tree>

        <!-- 空状态 -->
        <div v-if="!treeLoading && treeData.length === 0" class="yzh-tree-table__tree-empty">
          <el-empty description="暂无数据" :image-size="60" />
        </div>
      </div>

      <!-- ====== 分隔条（可拖拽调整宽度）===== -->
      <div class="yzh-tree-table__divider" @mousedown.prevent="startResize"></div>

      <!-- ====== 右侧：CRUD 表格（复用 YzhCrudTable）====== -->
      <div class="yzh-tree-table__table">
        <YzhCrudTable
          ref="crudTableRef"
          :schema="schema"
          :options="options"
          :lifecycles="mergedLifecycles"
          :incremental-update="incrementalUpdate"
          :search-mode="searchMode"
          :external-filter="currentFilter"
          :show-action-column="showActionColumn"
          :dialog-width="dialogWidth"
          :buttons="buttons"
          :page-key="pageKey"
        >
          <!-- 透传所有插槽 -->
          <template #gridHeader><slot name="gridHeader"></slot></template>
          <template #toolbarExtra><slot name="toolbarExtra"></slot></template>
          <template #tableEmpty><slot name="tableEmpty"></slot></template>
        </YzhCrudTable>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * YZH Framework V2.1 —— 左树右表基类组件
 *
 * 继承关系：
 * YzhTreeTable → 组合使用 YzhCrudTable（非继承，组合模式）
 *
 * 设计原则：
 * 1. 左侧树只负责"选择过滤条件"，不参与 CRUD
 * 2. 右侧表格完全复用 YzhCrudTable 的能力
 * 3. 通过 externalFilter 实现联动过滤
 * 4. 新增时自动填充 filterField 值
 */
import {
  ref,
  reactive,
  computed,
  watch,
  onMounted,
  getCurrentInstance,
  markRaw,
  shallowRef,
  nextTick,
} from 'vue'
import { RefreshRight } from '@element-plus/icons-vue'
import type { IYZHTreeTableProps } from '../types/YZHPageProps'
import type { IYZHEntitySchema } from '../types/YZHEntitySchema'
import type { IYZHPageLifecycle } from '../types/YZHLifecycles'
import YzhCrudTable from './YzhCrudTable.vue'
import { createDefaultLifecycles, runGuard } from '../core/YZHPageLifecycle'

// ============================================================
// Props
// ============================================================
const props = withDefaults(defineProps<IYZHTreeTableProps<any, any>>(), {
  incrementalUpdate: true,
  searchMode: 'fixed',
  showActionColumn: true,
  dialogWidth: 960,
  // 左树特有默认值
  treeTitle: '导航',
  treeWidth: '240px',
  treeLabelField: 'Name',
  treeKeyField: 'Code',
  showTreeRefresh: true,
  defaultExpandAll: true,
  showNodeCount: false,
})

const emit = defineEmits<{
  (e: 'ready', instance: any): void
  (e: 'treeNodeSelect', node: any, data: any): void
  (e: 'treeDataLoaded', data: any[]): void
}>()

const { proxy } = getCurrentInstance()

// ============================================================
// 左侧树状态
// ============================================================
const treeRef = ref()
const crudTableRef = ref()
const treeData = ref<any[]>([])
const treeLoading = ref(false)
const selectedTreeKey = ref<any>(null)
const selectedTreeNode = ref<any>(null)

// 树属性映射
const labelField = computed(() => props.treeLabelField || 'Name')
const keyField = computed(() => props.treeKeyField || 'Code')
const treeProps = computed(() => ({
  label: labelField.value,
  children: props.treeChildrenField || 'children',
  isLeaf: (data: any) => !data[props.treeChildrenField || 'children']?.length,
}))

// 分隔条拖拽状态
const isResizing = ref(false)

// ============================================================
// Schema & Options（透传给 YzhCrudTable）
// ============================================================
const schema = computed(() => props.schema as IYZHEntitySchema<any, any>)

// ============================================================
// 外部过滤条件（核心联动机制）
// ============================================================
const currentFilter = computed(() => {
  if (selectedTreeKey.value === null || selectedTreeKey.value === '') return []
  return [
    {
      name: props.filterField,
      value: selectedTreeKey.value,
      cond: '==',  // 精确匹配
    },
  ]
})

// ============================================================
// 合并生命周期钩子（注入左树选择逻辑）
// ============================================================

/** 创建增强的生命周期：在 onAddBefore 中自动填入 filterField */
function createMergedLifecycles(): IYZHPageLifecycle<any, any> {
  const defaults = createDefaultLifecycles<any, any>()
  const custom = props.lifecycles || {}

  return {
    ...defaults,
    ...custom,

    // 新增前：自动将选中的树节点 key 填入表单
    onAddBefore: async (form: any) => {
      if (selectedTreeKey.value !== null && selectedTreeKey.value !== '') {
        form[props.filterField] = selectedTreeKey.value
      }
      // 调用用户自定义的 onAddBefore
      return runGuard(custom.onAddBefore, [form])
    },

    // 加载后：触发树节点选择事件
    onLoadAfter: async (data: any[]) => {
      custom.onLoadAfter?.(data)
    },
  }
}

const mergedLifecycles = markRaw(createMergedLifecycles())

// ============================================================
// 左侧树：加载方法
// ============================================================

/**
 * 加载树数据
 * @param data 直接传入数据（外部加载场景）
 * @param url 从接口加载（内部请求场景）
 */
async function loadTree(data?: any[] | null, url?: string) {
  treeLoading.value = true
  try {
    if (data !== undefined && data !== null) {
      // 外部直接传入数据
      treeData.value = data
    } else if (url) {
      // 从接口加载
      const res = await proxy?.http?.post(url, {}, false)
      treeData.value = Array.isArray(res) ? res : (res?.data ?? [])
    } else if (props.treeControllerName) {
      // 使用 treeControllerName 构建默认查询接口
      // 参数格式必须与 Vol PageDataOptions 匹配（和 YzhCrudTable.loadData 一致）
      const res = await proxy?.http?.post(
        `/api/${props.treeControllerName}/GetPageData`,
        {
          page: 1,
          rows: 1000,
          sort: 'Sort',
          order: 'asc',
          value: JSON.stringify([{ name: 'Enable', value: true, type: '=' }]),
        },
        false
      )
      const list = res?.data?.rows ?? res?.rows ?? (Array.isArray(res) ? res : [])
      treeData.value = list.map((item: any) => ({
        ...item,
        key: item[keyField.value],
        label: item[labelField.value],
      }))
    }

    emit('treeDataLoaded', treeData.value)

    // 自动选中第一个节点
    if (treeData.value.length > 0 && !selectedTreeKey.value) {
      await nextTick()
      handleTreeNodeClick(treeData.value[0])
    }
  } catch (e: any) {
    console.error('[YzhTreeTable] 加载树数据失败:', e)
  } finally {
    treeLoading.value = false
  }
}

/** 刷新树数据 */
async function handleRefreshTree() {
  await loadTree(null, props.treeUrl)
}

// ============================================================
// 左侧树：节点点击处理
// ============================================================
async function handleTreeNodeClick(data: any) {
  selectedTreeKey.value = data[keyField.value]
  selectedTreeNode.value = data

  emit('treeNodeSelect', data, data)

  // 等待 Vue 响应式更新传播到 YzhCrudTable 的 externalFilter prop
  await nextTick()
  await nextTick()

  // 切换树节点时：重置分页到第1页，然后用新的 externalFilter 重新加载表格
  const crud = crudTableRef.value
  if (crud) {
    // 重置分页到第1页（切换机构后应从第1页开始）
    if (crud.pagination) {
      crud.pagination.page = 1
    }
    // 调用 loadData 重新请求数据（此时 externalFilter 已更新为新选中节点的 CbCode）
    if (crud.loadData) {
      crud.loadData()
    } else {
      // 兼容回退：如果 loadData 不可用，使用 refresh（会清空搜索条件）
      crud.refresh?.()
    }
  }
}

// ============================================================
// 分隔条拖拽调整宽度
// ============================================================
function startResize(e: MouseEvent) {
  isResizing.value = true
  document.addEventListener('mousemove', onResize)
  document.addEventListener('mouseup', stopResize)
  e.preventDefault()
}

function onResize(e: MouseEvent) {
  if (!isResizing.value) return
  const container = document.querySelector('.yzh-tree-table__body') as HTMLElement
  const treePane = document.querySelector('.yzh-tree-table__tree') as HTMLElement
  if (!container || !treePane) return

  const containerRect = container.getBoundingClientRect()
  const newWidth = Math.max(180, Math.min(400, e.clientX - containerRect.left))
  ;(treePane as any).style.width = `${newWidth}px`
}

function stopResize() {
  isResizing.value = false
  document.removeEventListener('mousemove', onResize)
  document.removeEventListener('mouseup', stopResize)
}

// ============================================================
// 公开方法（供父组件调用）
// ============================================================

/** 获取当前选中的树节点 key */
function getSelectedTreeKey(): any {
  return selectedTreeKey.value
}

/** 获取当前选中的树节点完整数据 */
function getSelectedTreeNode(): any {
  return selectedTreeNode.value
}

/** 程序化选中树节点 */
async function selectTreeNode(key: any) {
  const node = treeData.value.find((item: any) => item[keyField.value] === key)
  if (node) {
    handleTreeNodeClick(node)
  }
}

/** 获取 CrudTable 实例（透传方法调用） */
function getCrudTable(): any {
  return crudTableRef.value
}

// 暴露给模板和父组件
defineExpose({
  loadTree,
  handleRefreshTree,
  handleTreeNodeClick,
  getSelectedTreeKey,
  getSelectedTreeNode,
  selectTreeNode,
  getCrudTable,
  treeData,
  selectedTreeKey,
})

// ============================================================
// 初始化
// ============================================================
onMounted(async () => {
  // 始终尝试加载树数据：
  // 1. 如果有外部传入的 treeData prop → 直接使用
  // 2. 否则通过 treeControllerName 或 treeUrl 从接口加载
  if (props.treeData && props.treeData.length > 0) {
    await loadTree(props.treeData)
  } else {
    // 自动加载：优先用 treeUrl，其次用 treeControllerName
    await loadTree(null, props.treeUrl)
  }

  emit('ready', {
    loadTree,
    selectTreeNode,
    getSelectedTreeKey,
    getCrudTable,
  })
})
</script>

<style lang="less" scoped>
.yzh-tree-table {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);

  &__body {
    display: flex;
    flex: 1;
    overflow: hidden;
    border: 1px solid var(--el-border-color-lighter);
    border-radius: 4px;
  }

  // ====== 左侧树面板 ======
  &__tree {
    display: flex;
    flex-direction: column;
    border-right: 1px solid var(--el-border-color-lighter);
    background: var(--el-fill-color-lighter);
    overflow: auto;
    min-width: 180px;
    max-width: 400px;
    flex-shrink: 0;

    &-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 12px;
      border-bottom: 1px solid var(--el-border-color-lighter);
      background: var(--el-bg-color);
    }

    &-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    &-empty {
      padding: 20px 0;
    }

    .el-tree {
      background: transparent;
      padding: 8px 0;
    }

    &-node {
      display: flex;
      align-items: center;
      width: 100%;
      padding: 0 4px;

      &-label {
        flex: 1;
        font-size: 13px;
        color: var(--el-text-color-regular);
        line-height: 24px;
        cursor: pointer;
        border-radius: 4px;
        padding: 2px 6px;
        transition: all 0.2s;

        &:hover {
          color: var(--el-color-primary);
          background: var(--el-fill-color);
        }

        &.is-active {
          color: var(--el-color-primary);
          background: var(--el-color-primary-light-9);
          font-weight: 500;
        }
      }

      &-count {
        margin-left: 4px;
      }
    }
  }

  // ====== 可拖拽分隔条 ======
  &__divider {
    width: 5px;
    cursor: col-resize;
    background: var(--el-border-color-lighter);
    transition: background 0.2s;
    flex-shrink: 0;

    &:hover {
      background: var(--el-color-primary-light-5);
    }
  }

  // ====== 右侧表格面板 ======
  &__table {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    min-width: 0;  // 重要：允许 flex 子项收缩
  }
}
</style>
