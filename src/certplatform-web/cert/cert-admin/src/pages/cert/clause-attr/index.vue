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
import PromptDrawer from './PromptDrawer.vue'

const { logic, treeTableRef } = useTreeTable(ClauseAttrLogic)

/**
 * 树就绪后才挂载布局。
 *
 * ⚠️ `default-expanded-keys`（树默认展开第一个标准 + 第一个章）**只在 el-tree 挂载时读一次**；
 * 首帧 `treeData` 还是空数组 ⇒ 早挂载会让展开全部失效（表现为整棵树折叠）。
 */
const treeReady = computed(() => logic.treeData.length > 0)

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
        </div>
      </template>

      <template #default>
        <!-- 顶部操作栏（始终可见，不依赖选中条款） -->
        <div class="clause-attr-toolbar">
          <span class="clause-attr-toolbar__hint">条款属性配置</span>
          <div class="clause-attr-toolbar__ops">
            <el-button type="default" size="small" @click="logic.promptDrawerVisible.value = true">
              <el-icon><Document /></el-icon>
              编辑提示词
            </el-button>
            <el-button type="primary" size="small" @click="logic.openGenerateDialog()">
              <el-icon><MagicStick /></el-icon>
              AI 预生成
            </el-button>
          </div>
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
                <template v-else>已采用 {{ logic.adoptedAttrs.length }} 项（池内共 {{ logic.sheet.value.length }} 项）</template>
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
                  只列本条款「已采用」的属性；不适用的属性不要添加（留空即不适用）。
                </span>
              </div>
              <div class="clause-workbench__attrs-ops">
                <el-select
                  :model-value="''"
                  size="small"
                  placeholder="＋ 添加属性"
                  class="clause-workbench__add"
                  :disabled="logic.addableAttrs.length === 0"
                  @change="logic.addAttr($event)"
                >
                  <el-option
                    v-for="row in logic.addableAttrs"
                    :key="row.AttrCode"
                    :label="row.AttrName"
                    :value="row.AttrCode"
                  />
                </el-select>
              </div>
            </div>

            <div v-loading="logic.sheetLoading.value" class="clause-workbench__attr-list">
              <YzhEmptyState
                v-if="!logic.sheetLoading.value && logic.adoptedAttrs.length === 0"
                compact
                title="该条款还没有采用任何属性"
                description="点右上角「＋ 添加属性」逐条添加；不添加 = 该条款没有要写的量化属性。"
              />

              <div
                v-for="row in logic.adoptedAttrs"
                :key="row.AttrCode"
                class="attr-item"
              >
                <div class="attr-item__head">
                  <span class="attr-item__name">{{ row.AttrName }}</span>
                  <code class="attr-item__code">{{ row.AttrCode }}</code>
                  <span class="attr-item__type">{{ row.ValueType }}</span>
                  <span class="attr-item__spacer" />
                  <el-button size="small" type="danger" text @click="logic.removeAttr(row)">
                    移除
                  </el-button>
                </div>

                <div v-if="row.Remark" class="attr-item__remark">{{ row.Remark }}</div>

                <div class="attr-item__editor">
                  <div class="attr-item__field attr-item__field--value">
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
                      size="small"
                      placeholder="请选择"
                      class="attr-item__dict"
                      @change="logic.setValue(row, $event)"
                    >
                      <el-option
                        v-for="opt in logic.dictOptionsOf(row)"
                        :key="opt.value"
                        :label="opt.label"
                        :value="opt.value"
                      />
                    </el-select>

                    <!-- json：有字典 ⇒ 多选写回 JSON 数组 -->
                    <el-select
                      v-else-if="row.ValueType === 'json' && row.DictCode"
                      :model-value="logic.jsonValueOf(row)"
                      multiple
                      filterable
                      collapse-tags
                      collapse-tags-tooltip
                      size="small"
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
                      :placeholder="row.ValueType === 'json' ? '填 JSON 数组，如 [management，production]' : '请填写'"
                      @input="logic.setValue(row, $event)"
                    />
                  </div>
                </div>
              </div>
            </div>

            <!-- 保存条：置底（改动与保存永远在同一屏） -->
            <div class="clause-workbench__attrs-footer">
              <span v-if="logic.dirtyCount > 0" class="clause-workbench__dirty">
                {{ logic.dirtyCount }} 项待保存
              </span>
              <span v-else class="clause-workbench__attrs-hint">无未保存改动</span>
              <div class="clause-workbench__footer-ops">
                <el-button
                  type="default"
                  size="small"
                  :disabled="logic.dirtyCount === 0"
                  @click="logic.resetSheet()"
                >
                  撤销修改
                </el-button>
                <el-button
                  type="primary"
                  size="small"
                  :loading="logic.sheetSaving.value"
                  :disabled="logic.dirtyCount === 0"
                  @click="logic.saveSheet()"
                >
                  保存属性
                </el-button>
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
            <el-select
              v-model="logic.genStandardCode.value"
              placeholder="请选择标准"
              class="clause-gen__select"
              @change="logic.onGenStandardChange()"
            >
              <el-option
                v-for="opt in logic.standardOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>
          <div class="clause-gen__field">
            <el-checkbox v-model="logic.genOverwrite.value">覆盖已配属性</el-checkbox>
          </div>
          <el-button
            v-if="!logic.genRunning.value"
            type="primary"
            @click="logic.runGenerate()"
          >
            开始生成
          </el-button>
          <template v-else>
            <el-button type="danger" plain @click="logic.stopGenerate()">停止</el-button>
            <span class="clause-gen__progress">
              {{ logic.genDone.value }} / {{ logic.genTotal.value }} 条 · {{ logic.genElapsed.value }}s
            </span>
          </template>
        </div>

        <!-- 条款多选：勾了这些就只跑这些；不勾 = 按条数从头跑 -->
        <div class="clause-gen__picker">
          <div class="clause-gen__picker-head">
            <span class="clause-gen__label">条款范围</span>
            <span class="clause-gen__picker-hint">
              {{
                logic.genSelectedCodes.value.length > 0
                  ? `已选 ${logic.genSelectedCodes.value.length} 条（只跑这些）`
                  : `未选 = 全量跑（该标准共 ${logic.genClauseOptions.length} 条叶子条款）`
              }}
            </span>
            <el-button size="small" type="primary" text @click="logic.selectAllGenClauses()">全选</el-button>
            <el-button size="small" type="primary" text @click="logic.clearGenClauses()">清空</el-button>
            <el-button
              v-if="logic.isClauseNode"
              size="small"
              type="primary"
              text
              @click="logic.pickCurrentClause()"
            >
              只跑当前条款
            </el-button>
          </div>
          <el-select
            v-model="logic.genSelectedCodes.value"
            multiple
            filterable
            collapse-tags
            collapse-tags-tooltip
            placeholder="不选 = 不限定条款"
            class="clause-gen__clauses"
            :disabled="logic.genRunning.value"
          >
            <el-option
              v-for="opt in logic.genClauseOptions"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </div>

        <p class="clause-gen__tip">
          服务端逐条串行调用 AI（单条约数秒）；超过 50 条会自动分批连续跑，可随时停止，已写入的保留。
          生成结果只写属性值，不改属性池。
          <span v-if="logic.genSelectedCodes.value.length > 0">
            本次只跑指定的 {{ logic.genSelectedCodes.value.length }} 条条款。
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
          height="420"
          size="small"
        >
          <template #column-status="{ row }">
            <YzhStatusBadge :type="row.Success ? 'success' : 'danger'">
              {{ row.Success ? '成功' : '失败' }}
            </YzhStatusBadge>
          </template>
          <template #column-ops="{ row }">
            <el-button size="small" text type="primary" @click="logic.jumpToClause(row)">
              查看
            </el-button>
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

.clause-attr-toolbar__ops {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.clause-attr-toolbar__ops :deep(.el-button) {
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

.clause-workbench__add {
  width: 132px;
}

.clause-workbench__add :deep(.el-select__placeholder) {
  color: var(--yzh-color-primary, #1e3a8a);
}

.clause-workbench__attrs-footer {
  position: sticky;
  bottom: 0;
  z-index: var(--yzh-z-sticky, 10);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  margin-top: var(--yzh-space-2, 8px);
  border-top: 1px solid var(--yzh-color-border, #e2e8f0);
  background: var(--yzh-color-bg-card, #ffffff);
}

.clause-workbench__footer-ops {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.clause-workbench__footer-ops :deep(.el-button) {
  margin-left: 0;
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

.attr-item__spacer {
  flex: 1;
  min-width: var(--yzh-space-2, 8px);
}

.attr-item__dict {
  width: 220px;
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

.clause-gen__picker {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--yzh-color-bg-subtle);
}

.clause-gen__picker-head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.clause-gen__picker-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.clause-gen__progress {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.clause-gen__clauses {
  width: 100%;
}
</style>
