<template>
  <div class="execution-result-panel">
    <div class="result-header">
      <div class="result-title">
        <el-icon :class="result.isSuccess ? 'icon-success' : 'icon-fail'">
          <IconCircleCheck v-if="result.isSuccess" />
          <IconClose v-else />
        </el-icon>
        <span>执行结果</span>
        <el-tag :type="result.isSuccess ? 'success' : 'danger'" size="small">
          {{ result.status }}
        </el-tag>
        <span class="duration">耗时 {{ result.durationMs }}ms</span>
      </div>
      <el-button link size="small" @click="$emit('close')"><el-icon><IconClose /></el-icon></el-button>
    </div>

    <div class="result-body">
      <!-- NC 结果摘要 -->
      <div class="nc-result-section">
        <div class="section-title">NC 结果</div>
        <div class="nc-summary">
          <div class="nc-row">
            <span class="nc-label">成功:</span>
            <el-tag :type="result.ncResult?.success ? 'success' : 'danger'" size="small">
              {{ result.ncResult?.success ? '是' : '否' }}
            </el-tag>
          </div>
          <div v-if="result.ncResult?.error" class="nc-row">
            <span class="nc-label">错误:</span>
            <span class="nc-value error-text">{{ result.ncResult.error }}</span>
          </div>
          <div v-if="result.ncResult?.result" class="nc-row">
            <span class="nc-label">结果:</span>
            <pre class="nc-json">{{ formatJson(result.ncResult.result) }}</pre>
          </div>
        </div>
      </div>

      <!-- 路径执行详情 -->
      <div class="paths-section">
        <div class="section-title">路径执行详情 ({{ result.pathResults?.length || 0 }} 条)</div>
        <el-collapse v-model="activePaths">
          <el-collapse-item
            v-for="path in result.pathResults"
            :key="path.pathIndex"
            :name="path.pathIndex"
          >
            <template #title>
              <div class="path-header">
                <el-tag :type="path.status === 'completed' ? 'success' : 'danger'" size="small">
                  {{ path.status }}
                </el-tag>
                <span class="path-index">路径 {{ path.pathIndex + 1 }}</span>
                <span class="path-nodes">{{ path.nodeIds?.join(' → ') }}</span>
              </div>
            </template>
            <div class="path-detail">
              <div v-if="path.error" class="path-error">
                <span>失败节点: {{ path.failedAtNodeId }}</span>
                <span>错误: {{ path.error }}</span>
              </div>
              <div v-if="path.output" class="path-output">
                <span>输出:</span>
                <pre class="nc-json">{{ formatJson(path.output) }}</pre>
              </div>
            </div>
          </el-collapse-item>
        </el-collapse>
      </div>

      <!-- 任务信息 -->
      <div class="task-info-section">
        <div class="section-title">任务信息</div>
        <div class="task-info">
          <div><span class="info-label">TaskCode:</span> {{ result.taskCode }}</div>
          <div><span class="info-label">ItemCode:</span> {{ result.itemCode }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { IconCircleCheck, IconClose } from '@/yzh/icons'

const props = defineProps({
  result: { type: Object, required: true }
})
defineEmits(['close'])

const activePaths = ref([0])

function formatJson(obj) {
  if (!obj) return ''
  try {
    return typeof obj === 'string' ? JSON.stringify(JSON.parse(obj), null, 2) : JSON.stringify(obj, null, 2)
  } catch {
    return String(obj)
  }
}
</script>

<style scoped lang="less">
.execution-result-panel {
  max-height: 300px;
  overflow-y: auto;
  border-top: 2px solid #409eff;
  background: #fafbfc;
  padding: 12px 16px;
  font-size: 13px;
}

.result-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.result-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  .icon-success { color: #67C23A; }
  .icon-fail { color: #F56C6C; }
}

.duration { color: #909399; font-size: 12px; font-weight: normal; }

.result-body { display: flex; flex-direction: column; gap: 12px; }

.section-title {
  font-size: 12px; font-weight: 600; color: #606266;
  margin-bottom: 6px; padding-bottom: 4px;
  border-bottom: 1px solid #ebeef5;
}

.nc-summary { display: flex; flex-direction: column; gap: 4px; }
.nc-row { display: flex; align-items: flex-start; gap: 8px; }
.nc-label { color: #909399; min-width: 50px; font-size: 12px; }
.nc-value { flex: 1; }
.error-text { color: #F56C6C; }

.nc-json {
  background: #f5f7fa; padding: 8px; border-radius: 4px;
  font-size: 11px; font-family: monospace;
  max-height: 150px; overflow-y: auto;
  margin: 0; flex: 1;
}

.path-header {
  display: flex; align-items: center; gap: 8px;
  .path-index { font-weight: 600; }
  .path-nodes { color: #909399; font-size: 12px; }
}

.path-detail {
  display: flex; flex-direction: column; gap: 8px; padding: 8px 0;
}

.path-error {
  display: flex; flex-direction: column; gap: 4px;
  color: #F56C6C; font-size: 12px;
}

.task-info {
  display: flex; gap: 16px; font-size: 12px; color: #909399;
  .info-label { color: #606266; }
}
</style>
