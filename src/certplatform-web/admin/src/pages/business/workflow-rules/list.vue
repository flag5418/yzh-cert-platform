<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { YzhPageLayout } from '@yzh-core/components/layout'

const loading = ref(false)
const ruleList = ref<any[]>([])

async function loadData() {
  loading.value = true
  try {
    // TODO: 待后端工作流规则 API 就绪后实现
    ruleList.value = []
  } catch (e: any) {
    // 静默处理
  } finally {
    loading.value = false
  }
}

function openEdit(row: any) {
  // TODO: 打开编辑弹窗
}

function doDelete(row: any) {
  // TODO: 删除规则
}

onMounted(loadData)
</script>

<template>
  <YzhPageLayout title="工作流规则">
    <template #toolbar>
      <el-button type="primary" @click="openEdit(null)">
        <el-icon><Plus /></el-icon> 新建规则
      </el-button>
    </template>

    <el-card shadow="never">
      <el-table :data="ruleList" stripe border v-loading="loading">
        <el-table-column prop="ruleCode" label="规则编码" width="180" />
        <el-table-column prop="ruleName" label="规则名称" width="200" />
        <el-table-column prop="ruleType" label="规则类型" width="120" />
        <el-table-column prop="description" label="说明" min-width="200" show-overflow-tooltip />
        <el-table-column prop="isActive" label="启用" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '是' : '否' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="doDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </YzhPageLayout>
</template>

<style scoped>
</style>
