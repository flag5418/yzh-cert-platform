<script setup lang="ts">
/**
 * 体系认证全局参数定义（后台管理 · ★ 左树右表）
 *
 * ★ 产品定位（用户 2026-10-02）：本页是「用程序把能力讲清楚」的第一块拼图 ——
 *   机构在这里**按「机构 × 标准 × 阶段」**预定义体系认证全局参数；
 *   企业端「企业全局参数定义」会自动把企业的基本信息与这批参数**关联成待完善清单**；
 *   文档填充时这批参数就是全部企业相关锚点的主要取值来源。
 *
 * ★ 页面形态（26 号 §3.1 方案 A「生效参数集」）：
 *   左树 机构 → 标准 → 阶段；右表列出**引擎实际会用到**的那批参数，每行带「作用域」列。
 *   选中「机构A + 9001 + 复审」时，右表 = 通用 + 9001 专属 + 复审专属 + 9001×复审专属，
 *   同一 ParamCode 只留最具体的一条（去重在后端 `ParamValueResolver.PickMostSpecific`）。
 *
 * 表 `cert_fill_param_def`｜菜单 `MENU_00217`｜路由 `/business/fill-param-def`
 */
import { computed } from 'vue'
import { Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable, type TreeNode } from '@yzh-core'
import { ElMessage } from 'element-plus'
import {
  SCOPE_KIND_LABEL,
  SCOPE_KIND_TAG,
  SHADOW_REASON_LABEL,
} from '@share/api/cert/fill-param-def'
import {
  FillParamDefLogic,
  MAINTAIN_MODE_LABEL,
  MAINTAIN_MODE_TAG,
  SOURCE_KIND_LABEL,
} from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(FillParamDefLogic)

const rowActions = computed(() => logic.rowActions)

/** 右表空态文案：区分「没选阶段」与「选了但确实没参数」 */
const emptyText = computed(() =>
  logic.anySelected ? '该作用域下暂无参数，可点「新增参数」' : '请在左侧选择「标准 → 阶段」',
)

/** 锚点语法速查（与后端 DocumentFillEngine 注册的 4 项能力一一对应） */
const ANCHORS = [
  { syntax: '{{param_code}}', name: '全局参数', note: '读企业完善后的参数值，企业可覆盖' },
  { syntax: '{{enterprise.Name}}', name: '替换', note: '直接取企业/机构属性，企业不可改' },
  { syntax: '{{@doc_no}}', name: '页眉页脚', note: '编号/版本/日期/页码，每页重复' },
  { syntax: '{{ai:quality_policy}}', name: 'AI 生成', note: '质量方针等段落，生成后落参数表复用' },
]

async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

/** 新增前校验：必须先选中阶段（右表数据的前置条件） */
function handleAdd() {
  if (!logic.anySelected) {
    ElMessage.warning('请先在左侧选择「标准 → 阶段」')
    return
  }
  logic.onToolbarAction('add')
}
</script>

<template>
  <div class="fill-param-def-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="290"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="logic.nodeActions"
      @tree-node-click="handleNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <template #default>
        <div class="fill-param-def-page__content">
          <!-- ① 当前作用域 + 生效集构成：让「这一屏参数是从哪来的」一眼可辨 -->
          <div class="fill-param-def-scope-bar">
            <div class="fill-param-def-scope-bar__left">
              <span class="fill-param-def-scope-bar__label">当前作用域</span>
              <span class="fill-param-def-scope-bar__value">{{ logic.scopeText }}</span>
            </div>
            <div v-if="logic.stats.value" class="fill-param-def-scope-bar__right">
              <el-tag size="small" type="primary" effect="plain">
                生效 {{ logic.stats.value.EffectiveCount }} 条
              </el-tag>
              <span class="fill-param-def-scope-bar__stat">
                通用 {{ logic.stats.value.ByScope.Common }}
              </span>
              <span class="fill-param-def-scope-bar__stat">
                标准专属 {{ logic.stats.value.ByScope.Standard }}
              </span>
              <span class="fill-param-def-scope-bar__stat">
                阶段专属 {{ logic.stats.value.ByScope.Stage }}
              </span>
              <span class="fill-param-def-scope-bar__stat">
                标准 × 阶段 {{ logic.stats.value.ByScope.StandardStage }}
              </span>
              <el-tag
                v-if="logic.stats.value.ShadowedCount"
                size="small"
                type="warning"
                effect="plain"
              >
                未生效 {{ logic.stats.value.ShadowedCount }}
              </el-tag>
            </div>
          </div>

          <!-- ② 锚点语法速查：本页配的参数最终会被哪些锚点消费 -->
          <el-alert type="info" :closable="false" class="fill-param-def-guide">
            <template #title>
              同一参数编码可配多条（通用 / 标准专属 / 阶段专属）—— 填充时自动取<strong>最具体</strong>的那条
            </template>
            <div class="fill-param-def-guide__anchors">
              <span class="fill-param-def-guide__anchors-title">填充锚点语法：</span>
              <span v-for="a in ANCHORS" :key="a.syntax" class="fill-param-def-guide__anchor">
                <code>{{ a.syntax }}</code>
                <span class="fill-param-def-guide__anchor-name">{{ a.name }}</span>
                <span class="fill-param-def-guide__anchor-note">{{ a.note }}</span>
              </span>
            </div>
          </el-alert>

          <!-- ③ 生效参数表 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columns"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields"
            :row-action-buttons="rowActions"
            :empty-text="emptyText"
            row-key="Code"
            select-mode="multiple"
            @selection-change="logic.onSelectionChange($event)"
            @row-action="logic.onRowAction"
          >
            <!-- ★ 作用域计算列：复用 EntityConfig 的「所属标准」列位 -->
            <template #column-StandardCode="{ row }">
              <div class="fill-param-def-scope">
                <el-tag
                  :type="SCOPE_KIND_TAG[row.ScopeKind] || 'info'"
                  size="small"
                  effect="plain"
                >
                  {{ SCOPE_KIND_LABEL[row.ScopeKind] || row.ScopeKind }}
                </el-tag>
                <span class="fill-param-def-scope__text">{{ row.ScopeText }}</span>
              </div>
              <div v-if="row.ShadowedScopes?.length" class="fill-param-def-scope__hint">
                覆写了：{{ row.ShadowedScopes.join('、') }}
              </div>
            </template>

            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
              <!-- 未生效行：说明「为什么我配的这条没生效」，避免管理员反复调试 -->
              <el-tooltip
                v-if="!row.IsEffective && row.ShadowReason"
                :content="SHADOW_REASON_LABEL[row.ShadowReason] || row.ShadowReason"
                placement="top"
              >
                <el-tag type="warning" size="small" effect="plain" class="fill-param-def-shadow-tag">
                  未生效
                </el-tag>
              </el-tooltip>
            </template>

            <template #column-IsBuiltin="{ row }">
              <el-tag v-if="row.IsBuiltin" type="warning" size="small">内置</el-tag>
              <span v-else class="fill-param-def-muted">—</span>
            </template>

            <template #column-SourceKind="{ row }">
              <el-tag size="small" effect="plain">
                {{ SOURCE_KIND_LABEL[row.SourceKind] || row.SourceKind }}
              </el-tag>
            </template>

            <template #column-MaintainMode="{ row }">
              <el-tag :type="MAINTAIN_MODE_TAG[row.MaintainMode] || 'info'" size="small">
                {{ MAINTAIN_MODE_LABEL[row.MaintainMode] || row.MaintainMode }}
              </el-tag>
            </template>

            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAdd">新增参数</el-button>
              <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
            </template>

            <template #toolbar-right>
              <div class="fill-param-def-toolbar">
                <el-input
                  v-model="logic.keyword.value"
                  placeholder="搜索参数编码 / 名称"
                  clearable
                  size="default"
                  style="width: 220px"
                  @keyup.enter="logic.applyKeyword()"
                  @clear="logic.applyKeyword()"
                />
                <div class="fill-param-def-toolbar__switch">
                  <span class="fill-param-def-toolbar__label">显示未生效的定义</span>
                  <el-switch
                    :model-value="logic.showDisabled.value"
                    @change="logic.toggleShowDisabled()"
                  />
                </div>
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="全局参数"
      :title="logic.dialogMode.value === 'add' ? '新增全局参数' : '编辑全局参数'"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="720px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="fill-param-def-form-header">
          <span class="fill-param-def-form-header__label">归属作用域：</span>
          <span class="fill-param-def-form-header__value">{{ logic.scopeText }}</span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.fill-param-def-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.fill-param-def-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

/* ── 作用域条 ── */
.fill-param-def-scope-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-wrap: wrap;
}

.fill-param-def-scope-bar__left {
  display: flex;
  align-items: baseline;
  gap: 8px;
  min-width: 0;
}

.fill-param-def-scope-bar__label {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.fill-param-def-scope-bar__value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.fill-param-def-scope-bar__right {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.fill-param-def-scope-bar__stat {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* ── 锚点语法提示 ── */
.fill-param-def-guide {
  margin: 10px 14px;
  width: auto;
}

.fill-param-def-guide__anchors {
  margin-top: 6px;
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 6px 14px;
}

.fill-param-def-guide__anchors-title {
  color: var(--el-text-color-regular);
}

.fill-param-def-guide__anchor {
  display: inline-flex;
  align-items: baseline;
  gap: 6px;
}

.fill-param-def-guide__anchor code {
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--el-fill-color);
  color: var(--el-color-primary);
  font-size: 12px;
}

.fill-param-def-guide__anchor-name {
  font-weight: 600;
}

.fill-param-def-guide__anchor-note {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

/* ── 作用域列 ── */
.fill-param-def-scope {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.fill-param-def-scope__text {
  font-size: 12px;
  color: var(--el-text-color-regular);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.fill-param-def-scope__hint {
  margin-top: 2px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.fill-param-def-shadow-tag {
  margin-left: 4px;
}

/* ── 工具栏 ── */
.fill-param-def-toolbar {
  display: flex;
  align-items: center;
  gap: 14px;
}

.fill-param-def-toolbar__switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.fill-param-def-toolbar__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.fill-param-def-muted {
  color: var(--el-text-color-placeholder);
}

/* ── 表单头部 ── */
.fill-param-def-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.fill-param-def-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.fill-param-def-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
