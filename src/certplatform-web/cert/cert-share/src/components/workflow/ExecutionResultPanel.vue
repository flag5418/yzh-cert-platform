<!--
  ExecutionResultPanel.vue — 工作流执行结果面板（共享层）

  数据来源：`POST /api/Workflow/test/run`（整流）的 `data`，契约见
  `CertPlatform.Admin/Services/Workflow/Models/TaskExecutionModels.cs`。

  层次（阶段四 2026-09-22 扩展）：
    路径执行详情
      └─ 路径 N          状态 / 节点链 / 耗时 / 复用节点数
           ├─ 路径级错误与最终输出
           └─ 节点级明细子表   节点 / 类型 / 状态 / 耗时 / 复用 / 输出折叠   ← 本次新增

  为什么加节点级子表：`PathResult.NodeResults` 早在阶段一就已随响应返回（中间节点的
  输出/耗时/时序/复用标记都在里面），但面板只渲染到路径级，等于把已有数据丢了。
  这里把它用起来，**不需要新增任何请求**。

  节点类型/标题的展示依赖 `NodeId`（格式 `{classCode}_n{序号}`，如 `docField_n1`），
  取前缀映射为中文类型名；取不到时原样展示，不阻塞渲染。
-->
<template>
  <div class="execution-result-panel">
    <div class="result-header">
      <div class="result-title">
        <el-icon :class="result.IsSuccess ? 'icon-success' : 'icon-fail'">
          <CircleCheck v-if="result.IsSuccess" /><Close v-else />
        </el-icon>
        <span>执行结果</span>
        <el-tag :type="result.IsSuccess ? 'success' : 'danger'" size="small">{{ result.Status }}</el-tag>
        <span class="duration">耗时 {{ result.DurationMs }}ms</span>
      </div>
      <el-button link size="small" @click="$emit('close')"><el-icon><Close /></el-icon></el-button>
    </div>
    <div class="result-body">
      <div class="nc-result-section">
        <div class="section-title">NC 结果</div>
        <div class="nc-summary">
          <!-- NcResult 内层键为工作流引擎聚合出的运行时载荷键（camelCase，已登记例外，非实体字段） -->
          <div class="nc-row"><span class="nc-label">成功:</span><el-tag :type="result.NcResult?.success ? 'success' : 'danger'" size="small">{{ result.NcResult?.success ? '是' : '否' }}</el-tag></div>
          <div v-if="result.NcResult?.error" class="nc-row"><span class="nc-label">错误:</span><span class="nc-value error-text">{{ result.NcResult.error }}</span></div>
          <div v-if="result.NcResult?.result" class="nc-row"><span class="nc-label">结果:</span><pre class="nc-json">{{ formatJson(result.NcResult.result) }}</pre></div>
        </div>
      </div>
      <div class="paths-section">
        <div class="section-title">路径执行详情 ({{ result.PathResults?.length || 0 }} 条)</div>
        <el-collapse v-model="activePaths">
          <el-collapse-item v-for="path in result.PathResults" :key="path.PathIndex" :name="path.PathIndex">
            <template #title>
              <div class="path-header">
                <el-tag :type="path.Status === 'completed' ? 'success' : 'danger'" size="small">{{ path.Status }}</el-tag>
                <span class="path-index">路径 {{ path.PathIndex + 1 }}</span>
                <span class="path-nodes">{{ path.NodeIds?.join(' → ') }}</span>
                <span class="path-meta">{{ path.DurationMs }}ms</span>
                <el-tag v-if="reusedCount(path) > 0" type="info" size="small">复用 {{ reusedCount(path) }} 个节点</el-tag>
              </div>
            </template>
            <div class="path-detail">
              <div v-if="path.Error" class="path-error"><span>失败节点: {{ path.FailedAtNodeId }}</span><span>错误: {{ path.Error }}</span></div>

              <!-- 节点级明细：数据早已在响应里（NodeResults），此前未渲染 -->
              <div v-if="path.NodeResults?.length" class="node-detail">
                <div class="node-detail-title">节点级明细 ({{ path.NodeResults.length }} 个)</div>
                <el-table :data="path.NodeResults" size="small" border class="node-table">
                  <el-table-column label="节点" min-width="150">
                    <template #default="{ row }">
                      <div class="node-cell">
                        <span class="node-id">{{ row.NodeId }}</span>
                        <span class="node-type">{{ typeLabel(row.NodeId) }}</span>
                      </div>
                    </template>
                  </el-table-column>
                  <el-table-column label="状态" width="80" align="center">
                    <template #default="{ row }">
                      <el-tag :type="row.Success ? 'success' : 'danger'" size="small">{{ row.Success ? '成功' : '失败' }}</el-tag>
                    </template>
                  </el-table-column>
                  <el-table-column label="耗时" width="80" align="right">
                    <template #default="{ row }">{{ row.DurationMs }}ms</template>
                  </el-table-column>
                  <el-table-column label="LLM耗时" width="90" align="right">
                    <template #default="{ row }">
                      <span v-if="row.LlmDurationMs != null" class="llm-dur">{{ row.LlmDurationMs }}ms</span>
                      <span v-else class="muted">—</span>
                    </template>
                  </el-table-column>
                  <el-table-column label="Tokens" width="110" align="right">
                    <template #default="{ row }">
                      <span v-if="row.PromptTokens != null" class="token-badge" :title="`Prompt: ${row.PromptTokens} / Completion: ${row.CompletionTokens}`">
                        {{ row.PromptTokens }} / {{ row.CompletionTokens }}
                      </span>
                      <span v-else class="muted">—</span>
                    </template>
                  </el-table-column>
                  <el-table-column label="复用" width="64" align="center">
                    <template #default="{ row }">
                      <el-tag v-if="row.IsReused" type="info" size="small">复用</el-tag>
                      <span v-else class="muted">—</span>
                    </template>
                  </el-table-column>
                  <el-table-column label="输出 / 错误" min-width="200">
                    <template #default="{ row }">
                      <span v-if="row.Error" class="error-text">{{ row.Error }}</span>
                      <el-popover v-else-if="row.Output && Object.keys(row.Output).length" placement="left" :width="420" trigger="click">
                        <template #reference><el-button link size="small">查看输出</el-button></template>
                        <pre class="nc-json popover-json">{{ formatJson(row.Output) }}</pre>
                      </el-popover>
                      <span v-else class="muted">—</span>
                    </template>
                  </el-table-column>
                </el-table>
              </div>

              <div v-if="path.Output && Object.keys(path.Output).length" class="path-output"><span>输出:</span><pre class="nc-json">{{ formatJson(path.Output) }}</pre></div>
            </div>
          </el-collapse-item>
        </el-collapse>
      </div>
      <div class="task-info-section">
        <div class="section-title">任务信息</div>
        <div class="task-info">
          <div><span class="info-label">TaskCode:</span> {{ result.TaskCode }}</div>
          <div><span class="info-label">ItemCode:</span> {{ result.ItemCode }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { CircleCheck, Close } from '@element-plus/icons-vue'

defineProps<{ result: any }>()
defineEmits<{ close: [] }>()
const activePaths = ref([0])

/** 本路径复用的节点数（阶段三起同一信息也落在 wf_path_execution.ReusedCount） */
function reusedCount(path: any): number {
  return (path?.NodeResults || []).filter((n: any) => n.IsReused).length
}

/**
 * NodeId 前缀 → 中文类型名
 * NodeId 格式为 `{classCode}_n{序号}`（如 docField_n1），与 specialNodes.ts 的 classCode 一致。
 * 映射不到时返回空串（模板里不渲染），不阻塞展示。
 */
const TYPE_LABELS: Record<string, string> = {
  start: '开始', end: '结束', branch: '分支', loop: '循环',
  ai_node: 'AI', docField: '字段提取', docTable: '表格提取',
  compare: '值比较', assemble: '文本拼接', constant: '常量', skill: '功能',
}
function typeLabel(nodeId: string): string {
  const prefix = String(nodeId || '').split('_n')[0]
  return TYPE_LABELS[prefix] || ''
}

function formatJson(obj: any) {
  if (!obj) return ''
  try { return typeof obj === 'string' ? JSON.stringify(JSON.parse(obj), null, 2) : JSON.stringify(obj, null, 2) } catch { return String(obj) }
}
</script>

<style scoped lang="less">
.execution-result-panel { max-height: 300px; overflow-y: auto; border-top: 2px solid #409eff; background: #fafbfc; padding: 12px 16px; font-size: 13px; }
.result-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.result-title { display: flex; align-items: center; gap: 8px; font-weight: 600; .icon-success { color: #67C23A; } .icon-fail { color: #F56C6C; } }
.duration { color: #909399; font-size: 12px; font-weight: normal; }
.result-body { display: flex; flex-direction: column; gap: 12px; }
.section-title { font-size: 12px; font-weight: 600; color: #606266; margin-bottom: 6px; padding-bottom: 4px; border-bottom: 1px solid #ebeef5; }
.nc-summary { display: flex; flex-direction: column; gap: 4px; }
.nc-row { display: flex; align-items: flex-start; gap: 8px; }
.nc-label { color: #909399; min-width: 50px; font-size: 12px; }
.error-text { color: #F56C6C; }
.muted { color: #c0c4cc; }
.nc-json { background: #f5f7fa; padding: 8px; border-radius: 4px; font-size: 11px; font-family: monospace; max-height: 150px; overflow-y: auto; margin: 0; flex: 1; }
.popover-json { max-height: 320px; }
.path-header { display: flex; align-items: center; gap: 8px; .path-index { font-weight: 600; } .path-nodes { color: #909399; font-size: 12px; } .path-meta { color: #c0c4cc; font-size: 12px; } }
.path-detail { display: flex; flex-direction: column; gap: 8px; padding: 8px 0; }
.path-error { display: flex; flex-direction: column; gap: 4px; color: #F56C6C; font-size: 12px; }
.path-output { display: flex; gap: 8px; font-size: 12px; color: #909399; }
.node-detail-title { font-size: 12px; color: #909399; margin-bottom: 4px; }
.node-table { font-size: 12px; }
.node-cell { display: flex; flex-direction: column; line-height: 1.3; .node-id { font-family: monospace; } .node-type { color: #909399; font-size: 11px; } }
.task-info { display: flex; gap: 16px; font-size: 12px; color: #909399; .info-label { color: #606266; } }
.token-badge { font-family: monospace; font-size: 11px; color: #409eff; background: #ecf5ff; padding: 1px 4px; border-radius: 3px; }
.llm-dur { font-size: 11px; color: #e6a23c; }
</style>
