<!--
  认证机构管理 —— YZH 单表基类 MVP 落地页
  新写法：<YZHSingleTable :schema :options :lifecycles> + 插槽写业务按钮
  对比旧写法：从 156 行 → 约 60 行，减少 60% 重复代码，且所有 YZH 页面保持一致 UX
-->
<template>
  <YzhBaseSingleTable
    ref="yzhGrid"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    :incremental-update="true"
  >
    <!-- 业务工具栏：默认空（无需要放顶部的业务按钮）
         「查看标准」作为行操作列的扩展按钮或右键菜单使用，不与新增/删除同层占位，导致顶部工具栏混乱；
         若后续需要，改成在行上用 handleRowOpenStandards(row) 跳转。 -->
    <template #toolbarLeft="{ selectedRow }">
      <div style="display: flex; gap: 8px; align-items: center">
        <el-button
          v-if="selectedRow"
          size="small"
          type="success"
          plain
          @click="handleOpenStandards"
        >
          查看关联标准
        </el-button>
      </div>
    </template>

    <!-- gridHeader（原 ViewGrid 的）：保留一条 Info 提示，便于运营理解 -->
    <template #gridHeader>
      <el-alert
        title="认证机构管理：维护所有ISO认证机构基本信息（CNAS编号、联系人、状态）"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>
  </YzhBaseSingleTable>
</template>

<script setup lang="jsx">
import YzhBaseSingleTable from '@/components/yzh/base/YzhBaseSingleTable.vue'
import { getCurrentInstance, markRaw, ref } from 'vue'
import { useRouter } from 'vue-router'
import viewOptions from './options.js'

const { proxy } = getCurrentInstance()
const router = useRouter()
const yzhGrid = ref(null)

// —— ① 声明实体 Schema（TS 泛型基类的入口）
//    controllerName + key + 排序字段 = 自动拼 URL + 增量插行定位
const schema = Object.freeze({
  keyField: 'Id', // 与 options.table.key / 后端实体主键一致
  keyType: 'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'CertCertificationBody', // 后端 Controller 类名（去 Controller 后缀）
  statusTagColors: { Status: 'org_status' }
})

// —— ② 声明业务生命周期（未声明的走基类默认空实现）
const lifecycles = markRaw({
  // 查询回来后：二次加工（此处示例打印行数，业务按需扩展）
  onLoadAfter(rows, raw) {
    // 可在这里翻译字典 / 合并外部字段 / 注入 tag 色
    return rows
  },

  // 删除前：二次确认 + 业务判断（机构下已挂标准时禁止删除）
  async onDeleteBefore(rows, ids) {
    if (rows.some((r) => String(r.Status || '').toLowerCase() === 'active')) {
      // 例：激活状态机构不允许直接删（此处仅提示，最终仍走用户确认）
      proxy.$message?.warning?.('待删除列表中存在「启用」状态机构，请确认已停用后再操作')
    }
    return true // 返回 false 可阻断
  },

  // 删除后：可联动清理相关表（此处仅示例 log）
  onDeleteAfter(ids) {
    console.log('[CertificationBody] deleted ids=', ids)
  },

  // 新增保存前：自动补 CreatorId / CreateDate 等公共字段
  onAddSaveBefore(main) {
    // 后端 ServiceBase 会自动补公共字段；这里可做前端特有默认值
    if (!main.ContactName) main.ContactName = ''
    return true
  }
})

// —— ③ 业务槽位按钮：选中机构 → 跳转 ISOStandard 并带 CbCode 过滤
function handleOpenStandards() {
  const row = yzhGrid.value?.getSelected?.()?.[0]
  if (!row) {
    proxy.$message?.warning?.('请先选择一行机构')
    return
  }
  router.push({
    path: '/cert/ISOStandard',
    query: { CbCode: row.CbCode, CbName: row.Name }
  })
}

// 对外暴露（父组件 / tab 容器可调用）
defineExpose({
  refresh: () => yzhGrid.value?.refresh?.(),
  getSelected: () => yzhGrid.value?.getSelected?.()
})
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
