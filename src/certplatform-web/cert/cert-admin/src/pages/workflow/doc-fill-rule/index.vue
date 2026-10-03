<script setup lang="ts">
/**
 * ★ 标准文档填写规则（MENU_00218 → /business/doc-fill-rule）
 *
 * 左树 = 模板三级树（机构 → 标准·阶段 → 模板），右区两个标签页：
 *   - 「锚点规则」：`cert_doc_template_anchor` 的行级 CRUD（EntityConfig 驱动，零手写）
 *   - 「全文规则」：该模板挂的 `doc_fill_prompt` 版本列表 + 当前生效版本
 *
 * 架构（样板页面指南-V1 §四）：Logic 继承 TreeTableLogic，由 useTreeTable 注入；
 * 列/表单/搜索/行按钮全部来自后端 EntityConfig，页面无手写 CRUD handler。
 */
import { computed, ref, watch } from 'vue'
import { Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhTable, YzhFormDialog, YzhTreeTableLayout, useTreeTable, type TreeNode } from '@yzh-core'
import { DocFillRuleLogic } from './logic'
import RegisterTemplateDialog from './components/RegisterTemplateDialog.vue'
import PromptPanel from './components/PromptPanel.vue'

const { logic, tableRef, treeTableRef } = useTreeTable(DocFillRuleLogic)

const rowActions = computed(() => logic.rowActions)
const emptyText = computed(() => (logic.anySelected ? '暂无锚点规则，点「新建锚点」开始' : '请在左侧选择模板'))

/** 当前标签页。切到「全文规则」时靠 PromptPanel 的 key 变化触发重载 */
const activeTab = ref<'anchor' | 'prompt'>('anchor')
const registerVisible = ref(false)

async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

/** 切模板时回到「锚点规则」页签 —— 全文规则是模板级配置，留在旧页签容易误读 */
watch(
  () => logic.templateCode,
  () => {
    activeTab.value = 'anchor'
  },
)

/** 登记成功 → 刷新左树（新模板要立刻可见） */
async function handleRegistered() {
  await logic.reloadTree()
}
</script>

<template>
  <div class="doc-fill-rule-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="300"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="logic.nodeActions"
      @tree-node-click="handleNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <!-- 左树底部：登记空白模板（模板来自标准目录，⛔ 不在树上新建） -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" style="width: 100%" @click="registerVisible = true">
          登记模板
        </el-button>
      </template>

      <template #default>
        <div class="doc-fill-rule-page__content">
          <el-tabs v-model="activeTab" class="doc-fill-rule-page__tabs">
            <el-tab-pane label="锚点规则" name="anchor">
              <YzhTable
                ref="tableRef"
                :columns="logic.columns as any"
                :data-loader="logic.dataLoader.bind(logic)"
                :search-fields="logic.searchFields as any"
                :row-action-buttons="rowActions"
                :empty-text="emptyText"
                row-key="Code"
                @row-action="logic.onRowAction"
              >
                <!-- IsValid 是 int（0/1），内核会渲成裸数字 —— 必须给插槽 -->
                <template #column-IsValid="{ row }">
                  <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                    {{ row.IsValid === 1 ? '启用' : '禁用' }}
                  </el-tag>
                </template>

                <template #toolbar-left>
                  <el-button
                    type="primary"
                    :icon="Plus"
                    :disabled="!logic.anySelected"
                    @click="logic.onToolbarAction('add')"
                  >
                    新建锚点
                  </el-button>
                  <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
                </template>
              </YzhTable>
            </el-tab-pane>

            <el-tab-pane label="全文规则" name="prompt" :disabled="!logic.anySelected">
              <PromptPanel
                v-if="logic.anySelected"
                :key="logic.templateCode"
                :prompt-code="logic.promptCode"
                :org-code="logic.templateOrgCode"
                :template-name="logic.templateNode?.Name ?? ''"
              />
            </el-tab-pane>
          </el-tabs>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 编辑弹窗（★ 布局列数由 EntityConfig.FormCols 决定，勿硬编码） -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="锚点"
      :title="logic.dialogMode.value === 'add' ? '新建锚点规则' : '编辑锚点规则'"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="680px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="doc-fill-rule-form-header">
          <span class="doc-fill-rule-form-header__label">所属模板：</span>
          <span class="doc-fill-rule-form-header__value">
            {{ logic.templateNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>

    <RegisterTemplateDialog v-model:visible="registerVisible" @registered="handleRegistered" />
  </div>
</template>

<style scoped>
.doc-fill-rule-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.doc-fill-rule-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

/* 标签页头与内容之间留白，避免贴边 */
.doc-fill-rule-page__tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  padding: 0 12px;
}

.doc-fill-rule-page__tabs :deep(.el-tabs__header) {
  margin-bottom: 8px;
}

.doc-fill-rule-page__tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.doc-fill-rule-page__tabs :deep(.el-tab-pane) {
  height: 100%;
}

.doc-fill-rule-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.doc-fill-rule-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.doc-fill-rule-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
