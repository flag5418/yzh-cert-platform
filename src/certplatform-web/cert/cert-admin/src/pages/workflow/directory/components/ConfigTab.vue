<script setup lang="ts">
/**
 * ConfigTab —— 目录配置管理（管理入口：改根名/状态/级联清理）
 *
 * 后端：GET/POST /api/Workflow/StandardDirectory/configs*
 * 表：cert_standard_directory_config（一行 = 一个「机构 × 标准 × 阶段」）
 *
 * ★ 契约（2026-09-27 决策⑳修订 + ㉑）：
 *   - 业务键 = `Code`（GUID，服务端生成）；uk = (OrgCode, StandardCode, StageCode) 三键
 *   - `OrgCode` = certification_body.Code、`StandardCode` = cert_iso_standard.Code、
 *     `StageCode` = cert_cert_stage.Code
 *   - ★ 创建已无感懒建：阶段首次进入/上传时后端 Ensure 自动建行，本界面只做管理与批量查看。
 */
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, FolderOpened } from '@element-plus/icons-vue'
import { yzhApi } from '@yzh-core/api/client'
import {
  getDirectoryConfigs,
  createDirectoryConfig,
  updateDirectoryConfig,
  deleteDirectoryConfig,
} from '@share/composables/useDirectoryApi'
import type { StandardDirectoryConfig, ISOStandard, CertStage, CertificationBody } from '@share/types'

const emit = defineEmits<{ (e: 'changed'): void }>()

/** 预填：由 index.vue 弹窗传入当前选中阶段（机构 / 标准 / 阶段 三键） */
const props = defineProps<{
  presetOrgCode?: string
  presetStandardCode?: string
  presetStageCode?: string
}>()

const loading = ref(false)
const tableData = ref<StandardDirectoryConfig[]>([])
const dialogVisible = ref(false)

const standards = ref<ISOStandard[]>([])
const stages = ref<CertStage[]>([])
const bodies = ref<CertificationBody[]>([])

const form = reactive<{
  code: string
  orgCode: string
  standardCode: string
  stageCode: string
  rootFolderName: string
  status: string
}>({
  code: '',
  orgCode: '',
  standardCode: '',
  stageCode: '',
  rootFolderName: '',
  status: 'draft',
})

/** 兼容 PagedResult（data.Items）等几种返回形态 */
function pickItems(res: any): any[] {
  const d = res?.data ?? res
  return d?.Items || d?.items || d?.Rows || d?.rows || []
}

async function loadOptions() {
  try {
    const [stdRes, stageRes, bodyRes] = await Promise.all([
      yzhApi.post('/api/Foundation/ISOStandard/filter', { Page: 1, PageSize: 1000 }),
      yzhApi.post('/api/Foundation/CertStage/filter', { Page: 1, PageSize: 1000 }),
      yzhApi.post('/api/Foundation/CertificationBody/filter', { Page: 1, PageSize: 1000 }),
    ])
    standards.value = pickItems(stdRes)
    stages.value = pickItems(stageRes)
    bodies.value = pickItems(bodyRes)
  } catch {
    // 下拉仅影响选择体验，失败不阻断列表
  }
}

function orgLabel(code: string): string {
  if (!code) return '--'
  const b = bodies.value.find((x) => x.Code === code)
  return b ? b.Name || code : code
}

function stdLabel(code: string): string {
  if (!code) return '--'
  const s = standards.value.find((x) => x.Code === code)
  return s ? `${s.StandardCode}${s.StandardName ? ' - ' + s.StandardName : ''}` : code
}

function stageLabel(code: string): string {
  if (!code) return '--'
  const s = stages.value.find((x) => x.Code === code)
  return s ? `${s.StageName}（${s.StageCode}）` : code
}

async function loadConfigs() {
  loading.value = true
  try {
    tableData.value = (await getDirectoryConfigs()) || []
  } catch (e: any) {
    ElMessage.error('加载目录配置失败：' + (e?.message || ''))
    tableData.value = []
  } finally {
    loading.value = false
  }
}

function resetForm() {
  form.code = ''
  form.orgCode = ''
  form.standardCode = ''
  form.stageCode = ''
  form.rootFolderName = ''
  form.status = 'draft'
}

function handleAdd() {
  resetForm()
  if (props.presetOrgCode) form.orgCode = props.presetOrgCode
  if (props.presetStandardCode) form.standardCode = props.presetStandardCode
  if (props.presetStageCode) form.stageCode = props.presetStageCode
  dialogVisible.value = true
}

function handleEdit(row: StandardDirectoryConfig) {
  form.code = row.Code || ''
  form.orgCode = row.OrgCode || ''
  form.standardCode = row.StandardCode || ''
  form.stageCode = row.StageCode || ''
  form.rootFolderName = row.RootFolderName || ''
  form.status = row.Status || 'draft'
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!form.orgCode) {
    ElMessage.warning('请选择机构')
    return
  }
  if (!form.standardCode) {
    ElMessage.warning('请选择标准')
    return
  }
  if (!form.stageCode) {
    ElMessage.warning('请选择认证阶段')
    return
  }
  try {
    const payload = {
      OrgCode: form.orgCode,
      StandardCode: form.standardCode,
      StageCode: form.stageCode,
      RootFolderName: form.rootFolderName,
      Status: form.status,
    }
    // 双关键字准则：更新只用路由上的 Code，不回退 Id
    const res: any = form.code
      ? await updateDirectoryConfig(form.code, payload)
      : await createDirectoryConfig(payload)
    if (!res?.success) throw new Error(res?.err || res?.message || '保存失败')
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadConfigs()
    emit('changed')
  } catch (e: any) {
    ElMessage.error('保存失败：' + (e?.message || ''))
  }
}

async function handleDelete(row: StandardDirectoryConfig) {
  const code = row.Code
  try {
    await ElMessageBox.confirm(
      `确定删除该目录配置（${orgLabel(row.OrgCode)} × ${stdLabel(row.StandardCode)} × ${stageLabel(row.StageCode)}）吗？` +
        '该目录下的文件夹、文件与上传队列将一并清理；再次进入该阶段会自动重建空目录。',
      '删除确认',
      { type: 'warning' }
    )
  } catch {
    return
  }
  try {
    const res: any = await deleteDirectoryConfig(code)
    if (!res?.success) throw new Error(res?.err || res?.message || '删除失败')
    ElMessage.success('删除成功')
    loadConfigs()
    emit('changed')
  } catch (e: any) {
    ElMessage.error('删除失败：' + (e?.message || ''))
  }
}

onMounted(async () => {
  await loadOptions()
  await loadConfigs()
})
</script>

<template>
  <div class="config-tab">
    <el-card shadow="never" class="config-card">
      <template #header>
        <div class="card-header">
          <span class="card-title">目录配置管理（机构 × 标准 × 阶段）</span>
          <el-button type="primary" size="small" :icon="Plus" @click="handleAdd">新建配置</el-button>
        </div>
      </template>

      <div class="hint">
        <el-icon><FolderOpened /></el-icon>
        <span>一行 = 一套目录（uk = 机构+标准+阶段，同键不允许存在 2 份）。配置由阶段首次进入/上传时自动创建，此处用于管理根名 / 状态 / 级联清理。</span>
      </div>

      <el-table :data="tableData" v-loading="loading" border stripe size="small">
        <el-table-column prop="Code" label="配置 Code" min-width="240" show-overflow-tooltip />
        <el-table-column label="机构" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ orgLabel(row.OrgCode) }}</template>
        </el-table-column>
        <el-table-column label="标准" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">{{ stdLabel(row.StandardCode) }}</template>
        </el-table-column>
        <el-table-column label="阶段" width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ stageLabel(row.StageCode) }}</template>
        </el-table-column>
        <el-table-column prop="RootFolderName" label="根文件夹名" width="150" show-overflow-tooltip />
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="row.Status === 'active' ? 'success' : 'info'" size="small">
              {{ row.Status === 'active' ? '启用' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="170">
          <template #default="{ row }">
            {{ row.CreateTime ? new Date(row.CreateTime).toLocaleString('zh-CN') : '--' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="handleEdit(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无目录配置" :image-size="80" />
        </template>
      </el-table>
    </el-card>

    <el-dialog
      v-model="dialogVisible"
      :title="form.code ? '编辑目录配置' : '新建目录配置'"
      width="520px"
      destroy-on-close
    >
      <el-form :model="form" label-width="100px">
        <el-form-item label="机构">
          <el-select
            v-model="form.orgCode"
            placeholder="选择认证机构"
            filterable
            clearable
            :disabled="!!form.code"
            style="width: 100%"
          >
            <el-option
              v-for="b in bodies"
              :key="b.Code"
              :value="b.Code || ''"
              :label="b.Name || b.Code || ''"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="标准">
          <el-select
            v-model="form.standardCode"
            placeholder="选择 ISO 标准"
            filterable
            clearable
            :disabled="!!form.code"
            style="width: 100%"
          >
            <el-option
              v-for="s in standards"
              :key="s.Code"
              :value="s.Code || ''"
              :label="`${s.StandardCode}${s.StandardName ? ' - ' + s.StandardName : ''}`"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="认证阶段">
          <el-select
            v-model="form.stageCode"
            placeholder="选择认证阶段"
            filterable
            clearable
            :disabled="!!form.code"
            style="width: 100%"
          >
            <el-option
              v-for="st in stages"
              :key="st.Code"
              :value="st.Code || ''"
              :label="`${st.StageName}（${st.StageCode}）`"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="根文件夹名">
          <el-input v-model="form.rootFolderName" placeholder="如：企业基础资料" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio value="draft">草稿</el-radio>
            <el-radio value="active">启用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="form.code" label="配置 Code">
          <el-input :model-value="form.code" disabled />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.config-tab {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  height: 100%;
  overflow: auto;
}

.config-card {
  border: 1px solid var(--el-border-color-light);
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.card-title {
  font-weight: 600;
}

.hint {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 12px;
  padding: 8px 12px;
  font-size: 12px;
  color: var(--el-color-info);
  background: var(--el-fill-color-light);
  border-radius: 4px;
}
</style>
