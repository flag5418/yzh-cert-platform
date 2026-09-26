<script setup lang="ts">
/**
 * 企业-阶段-标准关联（专家平台 · 第 2 个业务功能）
 *
 * 左树：本工作区的企业（`YzhTree`，含「已关联 N 个标准」badge）
 * 右侧：「阶段 → 标准」勾选树表（`YzhTreeTableCheckSelector`）——**勾选即关联、取消即解除**
 *
 * ★ 本页走 core 的「关联型」范式（AS-1 / AS-2），参照实现
 *   `yzh.vue.core/src/pages/system/role-api/`（角色-接口授权），结构 1:1 对应。
 *   ⛔ 全选 / 取消全选 / 展开折叠 / 搜索 / 差集勾选 / 乐观更新 / 回滚 / badge
 *      全部由内核提供，**页面不得手写**。
 *
 * ★ 隔离由后端完成：`cert_enterprise_stage` 无 `OrgCode` 列，后端
 *   `OnBuildingFilter` 收敛到「本工作区的企业集合」。前端不传任何隔离参数。
 *
 * ★ 阶段数据源统一为 `cert_cert_stage`（菜单 MENU_00203「认证阶段定义」同源）；
 *   `cert_phase_definition` 是重复实现的孤儿表，本页不引用。
 *
 * ★ 展示字段说明：表格「标准编号」列绑 `StandardNo`（人读编号，如 `iso9001-2015`），
 *   **不是** `StandardCode`（那是 `cert_iso_standard.Code`，一串 GUID 关联键）。
 */
import { YzhTree, YzhTreeTableCheckSelector, useCheckTree } from '@yzh-core'
import { EnterpriseStageLogic } from './logic'

const { logic } = useCheckTree(EnterpriseStageLogic)

/** 节点类型文案（组件不内置业务文案，必须由调用方传入） */
const TYPE_LABELS = { stage: '阶段', standard: '标准' }
const TYPE_TAG_TYPES = { stage: 'primary' as const, standard: 'success' as const }

function handleNodeClick(data: any) {
  logic.handleNodeSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload)
}
</script>

<template>
  <div class="es-page">
    <!-- 左侧：企业树 -->
    <div class="es-page__tree-panel">
      <div class="es-page__tree-header">
        <span class="es-page__tree-title">企业</span>
      </div>
      <div class="es-page__tree-content">
        <YzhTree
          :data="logic.treeData.value"
          node-key="Code"
          searchable
          search-placeholder="搜索企业"
          @node-click="handleNodeClick"
        />
      </div>
    </div>

    <!-- 右侧：阶段 → 标准 勾选树表 -->
    <div class="es-page__table-panel">
      <div class="es-page__table-header">
        <span class="es-page__table-title">
          {{
            logic.selectedNode.value
              ? `阶段 / 标准关联（勾选即生效）— ${logic.selectedNode.value.Name}`
              : '请先选择左侧企业'
          }}
        </span>
        <span v-if="logic.saving.value" class="es-page__saving">保存中...</span>
      </div>

      <div class="es-page__table-content">
        <YzhTreeTableCheckSelector
          v-if="logic.selectedNode.value"
          :flat-data="logic.associationData.value"
          :columns="logic.columns"
          node-key="Code"
          parent-key="ParentCode"
          node-type-field="NodeType"
          check-field="CheckFlag"
          :type-labels="TYPE_LABELS"
          :type-tag-types="TYPE_TAG_TYPES"
          :check-all-exclude-types="['stage']"
          :default-expand-all="true"
          cascade
          searchable
          :search-fields="['Name', 'StandardNo', 'StandardName']"
          search-placeholder="搜索阶段 / 标准 / 编号"
          count-type="standard"
          @check-change="handleCheckChange"
        >
          <!-- 阶段行：阶段名 + 标准数；标准行：标准名称 -->
          <template #column-Name="{ row }">
            <template v-if="row.NodeType === 'stage'">
              <el-tag size="small" type="primary" effect="plain">{{ row.Name }}</el-tag>
              <span class="es-page__stage-count">{{ row.ChildCount ?? 0 }} 个标准</span>
            </template>
            <span v-else>{{ row.StandardName || row.Name }}</span>
          </template>
        </YzhTreeTableCheckSelector>
        <div v-else class="es-page__empty">
          <el-empty description="请先选择左侧企业" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.es-page {
  height: 100%;
  display: flex;
  overflow: hidden;
  background: #fff;
  border-radius: 4px;
}

/* ──── 左侧企业树 ──── */

.es-page__tree-panel {
  width: 300px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.es-page__tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.es-page__tree-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.es-page__tree-content {
  flex: 1;
  overflow: auto;
}

/* ──── 右侧勾选树表 ──── */

.es-page__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.es-page__table-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.es-page__table-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.es-page__saving {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--el-color-warning);
}

.es-page__table-content {
  flex: 1;
  overflow: auto;
}

.es-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.es-page__stage-count {
  margin-left: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
