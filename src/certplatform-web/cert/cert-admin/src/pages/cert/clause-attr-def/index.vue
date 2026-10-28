<script setup lang="ts">
/**
 * 条款属性池（L0 · 配置驱动 CRUD）
 *
 * - 单表：`cert_clause_attr_def` —— "世界上有哪些属性"，跨标准跨条款复用
 * - 右表列：属性码 / 属性名 / 值类型 / 字典编码 / 作用 / 排序 / 状态
 * - 新增/编辑弹窗：字段来自 `/config`，`DictCode` 由 Logic 按值类型联动必填
 * - ⛔ 本页没有「值来源」「适用条款」—— 那是「条款属性配置」页（L1）的事
 */
import { YzhFormDialog, YzhStatusBadge, YzhTable, useSingleTable } from '@yzh-core'
import ClauseAttrDefLogic from './logic'

const { logic, tableRef } = useSingleTable(ClauseAttrDefLogic)
</script>

<template>
  <div class="clause-attr-def-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      select-mode="multiple"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 状态列：IsValid 启用/禁用徽标（法条 S08：⛔ 裸 el-tag） -->
      <template #column-IsValid="{ row }">
        <YzhStatusBadge :type="row.IsValid === 1 ? 'success' : 'info'">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </YzhStatusBadge>
      </template>

      <!-- 显示已禁用：后端默认过滤 IsValid=0，不开这个开关禁用即不可逆 -->
      <template #toolbar-right>
        <div class="clause-attr-def-switch">
          <span class="clause-attr-def-switch__label">显示已禁用</span>
          <el-switch
            :model-value="logic.showDisabled.value"
            @change="logic.toggleShowDisabled()"
          />
        </div>
      </template>
    </YzhTable>

    <!-- 属性新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="条款属性"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="760px"
      @submit="logic.submitForm()"
    >
      <!-- 值类型说明：告诉配置者「这个类型将来会被谁消费」，避免乱选 -->
      <template #prepend>
        <div v-if="logic.valueTypeTip" class="clause-attr-def-tip">
          <span class="clause-attr-def-tip__tag">{{ logic.currentValueType }}</span>
          <span class="clause-attr-def-tip__text">{{ logic.valueTypeTip }}</span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.clause-attr-def-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.clause-attr-def-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.clause-attr-def-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.clause-attr-def-tip {
  display: flex;
  align-items: flex-start;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--el-fill-color-light);
  border-left: 3px solid var(--el-color-primary);
}

.clause-attr-def-tip__tag {
  flex: none;
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-color-primary);
  font-weight: 600;
}

.clause-attr-def-tip__text {
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.6;
  color: var(--el-text-color-regular);
}
</style>
