<template>
  <el-dialog
    v-model="visible"
    :title="title"
    width="500px"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="100px"
    >
      <el-form-item label="菜单名称" prop="menuName">
        <el-input v-model="form.menuName" placeholder="请输入菜单名称" />
      </el-form-item>
      <el-form-item label="路由路径" prop="url">
        <el-input v-model="form.url" placeholder="/system/menu" />
      </el-form-item>
      <el-form-item label="图标" prop="icon">
        <IconPicker v-model="form.icon" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="2"
          placeholder="请输入菜单描述"
        />
      </el-form-item>
      <el-form-item label="排序号">
        <el-input-number v-model="form.orderNo" :min="0" :max="9999" />
      </el-form-item>
      <el-form-item label="是否启用">
        <el-switch
          v-model="form.enable"
          :active-value="1"
          :inactive-value="0"
        />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import IconPicker from '@/components/IconPicker.vue'
import type { SysMenu } from '@/api/system/menu'

const props = defineProps<{
  modelValue: boolean
  title: string
  data: Partial<SysMenu>
  isEdit: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: Partial<SysMenu>): void
}>()

const formRef = ref()
const submitting = ref(false)

const form = ref<Partial<SysMenu>>({})

const rules = {
  menuName: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
  url: [{ required: true, message: '请输入路由路径', trigger: 'blur' }]
}

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

watch(() => props.data, (val) => {
  form.value = { ...val }
}, { immediate: true })

function handleClose() {
  formRef.value?.resetFields()
  emit('update:modelValue', false)
}

async function handleSubmit() {
  try {
    await formRef.value.validate()
    submitting.value = true
    emit('submit', form.value)
  } catch (e) {
    // validation failed
  } finally {
    submitting.value = false
  }
}
</script>
