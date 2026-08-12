# Vue 3 + TypeScript 前端编码规范

> **版本**: V1.0 | **日期**: 2026-08-11 | **状态**: 草案

---

## 一、文件组织规范

### 1.1 目录结构

```
src/views/cert/
├── ModuleName/                    # 业务模块
│   ├── index.vue                  # 主页面（<500行）
│   ├── api.ts                     # API封装（TypeScript）
│   ├── types.ts                   # 类型定义
│   ├── composables/               # 组合式函数
│   │   ├── useApi.ts
│   │   ├── useForm.ts
│   │   └── usePermission.ts
│   └── components/                # 组件目录
│       ├── Table.vue
│       ├── Dialog.vue
│       └── ...
```

### 1.2 文件大小限制

| 文件类型 | 最大行数 | 处理方式 |
|----------|----------|----------|
| .vue 页面 | 500行 | 拆分组件或提取composable |
| .vue 组件 | 300行 | 拆分子组件 |
| .ts 文件 | 200行 | 拆分模块 |
| api.ts | 不限 | 按业务模块拆分 |

---

## 二、TypeScript规范

### 2.1 禁止使用 any

```typescript
// ❌ 错误
const data: any = await api.getData()

// ✅ 正确
interface IData {
  id: number
  name: string
}
const data: IData[] = await api.getData()
```

### 2.2 接口定义规范

```typescript
// ✅ 放在 types.ts 或文件顶部
export interface AIAnalyzeRequest {
  /** 文件编码 */
  fileCode: string
  /** 技能类型：word/excel/pdf */
  skill: 'word' | 'excel' | 'pdf'
}

export interface AIAnalyzeResponse {
  success: boolean
  data?: {
    fields: FieldDef[]
    tables: TableDef[]
    message: string
  }
  message?: string
}

export interface FieldDef {
  fieldCode: string
  fieldName: string
  fieldType: 'string' | 'number' | 'date'
  description?: string
}
```

### 2.3 Props定义规范

```typescript
// ✅ 使用defineProps泛型
interface Props {
  fields: FieldDef[]
  tables: TableDef[]
  loading?: boolean
}

const props = defineProps<Props>()
```

---

## 三、组件设计规范

### 3.1 单一职责

```vue
<!-- ❌ 错误：一个大组件承担所有职责 -->
<script setup>
// 1000行代码：API调用、表单处理、表格渲染、对话框...
</script>

<!-- ✅ 正确：拆分为多个小组件 -->
<template>
  <div class="page">
    <FileTree @select="onFileSelect" />
    <DocPreview :file="currentFile" />
    <AIAnalysisTab 
      :fields="fields" 
      @analyze="onAnalyze" 
    />
  </div>
</template>
```

### 3.2 Composable提取

```typescript
// composables/useAIAnalysis.ts
export function useAIAnalysis() {
  const fields = ref<FieldDef[]>([])
  const tables = ref<TableDef[]>([])
  const loading = ref(false)
  
  const analyze = async (fileCode: string, skill: string) => {
    loading.value = true
    try {
      const res = await aiAnalyzeDocument({ fileCode, skill })
      fields.value = res.data?.fields ?? []
      tables.value = res.data?.tables ?? []
    } finally {
      loading.value = false
    }
  }
  
  return { fields, tables, loading, analyze }
}
```

### 3.3 组件注释

```vue
<!-- AI分析标签页 -->
<!-- 
  【职责】展示AI分析结果，支持编辑和手动添加
  【使用方】DocExtractionRule/index.vue
  【事件】analyze, update:fields, update:tables
-->
<script setup lang="ts">
interface Props {
  fields: FieldDef[]
  tables: TableDef[]
}
const props = defineProps<Props>()
</script>
```

---

## 四、API调用规范

### 4.1 统一API封装

```typescript
// api.ts
import http from '@/api/http'

export const aiAnalyzeDocument = (data: AIAnalyzeRequest) => {
  return http.post('/api/DocExtractionRule/analyze', data)
}

export const getPromptTemplates = (params?: { promptType?: string }) => {
  return http.get('/api/prompt-template', params)
}
```

### 4.2 错误处理

```typescript
// ❌ 错误：每个API单独处理错误
const res = await api.getData()
if (!res.success) {
  ElMessage.error(res.message)
}

// ✅ 正确：统一拦截器处理（http.js已配置）
const res = await api.getData()
// 成功直接返回，错误由拦截器统一处理
```

---

## 五、样式规范

### 5.1 使用CSS变量

```css
/* styles/variables.css 或 <style> 顶部 */
:root {
  --yzh-primary: #409eff;
  --yzh-success: #67c23a;
  --yzh-warning: #e6a23c;
  --yzh-danger: #f56c6c;
  --yzh-info: #909399;
  
  --yzh-padding-section: 16px;
  --yzh-border-radius: 8px;
  --yzh-font-size-base: 14px;
}
```

### 5.2 样式组织

```vue
<style scoped>
/* 布局 */
.page-header { ... }
.main-container { ... }

/* 组件 */
.file-tree { ... }
.doc-preview { ... }

/* 状态 */
.status-bar { ... }
.empty-state { ... }
</style>
```

---

## 六、代码审查检查清单

- [ ] 是否使用TypeScript？（禁止any）
- [ ] 组件是否<500行？
- [ ] Props是否有类型定义？
- [ ] 是否提取了composable？
- [ ] API是否有统一封装？
- [ ] 样式是否使用CSS变量？
- [ ] 组件是否有注释说明职责？
