<template>
  <div class="ai-analysis-tab">
    <!-- AI 分析按钮 -->
    <div class="section-header">
      <h4>AI 自动分析</h4>
      <el-button
        type="primary"
        :loading="analyzing"
        @click="startAnalysis"
        :disabled="!hasSkills"
      >
        <el-icon><Aim /></el-icon>
        开始分析
      </el-button>
    </div>

    <el-alert
      v-if="!hasSkills"
      title="请先配置 AI 技能"
      type="info"
      :closable="false"
      show-icon
    />

    <!-- 提取字段列表 -->
    <div class="section">
      <div class="section-title">
        <span>📋 提取字段 ({{ localFields.length }})</span>
        <el-button size="small" @click="addField">
          <el-icon><Plus /></el-icon>添加
        </el-button>
      </div>

      <div class="field-list">
        <div
          v-for="(field, index) in localFields"
          :key="index"
          class="field-item"
          :class="{ 'manual': field.isManual }"
        >
          <div class="field-header">
            <el-input
              v-model="field.name"
              size="small"
              placeholder="字段名称"
              class="field-name"
            />
            <el-select
              v-model="field.dataType"
              size="small"
              placeholder="类型"
              class="field-type"
            >
              <el-option label="文本" value="string" />
              <el-option label="数字" value="number" />
              <el-option label="日期" value="date" />
              <el-option label="布尔" value="boolean" />
            </el-select>
            <el-button
              type="danger"
              size="small"
              circle
              @click="removeField(index)"
            >
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
          <div class="field-body">
            <el-input
              v-model="field.description"
              size="small"
              placeholder="字段描述（AI提取依据）"
              type="textarea"
              :rows="2"
            />
            <el-checkbox v-model="field.isManual" size="small">
              需手动补充
            </el-checkbox>
          </div>
        </div>
      </div>

      <el-empty v-if="localFields.length === 0" description="暂无字段，点击添加或AI分析" />
    </div>

    <!-- 提取表格列表 -->
    <div class="section">
      <div class="section-title">
        <span>📊 提取表格 ({{ localTables.length }})</span>
        <el-button size="small" @click="addTable">
          <el-icon><Plus /></el-icon>添加
        </el-button>
      </div>

      <div class="table-list">
        <div
          v-for="(table, index) in localTables"
          :key="index"
          class="table-item"
        >
          <div class="table-header">
            <el-input
              v-model="table.name"
              size="small"
              placeholder="表格名称"
              class="table-name"
            />
            <el-button
              type="danger"
              size="small"
              circle
              @click="removeTable(index)"
            >
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
          <div class="table-body">
            <el-input
              v-model="table.description"
              size="small"
              placeholder="表格描述（AI提取依据）"
              type="textarea"
              :rows="2"
            />

            <!-- 表格字段 -->
            <div class="table-fields">
              <div class="table-fields-header">
                <span>表格列定义</span>
                <el-button size="small" text @click="addTableField(index)">
                  <el-icon><Plus /></el-icon>添加列
                </el-button>
              </div>
              <div
                v-for="(col, colIndex) in table.columns"
                :key="colIndex"
                class="table-field-row"
              >
                <el-input
                  v-model="col.name"
                  size="small"
                  placeholder="列名"
                  class="col-name"
                />
                <el-select
                  v-model="col.dataType"
                  size="small"
                  placeholder="类型"
                  class="col-type"
                >
                  <el-option label="文本" value="string" />
                  <el-option label="数字" value="number" />
                  <el-option label="日期" value="date" />
                </el-select>
                <el-button
                  type="danger"
                  size="small"
                  text
                  @click="removeTableField(index, colIndex)"
                >
                  <el-icon><Delete /></el-icon>
                </el-button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <el-empty v-if="localTables.length === 0" description="暂无表格，点击添加或AI分析" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { Aim, Plus, Delete } from '@element-plus/icons-vue';
import { ElMessage } from 'element-plus';

const props = defineProps({
  fields: {
    type: Array,
    default: () => []
  },
  tables: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['analyze', 'update:fields', 'update:tables']);

const analyzing = ref(false);
const hasSkills = ref(true); // TODO: 从后端获取技能配置

const localFields = ref([]);
const localTables = ref([]);

// 同步 props 到本地
watch(() => props.fields, (val) => {
  localFields.value = JSON.parse(JSON.stringify(val));
}, { immediate: true, deep: true });

watch(() => props.tables, (val) => {
  localTables.value = JSON.parse(JSON.stringify(val));
}, { immediate: true, deep: true });

// 字段操作
const addField = () => {
  localFields.value.push({
    name: '',
    dataType: 'string',
    description: '',
    isManual: false
  });
  emit('update:fields', localFields.value);
};

const removeField = (index) => {
  localFields.value.splice(index, 1);
  emit('update:fields', localFields.value);
};

// 表格操作
const addTable = () => {
  localTables.value.push({
    name: '',
    description: '',
    columns: []
  });
  emit('update:tables', localTables.value);
};

const removeTable = (index) => {
  localTables.value.splice(index, 1);
  emit('update:tables', localTables.value);
};

// 表格字段操作
const addTableField = (tableIndex) => {
  localTables.value[tableIndex].columns.push({
    name: '',
    dataType: 'string'
  });
  emit('update:tables', localTables.value);
};

const removeTableField = (tableIndex, colIndex) => {
  localTables.value[tableIndex].columns.splice(colIndex, 1);
  emit('update:tables', localTables.value);
};

// AI分析
const startAnalysis = async () => {
  analyzing.value = true;
  try {
    emit('analyze');
  } finally {
    analyzing.value = false;
  }
};
</script>

<style scoped>
.ai-analysis-tab {
  height: 100%;
  overflow-y: auto;
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
  margin-bottom: 28px;
}

.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
  font-weight: 600;
  font-size: 14px;
  color: #606266;
}

/* 字段和表格列表 */
.field-list, .table-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* 字段项 - 更现代的卡片 */
.field-item, .table-item {
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  padding: 16px;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  transition: all 0.3s;
}

.field-item:hover, .table-item:hover {
  border-color: #c6e2ff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.1);
}

.field-item.manual {
  border-left: 3px solid #e6a23c;
  background: linear-gradient(to right, #fdf6ec, #fff);
}

/* 字段头部 */
.field-header, .table-header {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
  align-items: center;
}

.field-name, .table-name {
  flex: 1;
}

.field-type, .col-type {
  width: 100px;
}

/* 字段内容区 */
.field-body, .table-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

/* 表格字段区 */
.table-fields {
  margin-top: 12px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 6px;
  border: 1px dashed #dcdfe6;
}

.table-fields-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
  font-size: 12px;
  color: #606266;
  font-weight: 500;
}

.table-field-row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  align-items: center;
}

.col-name {
  flex: 1;
}

/* 空状态优化 */
:deep(.el-empty) {
  padding: 40px 0;
}

:deep(.el-empty__description) {
  color: #909399;
  font-size: 13px;
}
</style>
