<template>
  <div class="workflow-rules-page">
    <CertPageHeader title="NC检查项配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- 左侧树形导航：使用公共组件 -->
      <el-card shadow="never" class="tree-card">
        <template #header>
          <div class="tree-header">
            <span>机构 / 标准 / 阶段</span>
            <el-button link size="small" @click="refreshTree">
              <el-icon><IconRefresh /></el-icon>
            </el-button>
          </div>
        </template>
        <YzhStdTree
          ref="stdTreeRef"
          badge-field="ruleCount"
          @select="handleTreeSelect"
          @loaded="onTreeLoaded"
        />
      </el-card>

      <!-- 右侧内容区 -->
      <div class="content-area">
        <!-- 筛选栏 -->
        <el-card shadow="never" class="filter-card">
          <el-form :inline="true" class="filter-form">
            <el-form-item label="当前节点">
              <el-tag type="primary">{{ currentLabel || '全部检查项' }}</el-tag>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="loadData">查询</el-button>
              <el-button @click="resetFilter">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 列表 -->
        <el-card shadow="never" class="table-card">
          <template #header>
            <div class="card-header">
              <span class="card-title">NC检查项列表</span>
              <div class="card-actions">
                <el-button
                  type="primary"
                  size="small"
                  @click="openEdit(null)"
                  :disabled="!currentFilter.standardCode"
                >
                  <el-icon><IconAdd /></el-icon> 新建检查项
                </el-button>
              </div>
            </div>
          </template>

          <el-table
            :data="tableData"
            stripe
            border
            v-loading="loading"
            style="width: 100%"
            class="yzh-commercial-table"
          >
            <el-table-column prop="ruleName" label="中文名称" min-width="180">
              <template #default="{ row }">
                <div class="cell-main-text">{{ row.ruleName }}</div>
              </template>
            </el-table-column>
            <el-table-column
              prop="ruleNameEn"
              label="英文名称"
              min-width="150"
              show-overflow-tooltip
            >
              <template #default="{ row }">
                <div class="cell-sub-text">{{ row.ruleNameEn || '-' }}</div>
              </template>
            </el-table-column>
            <el-table-column label="关联条款" min-width="220">
              <template #default="{ row }">
                <div class="clause-cell" v-if="row.clauseNumber">
                  <el-tag size="small" effect="plain" class="clause-tag">{{
                    row.clauseNumber
                  }}</el-tag>
                  <span class="clause-title">{{ row.clauseTitle }}</span>
                </div>
                <span v-else class="empty-text">-</span>
              </template>
            </el-table-column>
            <el-table-column label="启用状态" width="120" align="center">
              <template #default="{ row }">
                <div class="status-wrapper">
                  <el-switch
                    v-model="row.isActive"
                    :active-value="true"
                    :inactive-value="false"
                    inline-prompt
                    active-text="启用"
                    inactive-text="禁用"
                    @change="(val) => handleStatusChange(row, val)"
                  />
                </div>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="160" fixed="right" align="center">
              <template #default="{ row }">
                <div class="row-actions">
                  <el-button type="primary" link @click="openEdit(row)">
                    <el-icon><IconEdit /></el-icon>编辑
                  </el-button>
                  <el-divider direction="vertical" />
                  <el-button type="danger" link @click="handleDelete(row)">
                    <el-icon><IconDelete /></el-icon>删除
                  </el-button>
                </div>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="total"
            layout="total, prev, pager, next"
            style="margin-top: 16px; justify-content: flex-end"
            @current-change="loadData"
          />
        </el-card>
      </div>
    </div>

    <!-- 编辑弹窗（极简 5 字段） -->
    <el-dialog
      v-model="dialogVisible"
      :title="editForm.id ? '编辑检查项' : '新建检查项'"
      width="500px"
      destroy-on-close
    >
      <el-form :model="editForm" label-width="100px" ref="formRef">
        <el-form-item
          label="中文名称"
          prop="ruleName"
          :rules="[{ required: true, message: '请输入中文名称' }]"
        >
          <el-input v-model="editForm.ruleName" placeholder="如：资源提供检查" />
        </el-form-item>
        <el-form-item label="英文名称">
          <el-input v-model="editForm.ruleNameEn" placeholder="English name" />
        </el-form-item>
        <el-form-item
          label="关联条款"
          prop="clauseCode"
          :rules="[{ required: true, message: '请选择关联条款' }]"
        >
          <el-tree-select
            v-model="editForm.clauseCode"
            :data="clauseTreeData"
            :props="{ label: 'label', value: 'code', children: 'children' }"
            filterable
            check-strictly
            placeholder="选择ISO条款"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="editForm.isActive" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="editForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { CertPageHeader } from '@/certcore'
import { YzhStdTree } from '@/yzh'
import { IconAdd, IconDelete, IconEdit, IconRefresh, IconSetting } from '@/yzh/icons'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getCurrentInstance, onMounted, reactive, ref } from 'vue'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const tableData = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const dialogVisible = ref(false)
const formRef = ref(null)
const stdTreeRef = ref(null)
const currentLabel = ref('全部检查项')
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })
const clauseTreeData = ref([])
const editForm = reactive({
  id: null,
  code: '',
  orgCode: '',
  standardCode: '',
  phaseCode: '',
  clauseCode: '',
  workflowCode: '',
  ruleCode: '',
  ruleName: '',
  ruleNameEn: '',
  severityIfViolated: '',
  ncDescriptionTemplate: '',
  ruleJson: '',
  remark: '',
  isActive: true
})

// ── 公共树组件事件 ──

function handleTreeSelect({
  phase,
  standard,
  org,
  orgCode,
  stdCode,
  standardCode,
  phaseCode,
  phaseName
}) {
  Object.assign(currentFilter, { orgCode, standardCode: stdCode || standardCode, phaseCode })
  currentLabel.value = `${standard?.label || ''} / ${phase.label}`
  page.value = 1
  loadData()
  if (stdCode || standardCode) loadClauseTree(stdCode || standardCode)
}

function onTreeLoaded(treeData) {
  // 树加载完成，可以在这里做额外处理
}

const refreshTree = () => {
  stdTreeRef.value?.reload()
  loadData()
}

// ── 条款树 ──

async function loadClauseTree(stdCode) {
  const code = stdCode || currentFilter.standardCode
  if (!code) {
    clauseTreeData.value = []
    return
  }
  try {
    const res = await proxy.http.get(`api/iso-clause/tree?standardCode=${code}`, null, false)
    if (res?.status) {
      // 转换数据，确保有 label 字段供 el-tree-select 使用
      const transform = (list) => {
        return (list || []).map((item) => ({
          ...item,
          label: `${item.clauseNumber} ${item.title}`,
          children: transform(item.children)
        }))
      }
      clauseTreeData.value = transform(res.data || [])
    }
  } catch (e) {
    ElMessage.error('加载条款树失败')
  }
}

// ── 数据操作 ──

async function loadData() {
  loading.value = true
  try {
    const res = await proxy.http.post(
      'api/validation-rule/page',
      {
        Page: page.value,
        Rows: pageSize.value,
        Sort: 'Id',
        Order: 'desc'
      },
      true,
      { params: { ...currentFilter } }
    )
    if (res?.status) {
      tableData.value = res.data?.rows || []
      total.value = res.data?.total || 0
    }
  } catch (e) {
    ElMessage.error('操作失败')
  } finally {
    loading.value = false
  }
}

const resetFilter = () => {
  Object.assign(currentFilter, { orgCode: '', standardCode: '', phaseCode: '' })
  currentLabel.value = '全部检查项'
  stdTreeRef.value?.clearSelection()
  page.value = 1
  loadData()
}

const openEdit = (row) => {
  if (row) {
    Object.assign(editForm, {
      id: row.id,
      code: row.code || '',
      orgCode: row.orgCode || currentFilter.orgCode,
      standardCode: row.standardCode || currentFilter.standardCode,
      phaseCode: row.phaseCode || currentFilter.phaseCode,
      clauseCode: row.clauseCode || '',
      workflowCode: row.workflowCode || '',
      ruleCode: row.ruleCode || '',
      ruleName: row.ruleName || '',
      ruleNameEn: row.ruleNameEn || '',
      severityIfViolated: row.severityIfViolated || '',
      ncDescriptionTemplate: row.ncDescriptionTemplate || '',
      ruleJson: row.ruleJson || '',
      remark: row.remark || '',
      isActive: row.isActive !== false
    })
  } else {
    Object.assign(editForm, {
      id: null,
      code: '',
      ruleCode: '',
      orgCode: currentFilter.orgCode,
      standardCode: currentFilter.standardCode,
      phaseCode: currentFilter.phaseCode,
      clauseCode: '',
      workflowCode: '',
      ruleName: '',
      ruleNameEn: '',
      severityIfViolated: '',
      ncDescriptionTemplate: '',
      ruleJson: '',
      remark: '',
      isActive: true
    })
  }
  // 始终根据当前编辑的 standardCode 加载条款树
  const stdCode = editForm.standardCode || currentFilter.standardCode
  if (stdCode) {
    loadClauseTree(stdCode)
  }
  dialogVisible.value = true
}

const handleSave = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    try {
      const res = await proxy.http.post('api/validation-rule', editForm, true)
      if (res?.status) {
        ElMessage.success('保存成功')
        dialogVisible.value = false
        loadData()
      } else ElMessage.error(res?.message || '保存失败')
    } catch (e) {
      ElMessage.error('保存失败')
    }
  })
}

const handleStatusChange = async (row, val) => {
  try {
    const res = await proxy.http.post('api/validation-rule', { ...row, isActive: val }, true)
    if (res?.status) ElMessage.success(`检查项「${row.ruleName}」已${val ? '启用' : '禁用'}`)
    else {
      row.isActive = !val // 回滚
      ElMessage.error(res?.message || '状态更新失败')
    }
  } catch (e) {
    row.isActive = !val // 回滚
    ElMessage.error('状态更新失败')
  }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除检查项「${row.ruleName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/validation-rule/delete/${row.id}`, null, true)
    if (res?.status) {
      ElMessage.success('删除成功')
      loadData()
    }
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('删除失败')
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="less">
.workflow-rules-page {
  padding: 32px;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #f8fafc;
  overflow: hidden;
  box-sizing: border-box;
}

.page-body {
  display: flex;
  gap: 24px;
  flex: 1;
  min-height: 0;
}

.tree-card {
  width: 320px;
  min-width: 320px;
  border-radius: 24px;
  border: 1px solid #e2e8f0;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.03);
  display: flex;
  flex-direction: column;
  background: #fff;

  :deep(.el-card__header) {
    padding: 24px;
    border-bottom: 1px solid #f1f5f9;
  }
  :deep(.el-card__body) {
    flex: 1;
    overflow-y: auto;
    padding: 12px;
  }
}

.tree-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  span {
    font-size: 16px;
    font-weight: 800;
    color: #0f172a;
  }
}

.content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  gap: 24px;
}

.filter-card {
  border-radius: 24px;
  border: 1px solid #e2e8f0;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.03);

  :deep(.el-card__body) {
    padding: 20px 32px;
  }

  .filter-form {
    display: flex;
    align-items: center;
    justify-content: space-between;

    :deep(.el-form-item) {
      margin-bottom: 0;
      margin-right: 0;
    }

    :deep(.el-form-item__label) {
      font-weight: 700;
      color: #64748b;
      font-size: 15px;
    }

    :deep(.el-tag) {
      height: 36px;
      padding: 0 20px;
      border-radius: 18px;
      font-weight: 700;
      font-size: 15px;
    }

    .el-button {
      height: 44px;
      padding: 0 24px;
      border-radius: 22px;
      font-weight: 600;
      &.el-button--primary {
        box-shadow: 0 4px 12px rgba(47, 84, 235, 0.2);
      }
    }
  }
}

.table-card {
  flex: 1;
  border-radius: 32px;
  border: 1px solid #e2e8f0;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.04);
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;

  :deep(.el-card__header) {
    padding: 24px 32px;
    border-bottom: 1px solid #f1f5f9;
  }

  :deep(.el-card__body) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    padding: 24px;
  }
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.card-title {
  font-size: 20px;
  font-weight: 900;
  color: #0f172a;
  display: flex;
  align-items: center;

  &::before {
    content: '';
    width: 4px;
    height: 20px;
    background: var(--yzh-color-primary);
    border-radius: 2px;
    margin-right: 12px;
  }
}

.card-actions {
  .el-button {
    height: 48px;
    padding: 0 24px;
    border-radius: 24px;
    font-weight: 600;
    font-size: 15px;
    box-shadow: 0 4px 12px rgba(47, 84, 235, 0.2);
  }
}

/* 表格商业化样式适配 */
.yzh-commercial-table {
  --el-table-header-bg-color: #f8fafc;
  --el-table-header-text-color: #0f172a;
  --el-table-row-hover-bg-color: #f1f5ff;

  :deep(.el-table__header) {
    th {
      height: 72px;
      font-size: 17px;
      font-weight: 900;
      border-bottom: 2px solid #e2e8f0;
    }
  }

  :deep(.el-table__row) {
    td {
      padding: 16px 0;
      font-size: 16px;
    }
  }

  .cell-main-text {
    font-weight: 700;
    color: #1e293b;
    line-height: 1.5;
  }

  .cell-sub-text {
    color: #64748b;
    font-size: 14px;
  }

  .clause-cell {
    display: flex;
    align-items: center;
    gap: 8px;

    .clause-tag {
      flex-shrink: 0;
      font-weight: 700;
      background: #f1f5f9;
      border: none;
      color: #475569;
    }

    .clause-title {
      font-size: 14px;
      color: #64748b;
      line-height: 1.4;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  }

  .row-actions {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;

    .el-button {
      font-size: 15px;
      font-weight: 600;
      padding: 0 4px;

      .el-icon {
        margin-right: 4px;
        font-size: 16px;
      }
    }
  }
}

:deep(.el-pagination) {
  padding: 24px 0 0;
  .el-pagination__total {
    font-size: 14px;
    font-weight: 600;
    color: #64748b;
  }
  .el-pager li {
    width: 36px;
    height: 36px;
    line-height: 36px;
    border-radius: 8px;
    font-weight: 700;
    margin: 0 4px;
    &.is-active {
      background: var(--yzh-color-primary);
      color: #fff;
      box-shadow: 0 4px 12px rgba(47, 84, 235, 0.2);
    }
  }
}
</style>
