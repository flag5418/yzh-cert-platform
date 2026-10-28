<script setup lang="ts">
/**
 * 条款属性配置（L1 · 左树右表）
 *
 * - 左树：标准 → **叶子条款**（208 条；只列叶子，非叶子条款不产生检查要求）
 * - 右表：选中条款已配的属性（一行 = 某条款上的某个属性，没配 = 没行）
 * - 新增/编辑：`AttrCode` 从属性池下拉选；`ValueSource` 只有 固定 / 参考 两档
 * - 提示词：`prompt` 类型属性走「编辑提示词」抽屉写长文本（⛔ 表格里塞 textarea）
 *
 * ★ 为什么 Artifacts 和 EvidenceChain 是两条独立属性（不是一条）：
 *   前者是企业侧「应提供哪些资料」（可公开），后者是审核员侧「怎么取证」（取证剧本，
 *   不提前给企业）。合成一条 ⇒ 企业照着剧本准备 ⇒ 审核失效。
 */
import { YzhDrawer, YzhFormDialog, YzhStatusBadge, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import ClauseAttrLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(ClauseAttrLogic)
</script>

<template>
  <div class="clause-attr-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expanded-keys="logic.expandedKeys"
      :node-actions="logic.nodeActions"
      @tree-node-click="logic.onNodeClick"
    >
      <template #default>
        <div class="clause-attr-page__table">
          <YzhTable
            ref="tableRef"
            :columns="logic.columns"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields"
            :toolbar-actions="logic.toolbarActions"
            :row-action-buttons="logic.rowActions"
            :empty-text="logic.emptyText"
            select-mode="multiple"
            row-key="Code"
            @selection-change="logic.onSelectionChange($event)"
            @row-action="logic.onRowAction"
            @toolbar-action="logic.onToolbarAction"
          >
            <!-- 属性列：显示中文属性名 + 裸 Code 副行（⛔ 不让人对着 Artifacts 猜） -->
            <template #column-AttrCode="{ row }">
              <div class="clause-attr-cell">
                <span class="clause-attr-cell__name">{{ logic.attrNameOf(row.AttrCode) }}</span>
                <span class="clause-attr-cell__code">{{ row.AttrCode }}</span>
              </div>
            </template>

            <!-- 状态列：IsValid 启用/禁用徽标（法条 S08：⛔ 裸 el-tag） -->
            <template #column-IsValid="{ row }">
              <YzhStatusBadge :type="row.IsValid === 1 ? 'success' : 'info'">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </YzhStatusBadge>
            </template>

            <!-- 显示已禁用：后端默认过滤 IsValid=0，不开这个开关禁用即不可逆 -->
            <template #toolbar-right>
              <div class="clause-attr-switch">
                <span class="clause-attr-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 属性配置新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="条款属性"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="820px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="clause-attr-form-header">
          <span class="clause-attr-form-header__label">归属条款：</span>
          <span class="clause-attr-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>

    <!-- 提示词抽屉：长文本专用（prompt 类型的值就是提示词正文） -->
    <YzhDrawer
      v-model="logic.promptDrawerVisible.value"
      title="编辑提示词"
      size="820px"
      :confirm-loading="logic.promptSaving.value"
      @confirm="logic.savePrompt()"
    >
      <div class="clause-attr-prompt">
        <div class="clause-attr-prompt__hint">
          <div class="clause-attr-prompt__title">
            {{ logic.promptAttrName }}
            <span class="clause-attr-prompt__code">{{ logic.promptAttrCode }}</span>
          </div>
          <div class="clause-attr-prompt__rules">
            三条铁律：① 要求内部输出结构化 JSON ② 只产「读到什么」不产结论
            ③ 找不到就交空，禁推测。
          </div>
        </div>
        <el-input
          v-model="logic.promptDraft.value"
          type="textarea"
          :rows="22"
          resize="vertical"
          placeholder="在此写提示词正文（可多行）。"
        />
      </div>
    </YzhDrawer>
  </div>
</template>

<style scoped>
/* ★ `YzhTreeTableLayout` 右面板是 overflow:hidden ⇒ 放入页必须自给滚动容器，
   否则内容被裁 + 整页滚不动（见 MEMORY §9 第一坑） */
.clause-attr-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.clause-attr-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

/* 第二坑：右面板子项会被 flex 压缩 ⇒ 显式禁压缩 */
.clause-attr-page__table > * {
  flex: none;
}

.clause-attr-cell {
  display: flex;
  flex-direction: column;
  line-height: 1.35;
}

.clause-attr-cell__name {
  color: var(--el-text-color-primary);
}

.clause-attr-cell__code {
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

.clause-attr-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.clause-attr-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.clause-attr-form-header {
  display: flex;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.clause-attr-form-header__label {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.clause-attr-form-header__value {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-primary);
  font-weight: 500;
}

.clause-attr-prompt {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
  height: 100%;
}

.clause-attr-prompt__hint {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--el-fill-color-light);
  border-left: 3px solid var(--el-color-primary);
}

.clause-attr-prompt__title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.clause-attr-prompt__code {
  margin-left: var(--yzh-space-2, 8px);
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 400;
  color: var(--el-text-color-secondary);
}

.clause-attr-prompt__rules {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.6;
  color: var(--el-text-color-regular);
}
</style>
