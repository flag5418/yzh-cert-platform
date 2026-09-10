<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'

const uploading = ref(false)
const fileList = ref<any[]>([])
const uploadRef = ref()

async function handleUpload(file: any) {
  uploading.value = true
  try {
    // TODO: 待后端文件上传接口就绪后实现
    await new Promise(resolve => setTimeout(resolve, 1000))
    fileList.value.push({ name: file.name, status: 'success', size: (file.size / 1024).toFixed(2) + ' KB' })
    ElMessage.success('上传成功')
  } catch (e: any) {
    ElMessage.error(e?.message || '上传失败')
  } finally {
    uploading.value = false
  }
}

function handleRemove(file: any) {
  fileList.value = fileList.value.filter(f => f.uid !== file.uid)
}

function handleProgress(event: any) {
  // 上传进度
}

onMounted(() => {
  // 初始化
})
</script>

<template>
  <YzhPageLayout title="文件上传">
    <template #default>
      <el-card shadow="never">
        <template #header><span>上传文件</span></template>
        <el-upload
          ref="uploadRef"
          drag
          action="#"
          :auto-upload="false"
          :on-change="handleUpload"
          :on-remove="handleRemove"
          :on-progress="handleProgress"
          :disabled="uploading"
          :file-list="fileList"
          accept=".doc,.docx,.xls,.xlsx,.pdf,.txt"
        >
          <el-icon class="el-icon--upload"><i class="bi bi-upload"></i></el-icon>
          <div class="el-upload__text">拖拽文件到此处或<em>点击上传</em></div>
          <template #tip>
            <div class="el-upload__tip">
              支持 doc/docx/xls/xlsx/pdf/txt 格式，单个文件不超过 50MB
            </div>
          </template>
        </el-upload>
      </el-card>
    </template>
  </YzhPageLayout>
</template>

<style scoped>
.el-icon--upload { font-size: 64px; color: #c0c4cc; }
</style>
