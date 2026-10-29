<script setup lang="ts">
/**
 * 条款属性配置（L1 · 左树 + 条款工作台）
 *
 * - 左树：标准 → 章 → 节 → 叶子条款（**完整四层**，章节是真的树节点）
 * - 右侧：条款详情（编号 / 标题 / 路径 / 原文）+ 该条款的属性清单
 * - 属性清单 = 属性池全集；勾选即配置、直接填值、一次保存
 * - `prompt` 类型（Artifacts / EvidenceChain）**在行内直接编辑**，⛔ 没有独立「编辑提示词」入口
 *
 * ★ 为什么右表改成「清单」：见 `logic.ts` 顶部注释（稀疏行表格与"未配置"语义冲突）。
 * ★ 只有叶子条款（NodeType=clause）能配属性；章 / 节点开是引导态，不写库。
 */
import { computed } from 'vue'
import {
  YzhDialog,
  YzhEmptyState,
  YzhStatusBadge,
  YzhTable,
  YzhTreeTableLayout,
  useTreeTable,
} from '@yzh-core'
import { MagicStick, Document } from '@element-plus/icons-vue'
import ClauseAttrLogic from './logic'
import type { AttrSheetRow } from './logic'
import PromptDrawer from './PromptDrawer.vue'

const { logic, treeTableRef } = useTreeTable(ClauseAttrLogic)

/**
 * 树就绪后才挂载布局。
 *
 * ⚠️ `default-expanded-keys`（树默认展开第一个标准 + 第一个章）**只在 el-tree 挂载时读一次**；
 * 首帧 `treeData` 还是空数组 ⇒ 早挂载会让展开全部失效（表现为整棵树折叠）。
 */
const treeReady = computed(() => logic.treeData.length > 0)

/** 值来源只有两档（24 号 §六）：固定值 = 标准已定死；参考值 = 必须从企业资料/证据取得 */
const valueSourceOptions = [
  { label: '固定值', value: 'fixed' },
  { label: '参考值', value: 'reference' },
]

/** 属性清单行的编辑器是否显示（取消勾选但仍存行时也要显示，用户才知道将删掉什么） */
function showEditor(row: AttrSheetRow): boolean {
  return row.Configured || !!row.Raw
}
</script>

<template>
  <div class="clause-attr-page">
    <!-- 树未就绪：占位（避免空数组首帧挂载导致默认展开失效） -->
    <div v-if="!treeReady" v-loading="true" class="clause-attr-page__booting" element-loading-text="正在加载条款树…" />

    <YzhTreeTableLayout
      v-else
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="320"
      tree-searchable
      status-field=""
      :tree-default-expanded-keys="logic.expandedKeys"
      @tree-node-click="logic.onNodeClick"
    >
      <!-- 树底部：整棵树级别动作 -->
      <template #treeFooter>
        <div class="clause-attr-tree-actions">
          <el-button type="default" @click="logic.refreshTree()">刷新树</el-button>
          <el-button type="primary" @click="logic.openGenerateDialog()">
            <el-icon><MagicStick /></el-icon>
            AI 预生成
          </el-button>
        </div>
      </template>

      <template #default>
        <!-- 顶部操作栏（始终可见，不依赖选中条款） -->
        <div class="clause-attr-toolbar">
          <span class="clause-attr-toolbar__hint">条款属性配置</span>
          <el-button type="default" size="small" @click="logic.promptDrawerVisible.value = true">
            <el-icon><Document /></el-icon>
            编辑提示词
          </el-button>
        </div>

        <!-- 未选中 -->
        <YzhEmptyState
          v-if="!logic.selectedNode"
          compact
          title="请选择左侧条款"
          description="左侧按「标准 → 章 → 节 → 条款」逐层展开；只有最末级条款才能配置属性。"
        />

        <div v-else class="clause-workbench">
          <!-- ① 条款详情 -->
          <section class="clause-workbench__head">
            <div class="clause-workbench__title-row">
              <YzhStatusBadge :type="logic.nodeBadgeType">{{ logic.nodeTypeLabel }}</YzhStatusBadge>
              <h2 class="clause-workbench__title">
                <span v-if="logic.selectedNumber" class="clause-workbench__number">{{ logic.selectedNumber }}</span>
                {{ logic.selectedTitle }}
              </h2>
            </div>

            <div class="clause-workbench__meta">
              <span class="clause-workbench__meta-label">路径</span>
              <span class="clause-workbench__meta-value">{{ logic.nodePath || '—' }}</span>
            </div>

            <div v-if="logic.isClauseNode" class="clause-workbench__meta">
              <span class="clause-workbench__meta-label">属性</span>
              <span class="clause-workbench__meta-value">
                <template v-if="logic.sheetLoading.value">加载中…</template>
                <template v-else>已配置 {{ logic.configuredCount }} / {{ logic.sheet.value.length }} 项</template>
              </span>
            </div>
            <div v-else class="clause-workbench__meta">
              <span class="clause-workbench__meta-label">下级条款</span>
              <span class="clause-workbench__meta-value">{{ logic.leafCountUnder }} 条</span>
            </div>
          </section>

          <!-- ② 条款原文 -->
          <section class="clause-workbench__desc">
            <div class="clause-workbench__desc-title">条款原文</div>
            <p v-if="logic.clauseDescription" class="clause-workbench__desc-text">
              {{ logic.clauseDescription }}
            </p>
            <p v-else class="clause-workbench__desc-empty">{{ logic.noDescriptionTip }}</p>
          </section>

          <!-- ③ 属性清单（仅叶子条款） -->
          <YzhEmptyState
            v-if="!logic.isClauseNode"
            compact
            title="该节点不能配置属性"
            description="章 / 节是汇总节点，本身不产生检查要求；展开后选择具体条款再配置。"
          />

          <section v-else class="clause-workbench__attrs">
            <div class="clause-workbench__attrs-head">
              <div class="clause-workbench__attrs-title">
                <span class="clause-workbench__attrs-name">条款属性</span>
                <span class="clause-workbench__attrs-hint">
                  勾选 = 当前条款采用该属性；填值即视为采用。取消勾选并保存 = 删除该条配置。
                </span>
              </div>
              <div class="clause-workbench__attrs-ops">
                <span v-if="logic.dirtyCount > 0" class="clause-workbench__dirty">
                  {{ logic.dirtyCount }} 项待保存
                </span>
                <el-button type="default" @click="logic.openGenerateForCurrent()">
                  AI 重生成此条款
                </el-button>
                <el-button type="default" :disabled="logic.dirtyCount === 0" @click="logic.resetSheet()">
                  撤销修改
                </el-button>
                <el-button
                  type="primary"
                  :loading="logic.sheetSaving.value"
                  :disabled="logic.dirtyCount === 0"
                  @click="logic.saveSheet()"
                >
                  保存属性
                </el-button>
              </div>
            </div>

            <div v-loading="logic.sheetLoading.value" class="clause-workbench__attr-list">
              <YzhEmptyState
                v-if="!logic.sheetLoading.value && logic.sheet.value.length === 0"
                compact
                title="属性池为空"
                description="请先到「条款属性池」维护属性定义，再回来配置。"
              />

              <div
                v-for="row in logic.sheet.value"
                :key="row.AttrCode"
                class="attr-item"
                :class="{ 'attr-item--on': row.Configured }"
              >
                <div class="attr-item__head">
                  <el-checkbox
                    :model-value="row.Configured"
                    @change="logic.toggleConfigured(row, $event)"
                  />
                  <span class="attr-item__name">{{ row.AttrName }}</span>
                  <code class="attr-item__code">{{ row.AttrCode }}</code>
                  <span class="attr-item__type">{{ row.ValueType }}</span>
                  <YzhStatusBadge v-if="row.Raw && row.IsValid === 0" type="info">已停用</YzhStatusBadge>
                </div>

                <div v-if="row.Remark" class="attr-item__remark">{{ row.Remark }}</div>

                <div v-if="showEditor(row)" class="attr-item__editor">
                  <!-- 值来源：固定值 / 参考值 -->
                  <div class="attr-item__field attr-item__field--source">
                    <span class="attr-item__label">值来源</span>
                    <el-select v-model="row.ValueSource" size="small">
                      <el-option
                        v-for="opt in valueSourceOptions"
                        :key="opt.value"
                        :label="opt.label"
                        :value="opt.value"
                      />
                    </el-select>
                  </div>

                  <!-- 值：按 ValueType 分派（text / prompt / dict / json / bool） -->
                  <div class="attr-item__field attr-item__field--value">
                    <span class="attr-item__label">值</span>

                    <!-- prompt：提示词本身就是值，行内直接编辑 -->
                    <el-input
                      v-if="row.ValueType === 'prompt'"
                      :model-value="row.AttrValue"
                      type="textarea"
                      :rows="5"
                      placeholder="在此写提示词正文（会被下游 AI 直接使用）"
                      @input="logic.setValue(row, $event)"
                    />

                    <!-- dict：字典单选 -->
                    <el-select
                      v-else-if="row.ValueType === 'dict'"
                      :model-value="row.AttrValue"
                      clearable
                      filterable
                      placeholder="请选择"
                      @change="logic.setValue(row, $event)"
                    >
                      <el-option
                        v-for="opt in logic.dictOptionsOf(row)"
                        :key="opt.value"
                        :label="opt.label"
                        :value="opt.value"
                      />
                    </el-select>

                    <!-- json：有字典 ⇒ 多选写回 JSON 数组；无字典 ⇒ 直接编辑 JSON 文本 -->
                    <el-select
                      v-else-if="row.ValueType === 'json' && row.DictCode"
                      :model-value="logic.jsonValueOf(row)"
                      multiple
                      filterable
                      collapse-tags
                      collapse-tags-tooltip
                      placeholder="请选择"
                      @change="logic.setJsonValue(row, $event)"
                    >
                      <el-option
                        v-for="opt in logic.dictOptionsOf(row)"
                        :key="opt.value"
                        :label="opt.label"
                        :value="opt.value"
                      />
                    </el-select>

                    <!-- bool：开关 -->
                    <el-switch
                      v-else-if="row.ValueType === 'bool'"
                      :model-value="logic.boolValueOf(row)"
                      @change="logic.setBoolValue(row, $event === true)"
                    />

                    <!-- text / 无字典 json：多行文本 -->
                    <el-input
                      v-else
                      :model-value="row.AttrValue"
                      type="textarea"
                      :rows="row.ValueType === 'json' ? 4 : 2"
                      :placeholder="row.ValueType === 'json' ? '填 JSON（如 [&quot;a&quot;,&quot;b&quot;]）' : '请填写'"
                      @input="logic.setValue(row, $event)"
                    />
                  </div>

                  <!-- 启用状态：只对已落库行有意义（新增行落库即启用） -->
                  <div v-if="row.Raw" class="attr-item__field attr-item__field--valid">
                    <span class="attr-item__label">启用</span>
                    <el-switch
                      :model-value="row.IsValid === 1"
                      @change="row.IsValid = $event === true ? 1 : 0"
                    />
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 提示词编辑抽屉（全局一条，对全部标准/条款生效） -->
    <PromptDrawer
      v-model="logic.promptDrawerVisible.value"
      @saved="logic.onPromptSaved()"
    />

    <!-- AI 预生成（P4）：服务端逐条串行跑，必须让用户看见进度 -->
    <YzhDialog
      v-model="logic.genVisible.value"
      title="AI 预生成条款属性"
      width="860px"
      :show-footer="false"
    >
      <div class="clause-gen">
        <div class="clause-gen__form">
          <div class="clause-gen__field">
            <span class="clause-gen__label">标准</span>
            <el-select v-model="logic.genStandardCode.value" placeholder="请选择标准" class="clause-gen__select">
              <el-option
                v-for="opt in logic.standardOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>
          <div class="clause-gen__field">
            <span class="clause-gen__label">条数</span>
            <el-input-number v-model="logic.genCount.value" :min="1" :max="50" size="small" />
          </div>
          <div class="clause-gen__field">
            <el-checkbox v-model="logic.genOverwrite.value">覆盖已配属性</el-checkbox>
          </div>
          <el-button
            type="primary"
            :loading="logic.genRunning.value"
            :disabled="logic.genRunning.value"
            @click="logic.runGenerate()"
          >
            {{ logic.genRunning.value ? `生成中… 已 ${logic.genElapsed.value}s` : '开始生成' }}
          </el-button>
        </div>

        <p class="clause-gen__tip">
          服务端按条款逐条串行调用 AI，条数越多耗时越长；生成结果只写属性值，不改属性池。
          <span v-if="logic.genClauseCodes.value && logic.genClauseCodes.value.length > 0">
            本次只跑指定的 {{ logic.genClauseCodes.value.length }} 条条款。
          </span>
        </p>

        <div v-if="logic.genResult.value" class="clause-gen__summary">
          <span>{{ logic.genResult.value.Message }}</span>
          <span v-if="logic.genResult.value.PromptScope === 'fallback'" class="clause-gen__warn">
            本次用的是代码内兜底提示词（库里还没保存过正文，抽屉里改的内容没生效）
          </span>
        </div>

        <YzhTable
          v-if="logic.genItems.value.length > 0"
          :columns="logic.genColumns"
          :data="logic.genItems.value"
          row-key="ClauseCode"
          :show-pagination="false"
          :toolbar="false"
          size="small"
        >
          <template #column-status="{ row }">
            <YzhStatusBadge :type="row.Success ? 'success' : 'danger'">
              {{ row.Success ? '成功' : '失败' }}
            </YzhStatusBadge>
          </template>
        </YzhTable>
      </div>
    </YzhDialog>
  </div>
</template>

<style scoped>
.clause-attr-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  box-sizing: border-box;
}

.clause-attr-page__booting {
  flex: 1;
  min-height: var(--yzh-space-12, 48px);
}

.clause-attr-tree-actions {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.clause-attr-tree-actions :deep(.el-button) {
  width: 100%;
  margin-left: 0;
}

.clause-workbench {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
  height: 100%;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
  box-sizing: border-box;
}

/* ---------- ① 条款详情 ---------- */

.clause-workbench__head {
  flex: none;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border: 1px solid var(--yzh-color-border, #e2e8f0);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-container, #ffffff);
}

/* ---------- 顶部操作栏 ---------- */

.clause-attr-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  background: var(--yzh-color-bg-container, #ffffff);
}

.clause-attr-toolbar :deep(.el-button) {
  margin-left: 0;
}

.clause-attr-toolbar__hint {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.clause-workbench__title-row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.clause-workbench__title {
  margin: 0;
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  line-height: var(--yzh-line-height-tight, 1.3);
}

.clause-workbench__number {
  margin-right: var(--yzh-space-1, 4px);
  color: var(--yzh-color-primary, #1e3a8a);
}

.clause-workbench__meta {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
}

.clause-workbench__meta-label {
  flex: none;
  color: var(--yzh-color-text-tertiary, #909399);
}

.clause-workbench__meta-value {
  color: var(--yzh-color-text-regular, #606266);
  word-break: break-all;
}

/* ---------- ② 条款原文 ---------- */

.clause-workbench__desc {
  flex: none;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
}

.clause-workbench__desc-title {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, #606266);
}

.clause-workbench__desc-text {
  margin: 0;
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.8);
  color: var(--yzh-color-text-body, #334155);
  white-space: pre-wrap;
}

.clause-workbench__desc-empty {
  margin: 0;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, #909399);
}

/* ---------- ③ 属性清单 ---------- */

.clause-workbench__attrs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.clause-workbench__attrs-head {
  flex: none;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  flex-wrap: wrap;
}

.clause-workbench__attrs-title {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.clause-workbench__attrs-name {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.clause-workbench__attrs-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.clause-workbench__attrs-ops {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.clause-workbench__attrs-ops :deep(.el-button) {
  margin-left: 0;
}

.clause-workbench__dirty {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-warning, #d97706);
}

.clause-workbench__attr-list {
  flex: 1;
  min-height: var(--yzh-space-12, 48px);
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

/* ---------- 单条属性 ---------- */

.attr-item {
  padding: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border, #e2e8f0);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-container, #ffffff);
  transition: border-color var(--yzh-transition-fast, 150ms ease);
}

.attr-item--on {
  border-color: var(--yzh-color-primary-light-7, #c6e2ff);
  background: var(--yzh-color-bg-active, #eff6ff);
}

.attr-item__head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.attr-item__name {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-text-primary, #303133);
}

.attr-item__code {
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-muted, #f3f4f6);
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-muted, #64748b);
}

.attr-item__type {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.attr-item__remark {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  line-height: var(--yzh-line-height-base, 1.6);
}

.attr-item__editor {
  display: flex;
  align-items: flex-start;
  gap: var(--yzh-space-3, 12px);
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border, #e2e8f0);
}

.attr-item__field {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.attr-item__field--source {
  flex: none;
  width: 120px;
}

.attr-item__field--valid {
  flex: none;
  width: 64px;
}

.attr-item__field--value {
  flex: 1;
  min-width: 0;
}

.attr-item__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

/* ---------- AI 预生成 ---------- */

.clause-gen {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
}

.clause-gen__form {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  flex-wrap: wrap;
}

.clause-gen__field {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.clause-gen__select {
  width: 260px;
}

.clause-gen__tip {
  margin: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.clause-gen__summary {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  flex-wrap: wrap;
}

.clause-gen__warn {
  color: var(--yzh-color-warning, #d97706);
}
</style>
