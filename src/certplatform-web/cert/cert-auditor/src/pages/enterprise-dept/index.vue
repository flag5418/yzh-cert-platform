<script setup lang="ts">
/**
 * 企业机构管理（配置驱动 CRUD 页面，路由 /enterprise-dept）
 *
 * 按企业建立部门树（不限层级），每部门带负责人信息。
 * 必须先选企业才能查（后端 OnBuildingFilter 强制：未选企业返回空）。
 *
 * ★ 顶部远程下拉选企业 → 自动加载该企业部门列表。
 *   部门名称列以缩进形式展示层级关系（ParentCode 建树）。
 */
import { ref, watch } from 'vue'
import { ElOption, ElSelect, ElSwitch } from 'element-plus'
import { YzhFormDialog, YzhStatusBadge, YzhTable, useSingleTable } from '@yzh-core'
import { EnterpriseDeptLogic } from './logic'
import { yzhApi } from '@yzh-core/api/client'

const { logic, tableRef } = useSingleTable(EnterpriseDeptLogic)

// ──── 企业选择 ────
interface EntOption { Code: string; Name: string }
const enterpriseOptions = ref<EntOption[]>([])
const selectedEnterpriseCode = ref<string>('')
const enterpriseLoading = ref(false)

async function loadEnterprises() {
  enterpriseLoading.value = true
  try {
    const res = await yzhApi.post('/api/Auditor/Enterprise/filter', {
      Page: 1,
      PageSize: 200,
      Filters: [{ Field: 'IsValid', Operator: 'eq', Value: '1' }]
    })
    if (res?.data?.Items) {
      enterpriseOptions.value = res.data.Items.map((e: any) => ({
        Code: e.Code,
        Name: e.Name
      }))
    }
  } finally {
    enterpriseLoading.value = false
  }
}
loadEnterprises()

// 监听企业选择 → 通过 logic filter 注入 EnterpriseCode 并刷新
watch(selectedEnterpriseCode, (code: string) => {
  // 通过 searchFields 机制注入 EnterpriseCode 过滤
  if (code) {
    logic.setFilter('EnterpriseCode', code)
  } else {
    logic.setFilter('EnterpriseCode', '')
  }
  logic.refresh()
})

// ──── 部门树形缩进展示 ────
// 树形缩进由 YzhTable 的 row-key + default-expand-all 配合后端 ParentCode 实现；
// 此处仅提供缩进前缀文本（depth 由行数据携带，未携带时退化为 0 级）。
interface DeptRow {
  Code: string
  DeptName: string
  ParentCode: string | null
  depth?: number
}

function deptIndent(node: DeptRow): string {
  const d = node.depth ?? 0
  return '　'.repeat(d * 2) + (d > 0 ? '└ ' : '')
}
</script>

<template>
  <div class="enterprise-dept-page">
    <!-- 顶部工具栏：企业选择 -->
    <div class="enterprise-dept-header">
      <span class="enterprise-dept-header__label">选择企业</span>
      <el-select
        v-model="selectedEnterpriseCode"
        placeholder="请先选择企业"
        filterable
        clearable
        :loading="enterpriseLoading"
        style="width: 320px"
      >
        <el-option
          v-for="ent in enterpriseOptions"
          :key="ent.Code"
          :label="ent.Name"
          :value="ent.Code"
        />
      </el-select>
    </div>

    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :search-max-fields="3"
      select-mode="multiple"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 部门名称列：带缩进展示层级 -->
      <template #column-DeptName="{ row }">
        <span class="dept-name-cell">
          <span class="dept-indent">{{ deptIndent(row) }}</span>
          <span class="dept-name">{{ row.DeptName }}</span>
        </span>
      </template>

      <!-- 启用状态列（S08：状态标签一律 YzhStatusBadge） -->
      <template #column-IsValid="{ row }">
        <YzhStatusBadge :type="row.IsValid === 1 ? 'success' : 'info'" :text="row.IsValid === 1 ? '启用' : '禁用'" />
      </template>

      <!-- 「显示已禁用」开关 -->
      <template #toolbar-right>
        <div class="enterprise-dept-switch">
          <span class="enterprise-dept-switch__label">显示已禁用</span>
          <el-switch
            :model-value="logic.showDisabled.value"
            @change="logic.toggleShowDisabled()"
          />
        </div>
      </template>
    </YzhTable>

    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="部门"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="760px"
      label-width="130px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.enterprise-dept-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.enterprise-dept-header {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: 0 0 var(--yzh-space-3, 12px) 0;
}

.enterprise-dept-header__label {
  font-size: var(--yzh-font-size-base, 14px);
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
}

.enterprise-dept-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.enterprise-dept-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.dept-name-cell {
  display: inline-flex;
  align-items: center;
}

.dept-indent {
  color: var(--el-text-color-secondary);
  font-family: monospace;
  white-space: pre;
}

.dept-name {
  font-weight: 500;
}
</style>
