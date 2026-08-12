<template>
  <div class="prompt-verify-tab">
    <!-- 生成Prompt按钮 -->
    <div class="section-header">
      <h4>Prompt 生成与验证</h4>
      <el-button type="primary" @click="generatePrompt" :loading="generating">
        <el-icon><IconPrompt /></el-icon>
        生成 Prompt
      </el-button>
    </div>

    <!-- Prompt 编辑区 -->
    <div class="section">
      <div class="section-title"><el-icon class="section-title-icon"><IconPrompt /></el-icon>提取 Prompt</div>
      <el-input
        :model-value="prompt"
        @update:model-value="onPromptUpdate"
        type="textarea"
        :rows="12"
        placeholder="点击「生成 Prompt」按钮，AI 将根据字段和表格定义自动生成提取 Prompt..."
        class="prompt-editor"
      />
      <div class="prompt-hint">
        <el-icon><IconInfo /></el-icon>
        <span>您可以直接编辑生成的 Prompt，调整提取逻辑</span>
      </div>
    </div>

    <!-- 验证结果 -->
    <div class="section" v-if="verifyResult">
      <div class="section-title"><el-icon class="section-title-icon is-success"><IconCircleSuccess /></el-icon>验证结果</div>

      <el-alert
        :title="verifyResult.success ? '验证通过' : '验证失败'"
        :type="verifyResult.success ? 'success' : 'error'"
        :description="verifyResult.message"
        show-icon
        :closable="false"
      />

      <!-- 提取结果预览 -->
      <div v-if="verifyResult.data" class="extract-result">
        <div class="result-section">
          <h5>提取字段值</h5>
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item
              v-for="(value, key) in verifyResult.data.fields"
              :key="key"
              :label="key"
            >
              {{ value || '-' }}
            </el-descriptions-item>
          </el-descriptions>
        </div>

        <div class="result-section" v-if="verifyResult.data.tables">
          <h5>提取表格数据</h5>
          <div
            v-for="(table, name) in verifyResult.data.tables"
            :key="name"
            class="table-preview"
          >
            <div class="table-name">{{ name }}</div>
            <el-table :data="table" size="small" border>
              <el-table-column
                v-for="col in getTableColumns(table)"
                :key="col"
                :prop="col"
                :label="col"
              />
            </el-table>
          </div>
        </div>
      </div>
    </div>

    <!-- 验证按钮 -->
    <div class="verify-actions" v-if="prompt">
      <el-button
        type="success"
        @click="verifyPrompt"
        :loading="verifying"
        :disabled="!prompt"
      >
        <el-icon><IconCircleSuccess /></el-icon>
        验证 Prompt
      </el-button>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { IconPrompt, IconInfo, IconCircleSuccess } from '@/yzh';

const props = defineProps({
  prompt: {
    type: String,
    default: ''
  },
  verifyResult: {
    type: Object,
    default: null
  }
});

const emit = defineEmits(['generate', 'verify', 'update:prompt']);

const generating = ref(false);
const verifying = ref(false);

const onPromptUpdate = (val) => {
  emit('update:prompt', val);
};

const generatePrompt = async () => {
  generating.value = true;
  try {
    emit('generate');
  } finally {
    generating.value = false;
  }
};

const verifyPrompt = async () => {
  verifying.value = true;
  try {
    emit('verify');
  } finally {
    verifying.value = false;
  }
};

const getTableColumns = (tableData) => {
  if (!tableData || tableData.length === 0) return [];
  return Object.keys(tableData[0]);
};
</script>

<style scoped>
/* yzh 设计令牌 */
@import '@/yzh/styles/yzh.css';

.prompt-verify-tab {
  height: 100%;
  overflow-y: auto;
}

.section-title {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.section-title-icon {
  font-size: 14px;
  color: var(--yzh-color-text-secondary, #909399);
}

.section-title-icon.is-success {
  color: var(--yzh-color-success, #67c23a);
}

/* 区块头部 - 更精致 */
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  padding-bottom: 16px;
  border-bottom: 1px solid #ebeef5;
}

.section-header h4 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

/* 区块样式 */
.section {
  margin-bottom: 24px;
}

.section-title {
  margin-bottom: 12px;
  font-weight: 600;
  font-size: 14px;
  color: #606266;
}

/* Prompt 编辑器 - 更精致的代码编辑区 */
.prompt-editor :deep(.el-textarea__inner) {
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.7;
  background: #1e1e1e;
  color: #d4d4d4;
  border: 1px solid #3e3e3e;
  border-radius: 6px;
  padding: 16px;
}

.prompt-editor :deep(.el-textarea__inner:focus) {
  border-color: #409eff;
  box-shadow: 0 0 0 2px rgba(64, 158, 255, 0.2);
}

/* 提示信息 */
.prompt-hint {
  margin-top: 10px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #909399;
  padding: 10px 12px;
  background: #f5f7fa;
  border-radius: 6px;
}

.prompt-hint :deep(.el-icon) {
  color: #409eff;
}

/* 提取结果 */
.extract-result {
  margin-top: 20px;
  padding: 16px;
  background: #f5f7fa;
  border-radius: 8px;
  border: 1px solid #e4e7ed;
}

.result-section {
  margin-bottom: 20px;
}

.result-section:last-child {
  margin-bottom: 0;
}

.result-section h5 {
  margin: 0 0 12px 0;
  font-size: 14px;
  color: #303133;
  font-weight: 600;
  padding-bottom: 8px;
  border-bottom: 1px solid #ebeef5;
}

/* 表格预览 */
.table-preview {
  margin-bottom: 16px;
  background: #fff;
  padding: 12px;
  border-radius: 6px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
}

.table-name {
  font-weight: 600;
  margin-bottom: 10px;
  color: #606266;
  font-size: 13px;
}

/* 验证操作区 */
.verify-actions {
  display: flex;
  justify-content: center;
  padding: 20px;
  background: #f5f7fa;
  border-radius: 8px;
  margin-top: 20px;
}
</style>
