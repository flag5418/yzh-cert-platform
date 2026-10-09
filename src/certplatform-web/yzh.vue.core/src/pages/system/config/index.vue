<script setup lang="ts">
/**
 * 系统参数配置（配置驱动 CRUD 页面）
 *
 * 设计原则：
 * - 前端不硬编码任何按钮/列/字段，全部由后端 EntityConfig 配置驱动
 * - 分页/排序/搜索条件由 YzhTable 传入 → SingleTableCore.dataLoader
 * - 编辑/删除/启停由内核 dispatch 统一派发（删除/启停自带二次确认）
 *
 * ★ 默认按 `Sort` 升序（2026-10-09 修，用户报障「视觉模型还未配置」）：
 *   此前**不传** `default-sort` ⇒ 后端 `SqlSugarDbOrm` 判 `SortField` 为空就**不加 ORDER BY**
 *   ⇒ 实际按 **Id 升序**（= 插入顺序）⇒ 晚插入的参数（如 `ai_vision_config`，Id=52）
 *   落到**第 2 页**，打开页面看不见 ⇒ 被误判成「这个参数没配」。
 *   `cert_sys_config.Sort` 列本来就是为显示顺序准备的（10/11/…/20/…/100），
 *   不传排序等于**这一列白配**。⚠️ 本页的 `Sort` 语义 = 「配置项展示顺序」。
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { ConfigLogic } from './logic'

// 实例化 Logic（useSingleTable 统一注入 tableRef）
const { logic, tableRef } = useSingleTable(ConfigLogic)
</script>

<template>
  <div class="config-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :default-sort="{ prop: 'Sort', order: 'asc' }"
      select-mode="multiple"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 状态列：IsValid 启用/禁用标签（列本身被内核标记为 slot，不给插槽会渲原值 1/0） -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 显示已禁用：后端默认过滤 IsValid=0，不开这个开关禁用即不可逆 -->
      <template #toolbar-right>
        <div class="config-toolbar-switch">
          <span class="config-toolbar-switch__label">显示已禁用</span>
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
      entity-name="参数"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.config-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.config-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.config-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
