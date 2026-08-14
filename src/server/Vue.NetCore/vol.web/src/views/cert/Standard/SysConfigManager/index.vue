<template>
  <div class="sys-config-page">
    <CertPageHeader title="系统参数配置" :icon="IconSetting" />

    <el-tabs v-model="activeCategory" @tab-change="loadConfigs">
      <el-tab-pane
        v-for="cat in categories"
        :key="cat.key"
        :label="cat.label"
        :name="cat.key"
      />
    </el-tabs>

    <el-table :data="configList" border style="width: 100%" v-loading="loading">
      <el-table-column prop="displayName" label="参数名称" width="200" />
      <el-table-column prop="configKey" label="参数键" width="250" />
      <el-table-column label="参数值" width="300">
        <template #default="{ row }">
          <el-input
            v-if="row.isReadonly === 0 && row.configType !== 'bool'"
            v-model="row.configValue"
            size="small"
            @blur="saveConfig(row)"
          />
          <el-switch
            v-else-if="row.isReadonly === 0 && row.configType === 'bool'"
            v-model="row.configValue"
            active-value="true"
            inactive-value="false"
            @change="saveConfig(row)"
          />
          <span v-else class="readonly-value">{{ row.configValue }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="configType" label="类型" width="80" />
      <el-table-column prop="description" label="说明" />
      <el-table-column label="状态" width="80">
        <template #default="{ row }">
          <el-tag v-if="row.isReadonly === 1" type="info" size="small">只读</el-tag>
          <el-tag v-else type="success" size="small">可编辑</el-tag>
        </template>
      </el-table-column>
    </el-table>

    <div class="config-footer">
      <el-button type="primary" @click="loadConfigs">刷新</el-button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, getCurrentInstance } from 'vue'
import { ElMessage } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconSetting } from '@/yzh'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const activeCategory = ref('convert_queue')
const configList = ref([])

const categories = [
  { key: 'convert_queue', label: '文件转换' },
  { key: 'ai_model', label: 'AI 模型' },
  { key: 'ocr', label: 'OCR 配置' },
  { key: 'storage', label: '存储配置' },
  { key: 'aliyun', label: '阿里云' },
  { key: 'system', label: '系统级' }
]

const loadConfigs = async () => {
  loading.value = true
  try {
    const res = await proxy.http.post('api/sys-config/list', {
      category: activeCategory.value
    }, true)
    if (res.status) {
      configList.value = res.data || []
    }
  } catch (e) {
    ElMessage.error('加载配置失败')
  } finally {
    loading.value = false
  }
}

const saveConfig = async (row) => {
  try {
    await proxy.http.post('api/sys-config/update', {
      configKey: row.configKey,
      configValue: row.configValue
    }, true)
    ElMessage.success(`${row.displayName} 已更新`)
  } catch (e) {
    ElMessage.error('更新失败')
    loadConfigs()
  }
}

onMounted(() => {
  loadConfigs()
})
</script>

<style scoped lang="less">
.sys-config-page {
  padding: var(--yzh-space-5, 20px);

  .readonly-value {
    color: var(--yzh-color-text-secondary, #909399);
  }

  .config-footer {
    margin-top: var(--yzh-space-4, 16px);
    text-align: right;
  }
}
</style>
