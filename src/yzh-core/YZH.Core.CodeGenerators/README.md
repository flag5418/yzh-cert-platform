# YZH 代码生成器

## 用途
从 C# 实体类自动生成 TypeScript 类型定义，确保前后端字段一致。

## 使用方式

### 1. 构建项目
```bash
cd src/yzh-core
dotnet build YZH.Core.CodeGenerators/YZH.Core.CodeGenerators.csproj
```

### 2. 运行代码生成器
```bash
dotnet run --project src/yzh-core/YZH.Core.CodeGenerators/YZH.Core.CodeGenerators.csproj
```

### 3. 生成的文件
生成的 TypeScript 类型文件会保存到：
```
src/certplatform-web/share/src/types/generated/
├── index.ts
├── Sys_User.ts
├── Sys_Organization.ts
└── ...
```

### 4. 在前端使用生成的类型
```typescript
import type { Sys_UserDb, Sys_OrganizationDb } from '@share/types/generated'

// 使用生成的类型
const user: Sys_UserDb = {
  user_name: 'admin',
  org_code: 'ORG001',
  role_id: 1
}
```

## 工作原理
1. 反射扫描 C# 实体类的属性
2. 自动生成 TypeScript interface
3. 属性名使用 camelCase（与 JSON 响应一致）
4. 类型映射：string/number/boolean/datetime 等

## 注意事项
- 不要手动修改生成的文件
- 修改 C# 实体后，重新运行代码生成器
- 生成的文件会自动覆盖
