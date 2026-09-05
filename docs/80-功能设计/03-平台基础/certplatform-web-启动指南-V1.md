# certplatform-web 启动指南

> **版本**：V1.0 | **日期**：2026-09-05 | **状态**：完成

---

## 一、目录结构

```
src/certplatform-web/
├── package.json                    ← workspace 根
├── yzh.vue.core/                   ← 核心组件库（业务无关）
├── share/                          ← 业务共享层（跨角色复用）
├── admin/                          ← 管理员端（端口 9990，Element Plus）
├── auditor/                        ← 审核员端（端口 9991，Element Plus）
└── enterprise/                     ← 企业端（待建）
```

## 二、启动方式

### 方法1：双击启动脚本（推荐）
```
admin/start.sh
auditor/start.sh
```

### 方法2：命令行启动
```bash
cd src/certplatform-web/admin
export PATH="/opt/homebrew/bin:$PATH"
node node_modules/.bin/vite --host 127.0.0.1 --port 9990
```

```bash
cd src/certplatform-web/auditor
export PATH="/opt/homebrew/bin:$PATH"
node node_modules/.bin/vite --host 127.0.0.1 --port 9991
```

### 方法3：构建生产版本
```bash
cd src/certplatform-web/admin
node node_modules/.bin/vite build
# dist/ 目录即为生产包
```

## 三、访问地址
- 管理员端：http://127.0.0.1:9990/
- 审核员端：http://127.0.0.1:9991/
- 后端 API：http://127.0.0.1:9992/（自动代理）

## 四、注意事项

1. **端口占用**：如果 9990/9991 被占用，修改 vite.config.ts 中的端口配置
2. **依赖共享**：node_modules 通过 symlink 复用 vol.web 的依赖，无需重复安装
3. **后端连接**：开发模式通过 Vite proxy 代理到 9992 端口

## 五、与旧 vol.web 的关系

- 旧前端保留在 `src/server/Vue.NetCore/vol.web/`，不删除
- 新代码全部写入 `src/certplatform-web/`
- 旧代码可作为参考，待新架构稳定后再清理

---

*创建时间：2026-09-05*
