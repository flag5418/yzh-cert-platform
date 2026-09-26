<script setup lang="ts">
/**
 * 企业管理（配置驱动 CRUD 页面，路由 `/enterprises`）
 *
 * 专家端所有业务的起点：先有企业，才有企业规则、企业文档、企业任务。
 *
 * useSingleTable 统一注入 logic/tableRef；按钮与动作全部由内核派发，
 * 页面无手写 handleAdd/handleBatchDelete/handleRowAction/handleSubmit。
 * 工作区隔离、企业编号生成、建档 2 表事务、组织树节点同步均在
 * 后端 EnterpriseController 内完成。
 *
 * ★ 启用 / 禁用（对齐后台管理 `system/organization`，同一套架构）：
 *   - 行按钮由 `Enterprise.json` 的 `RowButtons.CustomButtons` 声明
 *     `{ disable: 禁用, enable: 启用 }`，配合顶层 `EnableField: IsValid`
 *     → 内核 `toRowActions` 按 `row.IsValid` **二选一**渲染（不是「禁用/启用」一个按钮）
 *   - 点击 → 后端 `POST /action/disable|enable`（`RegisterRowAction` 注册）
 *   - ⛔ 不要用 `RowButtons.Enable = true`：那会额外追加一个 `toggle-valid`
 *     「禁用/启用」按钮，与上面的二选一按钮**重复**（后台 OrganizationController
 *     特意把它设为 false，本页同办）
 *   - ★ 禁用后该行默认从列表消失（`GetListAsync` 自动过滤 `IsValid=1`）
 *     → 必须配「显示已禁用」开关，否则禁用即**不可逆**（再也点不到「启用」）
 */
import { ElSwitch, ElTag } from 'element-plus'
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { EnterpriseLogic } from './logic'

const { logic, tableRef } = useSingleTable(EnterpriseLogic)
</script>

<template>
  <div class="enterprise-page">
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
      <!-- 启用状态列：渲染成标签（内核不格式化单元格，枚举/布尔会显示原始值） -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 「显示已禁用」开关：禁用行默认被后端过滤掉，靠它才能找回来并重新启用 -->
      <template #toolbar-right>
        <div class="enterprise-toolbar-switch">
          <span class="enterprise-toolbar-switch__label">显示已禁用</span>
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
      entity-name="企业"
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
.enterprise-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.enterprise-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.enterprise-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
