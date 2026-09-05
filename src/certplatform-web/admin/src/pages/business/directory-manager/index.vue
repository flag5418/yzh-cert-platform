<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import { CertDirectoryTree } from '@share/components'

const treeLoading = ref(false)
const selectedFile = ref<any>(null)

async function handleNodeClick(data: any) {
  selectedFile.value = data
  if (data.type === 'file') {
    ElMessage.info(`已选择文件：${data.name}`)
  }
}

onMounted(() => {
  // 目录树将在后续后端就绪后填充数据
})
</script>

<template>
  <YzhPageLayout title="标准目录管理">
    <template #default>
      <el-row :gutter="16">
        <el-col :span="6">
          <el-card shadow="never">
            <template #header>
              <div class="card-header">
                <span>文件目录</span>
              </div>
            </template>
            <div v-loading="treeLoading" style="min-height: 400px">
              <CertDirectoryTree @select="handleNodeClick" />
            </div>
          </el-card>
        </el-col>
        <el-col :span="18">
          <el-card shadow="never">
            <template #header>
              <div class="card-header">
                <span>目录配置</span>
              </div>
            </template>
            <div class="empty-state">
              <el-empty description="请点击左侧目录树选择文件或文件夹" />
            </div>
          </el-card>
        </el-col>
      </el-row>
    </template>
  </YzhPageLayout>
</template>

<style scoped>
.card-header { font-size: 15px; font-weight: 600; }
.empty-state { min-height: 400px; display: flex; align-items: center; justify-content: center; }
</style>
