<script setup lang="ts">
/**
 * 证据管理（专家端 · V2 认证检查闭环 · 路由 `/audit-evidence`）
 *
 * 功能：
 * 1. 证据列表（cert_evidence 已挂接到检查项）
 * 2. 上传/录入证据
 * 3. 证据 MD5 指纹追踪（内容变更 → 影响分析）
 *
 * 后端：/api/Auditor/CertEvidence
 * 契约：PascalCase；业务失败恒 HTTP 200，success 唯一判据。
 */
import { onMounted, ref } from 'vue'
import { ElButton, ElDialog, ElForm, ElFormItem, ElInput, ElMessage, ElTable, ElTableColumn, ElTag } from 'element-plus'
import {
  addEvidence,
  listEvidence,
  type CertEvidenceRow,
} from '@share/api/auditor/cert-check-task'

const loading = ref(false)
const rows = ref<CertEvidenceRow[]>([])
const total = ref(0)
const keyword = ref('')
const checkItemCode = ref('')

const uploadDialog = ref(false)
const uploadForm = ref({
  SourceName: '',
  CheckItemCode: '',
  TranscriptText: '',
})

async function loadList() {
  loading.value = true
  try {
    const res = await listEvidence({
      Keyword: keyword.value || undefined,
      CheckItemCode: checkItemCode.value || undefined,
      PageIndex: 1,
      PageSize: 50,
    })
    if (res.success && res.data) {
      rows.value = res.data.Rows ?? []
      total.value = res.data.Total ?? 0
    }
  } finally {
    loading.value = false
  }
}

function openUpload() {
  uploadForm.value = { SourceName: '', CheckItemCode: '', TranscriptText: '' }
  uploadDialog.value = true
}

async function submitUpload() {
  if (!uploadForm.value.SourceName) {
    ElMessage.error('请填写证据名称')
    return
  }
  const res = await addEvidence({
    SourceName: uploadForm.value.SourceName,
    CheckItemCode: uploadForm.value.CheckItemCode || undefined,
    TranscriptText: uploadForm.value.TranscriptText || undefined,
  })
  if (res.success) {
    ElMessage.success('证据已添加')
    uploadDialog.value = false
    await loadList()
  } else {
    ElMessage.error(res.message || '添加失败')
  }
}

onMounted(loadList)
</script>

<template>
  <div class="evidence-page">
    <div class="evidence-page__header">
      <el-input v-model="keyword" placeholder="证据名称 / 编号" clearable style="width: 260px" @keyup.enter="loadList" />
      <el-button type="primary" @click="loadList">检索</el-button>
      <el-button @click="openUpload">新增证据</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="Code" label="证据编号" min-width="180" show-overflow-tooltip />
      <el-table-column prop="SourceName" label="名称" min-width="160" />
      <el-table-column prop="CheckItemCode" label="关联检查项" width="140" show-overflow-tooltip />
      <el-table-column label="内容指纹" width="120">
        <template #default="{ row }">
          <code class="evidence-page__md5">{{ (row.ContentMD5 || '').slice(0, 8) }}...</code>
        </template>
      </el-table-column>
      <el-table-column label="变更" width="60">
        <template #default="{ row }">
          <el-tag v-if="row.PrevContentMD5 && row.PrevContentMD5 !== row.ContentMD5" type="warning" size="small">已变</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="LlmSummary" label="LLM 摘要" min-width="200" show-overflow-tooltip />
      <el-table-column prop="CreateTime" label="创建时间" width="170" />
    </el-table>

    <!-- 新增证据弹窗 -->
    <el-dialog v-model="uploadDialog" title="新增证据" width="520px">
      <el-form :model="uploadForm" label-width="100px">
        <el-form-item label="证据名称" required>
          <el-input v-model="uploadForm.SourceName" />
        </el-form-item>
        <el-form-item label="检查项编码">
          <el-input v-model="uploadForm.CheckItemCode" placeholder="留空 = 暂不挂接" />
        </el-form-item>
        <el-form-item label="内容文本">
          <el-input v-model="uploadForm.TranscriptText" type="textarea" :rows="6" placeholder="证据文字转录（用于 MD5 指纹 + LLM 摘要）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="uploadDialog = false">取消</el-button>
        <el-button type="primary" @click="submitUpload">确认</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.evidence-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
}
.evidence-page__header {
  display: flex;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-4, 16px);
}
.evidence-page__md5 {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
  font-family: monospace;
}
</style>
