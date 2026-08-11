<template>
  <div class="file-tree">
    <div class="tree-header">
      <span class="header-title">📁 文件目录</span>
    </div>
    <div class="tree-content">
      <el-tree
        :data="treeData"
        :props="defaultProps"
        @node-click="onNodeClick"
        :highlight-current="true"
        :expand-on-click-node="false"
      >
        <template #default="{ node, data }">
          <div class="tree-node" :class="{ active: isActive(data) }">
            <el-icon class="node-icon" :class="data.type">
              <OfficeBuilding v-if="data.type === 'organization'" />
              <Document v-else-if="data.type === 'standard'" />
              <Calendar v-else-if="data.type === 'stage'" />
              <Folder v-else-if="data.type === 'folder'" />
              <Document v-else-if="data.type === 'file'" />
              <Folder v-else />
            </el-icon>
            <span class="node-label">{{ data.name }}</span>
            <!-- 转换状态标识（旧版 Office 格式） -->
            <el-tag
              v-if="data.type === 'file' && data.convertStatus"
              size="small"
              :type="getConvertTagType(data.convertStatus)"
              class="convert-status-tag"
              :title="getConvertStatusTitle(data.convertStatus)"
            >
              {{ getConvertStatusIcon(data.convertStatus) }}
            </el-tag>
            <!-- 文档规则状态标识 -->
            <span
              v-if="data.type === 'file'"
              class="status-icon"
              :class="data.ruleStatus"
              :title="getStatusTitle(data.ruleStatus)"
            >
              {{ getStatusIcon(data.ruleStatus) }}
            </span>
          </div>
        </template>
      </el-tree>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { OfficeBuilding, Document, Calendar, Folder } from '@element-plus/icons-vue';

const props = defineProps({
  data: {
    type: Array,
    default: () => []
  },
  currentFile: {
    type: Object,
    default: null
  }
});

const emit = defineEmits(['select', 'load-phase']);

const defaultProps = {
  children: 'children',
  label: 'name'
};

const treeData = computed(() => props.data);

const isActive = (data) => {
  return props.currentFile && props.currentFile.id === data.id;
};

const getStatusIcon = (status) => {
  const map = {
    none: '○',
    configured: '✓',
    failed: '✕'
  };
  return map[status] || '○';
};

const getStatusTitle = (status) => {
  const map = {
    none: '未制定规则',
    configured: '已制定规则',
    failed: '制定规则失败'
  };
  return map[status] || '';
};

// 转换状态相关方法
const getConvertStatusIcon = (status) => {
  const map = {
    pending: '⏳',
    converting: '🔄',
    completed: '✓',
    failed: '✕'
  };
  return map[status] || '';
};

const getConvertStatusTitle = (status) => {
  const map = {
    pending: '等待转换',
    converting: '正在转换',
    completed: '转换完成',
    failed: '转换失败'
  };
  return map[status] || '';
};

const getConvertTagType = (status) => {
  const map = {
    pending: 'info',
    converting: 'warning',
    completed: 'success',
    failed: 'danger'
  };
  return map[status] || 'info';
};

const onNodeClick = (data, node) => {
  console.log('[FileTree] onNodeClick:', { type: data.type, id: data.id, name: data.name, raw: data })
  if (data.type === 'file') {
    console.log('[FileTree] ✅ emit select →', data.id, data.name)
    emit('select', data);
  } else if (data.type === 'stage') {
    // 点击阶段节点，触发加载文件
    emit('load-phase', data);
    // 展开节点
    node.expanded = !node.expanded;
  } else {
    console.log(`[FileTree] type=${data.type} 不触发 select/load-phase，仅展开`)
    node.expanded = !node.expanded
  }
};
</script>

<style scoped>
.file-tree {
  height: 100%;
  display: flex;
  flex-direction: column;
}

/* 树头部 - 更精致 */
.tree-header {
  padding: 16px 20px;
  border-bottom: 1px solid #e4e7ed;
  font-weight: 600;
  font-size: 14px;
  color: #303133;
  background: linear-gradient(to right, #fff, #f5f7fa);
}

.tree-content {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

/* 树节点样式优化 */
.tree-node {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 0;
  transition: all 0.2s;
}

.tree-node.active {
  color: #409eff;
  font-weight: 500;
}

.node-icon {
  font-size: 16px;
  color: #909399;
  transition: color 0.2s;
}

.node-icon.organization {
  color: #e6a23c;
}

.node-icon.standard {
  color: #409eff;
}

.node-icon.stage {
  color: #67c23a;
}

.node-icon.folder {
  color: #fac858;
}

.node-icon.file {
  color: #909399;
}

.tree-node:hover .node-icon {
  color: #409eff;
}

.node-label {
  flex: 1;
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #606266;
}

.tree-node.active .node-label {
  color: #409eff;
}

/* 状态图标 - 更精致 */
.status-icon {
  font-size: 11px;
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  font-weight: 600;
  transition: all 0.2s;
}

.status-icon.none {
  color: #c0c4cc;
  background: #f5f7fa;
}

.status-icon.configured {
  color: #67c23a;
  background: #f0f9eb;
  box-shadow: 0 0 0 1px #e1f3d8;
}

.status-icon.failed {
  color: #f56c6c;
  background: #fef0f0;
  box-shadow: 0 0 0 1px #fde2e2;
}

/* 转换状态徽标样式 */
.convert-status-tag {
  margin-left: 4px;
  font-size: 12px;
  padding: 0 4px;
  height: 18px;
  line-height: 16px;
}

/* Element Tree 样式覆盖 */
:deep(.el-tree-node__content) {
  height: 36px;
  border-radius: 6px;
  margin: 2px 0;
  transition: all 0.2s;
}

:deep(.el-tree-node__content:hover) {
  background: #f5f7fa;
}

:deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: #ecf5ff;
}

:deep(.el-tree-node__expand-icon) {
  color: #909399;
  font-size: 14px;
}

:deep(.el-tree-node__expand-icon.is-leaf) {
  color: transparent;
}
</style>
