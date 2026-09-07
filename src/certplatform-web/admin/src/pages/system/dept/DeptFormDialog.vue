<script setup lang="ts">
/**
 * DeptFormDialog - 部门新增/编辑弹窗
 *
 * Props:
 * - modelValue: 弹窗可见性
 * - mode: 'add' | 'edit'
 * - formData: 表单数据对象（双向绑定）
 * - parent-node: 父节点（用于显示父节点名称）
 *
 * Events:
 * - update:modelValue: 更新可见性
 * - submit: 提交表单
 */
import { computed, watch } from 'vue'
import { ElDialog, ElForm, ElFormItem, ElInput, ElSelect, ElOption, ElSwitch, ElTag } from 'element-plus'
import type { TreeNode } from '@share/types/tree'
import type { SysDept } from '@share/api/system-dept'

// 表单数据接口
interface FormData {
  departmentId?: string
  departmentName: string
  departmentCode: string
  departmentType: string
  enable: number
  remark: string
}

const props = defineProps<{
  modelValue: boolean
  mode: 'add' | 'edit'
  formData: FormData
  parentNode: TreeNode<SysDept> | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', val: boolean): void
  (e: 'submit'): void
}>()

// 弹窗可见性
const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

// 弹窗标题
const title = computed(() => (props.mode === 'add' ? '新增部门' : '编辑部门'))

// 父节点显示文本
const parentDisplay = computed(() => {
  if (props.mode === 'add') {
    return props.parentNode?.name ?? '根级'
  }
  return props.parentNode?.name ?? '-'
})

// 提交
function handleSubmit() {
  if (!props.formData.departmentName?.trim()) {
    return
  }
  emit('submit')
}

// 关闭时清理
watch(visible, (val) => {
  if (!val && props.mode === 'add') {
    // 新增模式关闭时重置表单（由 logic 层处理）
  }
})
</script>

<template>
  <ElDialog v-model="visible" :title="title" width="520px" align-center>
    <ElForm :model="formData" label-width="100px" @submit.prevent="handleSubmit">
      <!-- 编辑模式下显示部门ID（只读） -->
      <ElFormItem v-if="mode === 'edit'" label="部门ID">
        <ElInput :model-value="formData.departmentId" disabled />
      </ElFormItem>

      <!-- 父节点 -->
      <ElFormItem label="上级部门">
        <ElTag type="info">{{ parentDisplay }}</ElTag>
      </ElFormItem>

      <!-- 部门名称 -->
      <ElFormItem label="部门名称" required>
        <ElInput
          v-model="formData.departmentName"
          placeholder="请输入部门名称"
          maxlength="200"
          show-word-limit
        />
      </ElFormItem>

      <!-- 部门编码 -->
      <ElFormItem label="部门编码">
        <ElInput
          v-model="formData.departmentCode"
          placeholder="请输入部门编码"
          maxlength="50"
        />
      </ElFormItem>

      <!-- 部门类型 -->
      <ElFormItem label="部门类型">
        <ElSelect v-model="formData.departmentType" placeholder="请选择部门类型" clearable style="width: 100%">
          <ElOption label="公司" value="company" />
          <ElOption label="部门" value="department" />
          <ElOption label="小组" value="team" />
        </ElSelect>
      </ElFormItem>

      <!-- 状态 -->
      <ElFormItem label="状态">
        <ElSwitch
          v-model="formData.enable"
          :active-value="1"
          :inactive-value="0"
          active-text="启用"
          inactive-text="禁用"
        />
      </ElFormItem>

      <!-- 备注 -->
      <ElFormItem label="备注">
        <ElInput
          v-model="formData.remark"
          type="textarea"
          placeholder="请输入备注"
          :rows="3"
          maxlength="500"
          show-word-limit
        />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton type="primary" @click="handleSubmit">确定</ElButton>
    </template>
  </ElDialog>
</template>
