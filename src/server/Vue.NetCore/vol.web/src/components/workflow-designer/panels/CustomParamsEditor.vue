<template>
  <div class="cpe">
    <!-- 头部：标题 + 添加按钮 -->
    <div class="cpe-header">
      <span class="cpe-title">参数定义</span>
      <el-button type="primary" size="small" plain @click="addParam">
        <el-icon><IconAdd /></el-icon> 添加参数
      </el-button>
    </div>

    <!-- 空状态 -->
    <div v-if="params.length === 0" class="cpe-empty">
      <p>暂无参数，点击上方「添加参数」创建</p>
      <p class="cpe-empty-sub">每个参数可在提示词中以 <code>{{参数名}}</code> 引用</p>
    </div>

    <!-- 参数列表 -->
    <div v-else class="cpe-list">
      <div v-for="(param, index) in params" :key="index" class="cpe-card">
        <!-- 卡片头部：序号 + 名称 + 类型 + 删除 -->
        <div class="cpe-card-head">
          <span class="cpe-idx">{{ index + 1 }}</span>
          <el-input
            v-model="param.paramName"
            placeholder="参数名（如 enterpriseName）"
            size="small"
            class="cpe-name"
            @change="emitChange"
          />
          <el-select
            v-model="param.paramType"
            size="small"
            class="cpe-type"
            @change="emitChange"
          >
            <el-option label="文本" value="string" />
            <el-option label="数字" value="number" />
            <el-option label="是/否" value="boolean" />
          </el-select>
          <el-button type="danger" size="small" text circle @click="removeParam(index)">
            <el-icon><IconDelete /></el-icon>
          </el-button>
        </div>

        <!-- 来源切换（紧凑单行） -->
        <div class="cpe-source-row">
          <el-radio-group v-model="param.sourceType" size="small" @change="onSourceTypeChange(index)">
            <el-radio-button value="constant">常量</el-radio-button>
            <el-radio-button value="link">节点结果</el-radio-button>
            <el-radio-button value="skill">方法调用</el-radio-button>
          </el-radio-group>
        </div>

        <!-- 来源配置区（上下布局） -->
        <div class="cpe-config">
          <!-- 常量 -->
          <div v-if="param.sourceType === 'constant'" class="cpe-field">
            <label class="cpe-label">常量值</label>
            <el-input
              v-model="param.sourceConfig.value"
              placeholder="输入常量值"
              size="small"
              @change="emitChange"
            />
          </div>

          <!-- 节点结果 -->
          <template v-if="param.sourceType === 'link'">
            <div class="cpe-field">
              <label class="cpe-label">来源节点</label>
              <el-select
                v-model="param.sourceConfig.nodeId"
                placeholder="选择工作流中的节点"
                filterable
                size="small"
                class="cpe-full"
                @change="emitChange"
              >
                <el-option
                  v-for="node in linkableNodes"
                  :key="node.id"
                  :label="node.label"
                  :value="node.id"
                />
              </el-select>
            </div>
            <div v-if="param.sourceConfig.nodeId" class="cpe-field">
              <label class="cpe-label">输出端口</label>
              <el-input
                v-model="param.sourceConfig.portName"
                placeholder="如 result（默认）"
                size="small"
                @change="emitChange"
              />
            </div>
          </template>

          <!-- 方法调用 -->
          <template v-if="param.sourceType === 'skill'">
            <div class="cpe-field">
              <label class="cpe-label">选择方法</label>
              <el-select
                v-model="param.sourceConfig.skillCode"
                placeholder="选择可调用的方法"
                filterable
                size="small"
                class="cpe-full"
                @change="emitChange"
              >
                <el-option
                  v-for="sk in availableSkills"
                  :key="sk.skillCode"
                  :label="`${sk.skillName} (${sk.skillCode})`"
                  :value="sk.skillCode"
                />
              </el-select>
            </div>
            <!-- 方法参数列表 -->
            <div v-if="param.sourceConfig.skillCode" class="cpe-skill-params">
              <div class="cpe-sp-header">
                <span>方法参数</span>
                <el-button type="default" size="small" text @click="addSkillParam(index)">
                  <el-icon><IconAdd /></el-icon> 添加
                </el-button>
              </div>
              <div
                v-for="(sp, spIdx) in param.sourceConfig.skillParams || []"
                :key="spIdx"
                class="cpe-sp-row"
              >
                <el-input
                  v-model="sp.paramName"
                  placeholder="参数名"
                  size="small"
                  class="cpe-sp-name"
                  @change="emitChange"
                />
                <el-radio-group
                  v-model="sp.inputType"
                  size="small"
                  @change="emitChange"
                >
                  <el-radio-button value="constant">常量</el-radio-button>
                  <el-radio-button value="link">节点</el-radio-button>
                </el-radio-group>
                <el-input
                  v-model="sp.value"
                  :placeholder="sp.inputType === 'constant' ? '输入常量' : '节点ID'"
                  size="small"
                  class="cpe-sp-val"
                  @change="emitChange"
                />
                <el-button type="danger" size="small" text circle @click="removeSkillParam(index, spIdx)">
                  <el-icon><IconClose /></el-icon>
                </el-button>
              </div>
            </div>
          </template>
        </div>

        <!-- 底部预览条 -->
        <div class="cpe-preview">
          <code :text="`{{${param.paramName || '?'}}}`"></code>
          <span class="cpe-arrow">→</span>
          <span v-if="param.sourceType === 'constant' && param.sourceConfig.value" class="cpe-val" v-text="param.sourceConfig.value"></span>
          <span v-else-if="param.sourceType === 'link' && param.sourceConfig.nodeId" class="cpe-val" v-text="`${getNodeName(param.sourceConfig.nodeId)}.${param.sourceConfig.portName || 'result'}`"></span>
          <span v-else-if="param.sourceType === 'skill' && param.sourceConfig.skillCode" class="cpe-val" v-text="`${param.sourceConfig.skillCode}()`"></span>
          <span v-else class="cpe-val cpe-unset">未配置</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { IconAdd, IconDelete, IconClose } from '@/yzh/icons'

const props = defineProps({
  modelValue: { type: Array, default: () => [] },
  linkableNodes: { type: Array, default: () => [] },
  availableSkills: { type: Array, default: () => [] }
})

const emit = defineEmits(['update:modelValue'])

const params = ref([])

watch(() => props.modelValue, (val) => {
  params.value = JSON.parse(JSON.stringify(val || []))
}, { immediate: true, deep: true })

function addParam() {
  params.value.push({
    paramName: '',
    paramType: 'string',
    sourceType: 'constant',
    sourceConfig: { value: '' }
  })
  emitChange()
}

function removeParam(index) {
  params.value.splice(index, 1)
  emitChange()
}

function onSourceTypeChange(index) {
  const st = params.value[index].sourceType
  if (st === 'constant') {
    params.value[index].sourceConfig = { value: '' }
  } else if (st === 'link') {
    params.value[index].sourceConfig = { nodeId: '', portName: 'result' }
  } else if (st === 'skill') {
    params.value[index].sourceConfig = { skillCode: '', skillParams: [] }
  }
  emitChange()
}

function addSkillParam(pIdx) {
  if (!params.value[pIdx].sourceConfig.skillParams) {
    params.value[pIdx].sourceConfig.skillParams = []
  }
  params.value[pIdx].sourceConfig.skillParams.push({
    paramName: '',
    inputType: 'constant',
    value: ''
  })
  emitChange()
}

function removeSkillParam(pIdx, spIdx) {
  params.value[pIdx].sourceConfig.skillParams.splice(spIdx, 1)
  emitChange()
}

function getNodeName(nodeId) {
  const node = props.linkableNodes.find(n => n.id === nodeId)
  return node?.label || nodeId
}

function emitChange() {
  emit('update:modelValue', params.value)
}
</script>

<style scoped lang="less">
.cpe {
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  background: #fafbfc;
}

// ── 头部 ──
.cpe-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid #ebeef5;
}
.cpe-title {
  font-size: 13px;
  font-weight: 600;
  color: #303133;
}

// ── 空态 ──
.cpe-empty {
  padding: 20px 16px;
  text-align: center;
  p {
    margin: 4px 0;
    font-size: 13px;
    color: #909399;
  }
  code {
    background: #f0f0f0;
    padding: 1px 5px;
    border-radius: 3px;
    font-size: 12px;
  }
  .cpe-empty-sub {
    font-size: 11px;
    color: #c0c4cc;
  }
}

// ── 列表 ──
.cpe-list {
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

// ── 单个参数卡片 ──
.cpe-card {
  background: #fff;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  overflow: hidden;

  &:hover {
    border-color: #b37feb;
    box-shadow: 0 1px 6px rgba(156, 39, 176, 0.08);
  }
}

// 卡片头部：一行
.cpe-card-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  background: #f8f9fa;
  border-bottom: 1px solid #f0f0f0;
}
.cpe-idx {
  width: 20px;
  height: 20px;
  line-height: 20px;
  text-align: center;
  border-radius: 50%;
  background: #9C27B0;
  color: #fff;
  font-size: 11px;
  font-weight: 600;
  flex-shrink: 0;
}
.cpe-name {
  flex: 1;
  min-width: 0;
}
.cpe-type {
  width: 90px;
  flex-shrink: 0;
}

// 来源切换行
.cpe-source-row {
  padding: 6px 10px 0;
}

// 配置区（上下布局）
.cpe-config {
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 7px;
}
.cpe-field {
  display: flex;
  align-items: center;
  gap: 8px;
}
.cpe-label {
  font-size: 12px;
  color: #606266;
  width: 56px;
  flex-shrink: 0;
  white-space: nowrap;
}
.cpe-full {
  flex: 1;
}

// Skill 方法参数
.cpe-skill-params {
  margin-top: 2px;
  padding-top: 6px;
  border-top: 1px dashed #dcdfe6;
}
.cpe-sp-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  font-size: 12px;
  color: #606266;
}
.cpe-sp-row {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-bottom: 5px;
  padding: 4px 6px;
  background: #f5f7fa;
  border-radius: 4px;
}
.cpe-sp-name {
  width: 85px;
  flex-shrink: 0;
}
.cpe-sp-val {
  flex: 1;
  min-width: 0;
}

// 底部预览
.cpe-preview {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 10px;
  background: linear-gradient(135deg, #fdf2fb 0%, #f3e5f5 100%);
  border-top: 1px solid #e1bee7;
  font-size: 11px;
  code {
    background: rgba(156, 39, 176, 0.08);
    color: #9C27B0;
    padding: 1px 5px;
    border-radius: 3px;
    font-weight: 600;
    font-family: inherit;
  }
  .cpe-arrow {
    color: #c0c4cc;
  }
  .cpe-val {
    color: #606266;
    word-break: break-all;
  }
  .cpe-unset {
    color: #c0c4cc;
    font-style: italic;
  }
}
</style>
