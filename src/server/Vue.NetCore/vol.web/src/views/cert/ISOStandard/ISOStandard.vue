<!--
  ISO 标准管理 —— YZH V2.1 左树右表落地页

  布局：
  ┌──────────────────────────────────────────────┐
  │  ISO 标准管理：管理各认证机构可开展的ISO标准    │
  ├──────────┬───────────────────────────────────┤
  │          │  [搜索: 标准编号/名称] [状态▼]     │
  │ 认证机构  ├───────────────────────────────────┤
  │ 📂 A机构  │  标准编号  |  标准名称  |  版本 ...│
  │ 📂 B机构  │  ISO13485  |  医疗器械...| 2026  │
  │ 📂 C机构  │  ISO9001   |  质量管理.. | 2026  │
  └──────────┴───────────────────────────────────┘

  设计原则：
  - 左侧：认证机构列表（点击切换，右侧自动过滤）
  - 右侧：该机构下的 ISO 标准 CRUD 表格
  - 新增时自动填入当前选中的机构 Code
-->
<template>
  <YzhTreeTable
    ref="treeTableRef"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    page-key="ISOStandard"
    tree-controller-name="CertCertificationBody"
    filter-field="CbCode"
    tree-label-field="Name"
    tree-key-field="Code"
    tree-title="认证机构"
    :show-node-count="true"
    :incremental-update="true"
    :search-mode="'fixed'"
    @ready="onReady"
    @tree-node-select="onTreeNodeSelect"
  >
    <!-- 头部提示 -->
    <template #treeTableHeader>
      <el-alert
        title="ISO 标准管理：选择左侧机构，管理该机构可开展认证的 ISO 标准"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>

    <!-- 自定义树节点（显示机构简称 + 状态） -->
    <template #treeNode="{ node, data }">
      <div class="iso-tree-node">
        <span class="iso-tree-node__name">{{ data.Name }}</span>
        <el-tag v-if="data.Status" size="small" :type="getStatusTagType(data.Status)">
          {{ getStatusText(data.Status) }}
        </el-tag>
      </div>
    </template>
  </YzhTreeTable>
</template>

<script setup lang="ts">
import { ref, markRaw, onMounted } from 'vue'
import { YzhTreeTable } from '@/yzh/index'
import viewOptions from './options.js'

const treeTableRef = ref()

// —— ① 实体 Schema ——
const schema = Object.freeze({
  keyField: 'Id',
  keyType: 'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'ISOStandard',
  tableName: 'cert_iso_standard',
  statusTagColors: { Status: 'standard_status' },
})

// —— ② 生命周期钩子 ——
const lifecycles = markRaw({
  /**
   * 页面加载后：加载左侧机构树数据
   */
  onReady: async (instance: any) => {
    console.log('[ISOStandard] TreeTable ready, loading tree data...')
    // YzhTreeTable 会根据 treeControllerName 自动加载树数据
    // 如果需要自定义加载逻辑，可以在这里调用 instance.loadTree()
  },

  /** 新增前：额外校验是否已选机构 */
  onAddBefore: async (form: any) => {
    if (!form.CbCode) {
      // 理论上不会走到这里（基类已自动填充），但做防御性检查
      return false
    }
    return true
  },
})

// —— ③ 事件处理 ——
function onReady(instance: any) {
  console.log('[ISOStandard] YzhTreeTable 实例就绪:', instance)
}

function onTreeNodeSelect(node: any, data: any) {
  console.log('[ISOStandard] 切换机构:', data.Name, '(', data.Code, ')')
}

// —— ④ 辅助方法（树节点渲染用）——
/** 机构状态 Tag 类型映射 */
function getStatusTagType(status: string): '' | 'success' | 'warning' | 'danger' | 'info' {
  const map: Record<string, any> = {
    active: 'success',
    suspended: 'warning',
    cancelled: 'info',
  }
  return map[status] || 'info'
}

/** 机构状态文本 */
function getStatusText(status: string): string {
  const map: Record<string, string> = {
    active: '正常',
    suspended: '停用',
    cancelled: '注销',
  }
  return map[status] || status
}
</script>

<style lang="less" scoped>
.iso-tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;

  &__name {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 13px;
  }
}
</style>
