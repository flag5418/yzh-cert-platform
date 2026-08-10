<template>
  <div class="doc-preview">
    <div class="preview-header">
      <div class="file-info">
        <el-icon class="file-icon"><Document /></el-icon>
        <span class="file-name">{{ file.name }}</span>
        <el-tag size="small" type="info">{{ fileTypeText }}</el-tag>
      </div>
      <el-button size="small" @click="refresh">
        <el-icon><Refresh /></el-icon>刷新
      </el-button>
    </div>
    <div class="preview-content">
      <!-- 图片预览 -->
      <template v-if="isImage">
        <el-image
          :src="previewUrl"
          :preview-src-list="[previewUrl]"
          fit="contain"
          class="image-preview"
        />
      </template>

      <!-- Word文档（仅支持 .docx 格式） -->
      <template v-else-if="isDocx">
        <vue-office-docx
          :src="previewUrl"
          style="height: 100%"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- 旧版 .doc 格式不支持预览 -->
      <template v-else-if="isOldDoc">
        <div class="unsupported">
          <el-icon :size="48"><Document /></el-icon>
          <p>旧版 Word (.doc) 格式暂不支持在线预览</p>
          <p class="tip">提示：可下载后用 WPS 或 Microsoft Word 打开</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>

      <!-- Excel表格 -->
      <template v-else-if="isExcel">
        <vue-office-excel
          :src="previewUrl"
          style="height: 100%"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- PDF文档 -->
      <template v-else-if="isPdf">
        <vue-office-pdf
          :src="previewUrl"
          style="height: 100%"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- 文本文件 -->
      <template v-else-if="isText">
        <pre class="text-content">{{ textContent }}</pre>
      </template>

      <!-- 旧版 .xls 格式提示 -->
      <template v-else-if="ext === 'xls'">
        <div class="unsupported">
          <el-icon :size="48"><Document /></el-icon>
          <p>旧版 Excel (.xls) 格式暂不支持在线预览</p>
          <p class="tip">提示：可下载后用 WPS 或 Microsoft Excel 打开</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>

      <!-- 不支持的格式 -->
      <template v-else>
        <div class="unsupported">
          <el-icon :size="48"><Document /></el-icon>
          <p>该文件格式暂不支持预览</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { Document, Refresh } from '@element-plus/icons-vue';
import { ElMessage } from 'element-plus';

// vue-office 组件
import VueOfficeDocx from '@vue-office/docx';
import VueOfficeExcel from '@vue-office/excel';
import VueOfficePdf from '@vue-office/pdf';
import '@vue-office/docx/lib/index.css';
import '@vue-office/excel/lib/index.css';
// 注意：@vue-office/pdf 不需要 CSS 文件

const props = defineProps({
  file: {
    type: Object,
    required: true
  }
});

const previewUrl = ref('');
const textContent = ref('');

// 文件类型判断
const ext = computed(() => {
  const name = props.file.name || '';
  return name.split('.').pop().toLowerCase();
});

const isImage = computed(() => ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext.value));
// 注意：vue-office/docx 只支持 .docx 格式（基于XML/ZIP）
// 旧版 .doc 是 OLE2 二进制格式，不支持预览，只能下载
const isDocx = computed(() => ext.value === 'docx');
const isOldDoc = computed(() => ext.value === 'doc'); // 旧版 Word 格式
const isExcel = computed(() => ['xlsx', 'xls'].includes(ext.value)); // csv 用文本方式处理
const isPdf = computed(() => ext.value === 'pdf');
const isText = computed(() => ['txt', 'md', 'json', 'xml', 'csv'].includes(ext.value));

const fileTypeText = computed(() => {
  const map = {
    doc: 'Word 文档(旧版)',
    docx: 'Word 文档',
    xlsx: 'Excel 表格',
    xls: 'Excel 表格(旧版)',
    csv: 'CSV 文件',
    pdf: 'PDF 文档',
    txt: '文本文件',
    md: 'Markdown',
    jpg: '图片',
    jpeg: '图片',
    png: '图片'
  };
  return map[ext.value] || ext.value.toUpperCase() + ' 文件';
});

/** 是否为不支持预览的旧格式 */
const isOldFormat = computed(() => isOldDoc.value || ext.value === 'xls');

/**
 * 构建文件预览/下载URL
 * 使用后端 StandardDirectoryController 的 download 接口
 */
const buildFileUrl = () => {
  if (!props.file) return '';
  
  // 优先使用 storagePath（MinIO 路径）
  const path = props.file.storagePath || props.file.StoragePath || '';
  if (path) {
    // 使用后端 download 接口，通过 path 参数获取文件
    return `/api/standard-directory/download?path=${encodeURIComponent(path)}`;
  }
  
  // 回退：使用 fileCode（如果后端支持的话）
  const fileCode = props.file.fileCode || props.file.FileCode || '';
  if (fileCode) {
    return `/api/standard-directory/download?path=${encodeURIComponent(fileCode)}`;
  }
  
  return '';
};

// 加载预览
const loadPreview = async () => {
  if (!props.file) return;

  // 构建预览URL（从MinIO获取）
  previewUrl.value = buildFileUrl();
  console.log('📄 预览URL:', previewUrl.value, '文件类型:', ext.value);

  // 文本文件直接加载内容
  if (isText.value) {
    try {
      const response = await fetch(previewUrl.value);
      textContent.value = await response.text();
    } catch (e) {
      textContent.value = '加载失败: ' + e.message;
    }
  }
};

const onRendered = () => {
  console.log('文档渲染完成');
};

const onError = (e) => {
  ElMessage.error('文档预览失败: ' + e.message);
};

const refresh = () => {
  loadPreview();
};

const download = () => {
  window.open(previewUrl.value, '_blank');
};

watch(() => props.file, loadPreview, { immediate: true });
</script>

<style scoped>
/* 文档预览容器 - 更精致的卡片 */
.doc-preview {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  border: 1px solid #e4e7ed;
  overflow: hidden;
}

/* 预览头部 - 更现代的设计 */
.preview-header {
  padding: 14px 20px;
  background: linear-gradient(to right, #fff, #f5f7fa);
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.file-icon {
  font-size: 22px;
  color: #409eff;
}

.file-name {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}

/* 预览内容区 */
.preview-content {
  flex: 1;
  overflow: auto;
  padding: 20px;
  background: #fafafa;
}

.image-preview {
  width: 100%;
  height: 100%;
  border-radius: 6px;
  overflow: hidden;
}

/* 文本内容样式 */
.text-content {
  background: #fff;
  padding: 24px;
  border-radius: 8px;
  white-space: pre-wrap;
  word-wrap: break-word;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  line-height: 1.8;
  margin: 0;
  color: #303133;
  font-size: 14px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  border: 1px solid #ebeef5;
}

/* 不支持的格式 */
.unsupported {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #909399;
  gap: 16px;
  background: #fff;
  border-radius: 8px;
}

.unsupported :deep(.el-icon) {
  color: #c0c4cc;
}

.unsupported .tip {
  font-size: 12px;
  color: #b0b0b0;
  margin: -8px 0 0 0;
}
</style>
