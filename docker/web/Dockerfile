# syntax=docker/dockerfile:1
##############################################################################
# docker/web/Dockerfile — 前端「生产构建 + nginx」镜像（admin / auditor 共用）
#
# 【作用】
#   把 cert-admin / cert-auditor 的前端源码做**生产构建**（vite build），
#   产物交给 nginx 静态托管，并同源反代 /api 到宿主后端（127.0.0.1:9992）。
#
#   为什么要「生产构建」而非 dev server：
#     隧道走的是跨境链路（CF 美西边缘），Vite dev 模式会按需请求
#     ~275 个源码模块 = ~275 次跨境往返；生产构建只有 ~87 个可压缩/可
#    长缓存的产物文件，首屏请求数量级下降。这是外网提速的主来源。
#
# 【构建上下文】仓库根（见 docker/web/compose.yml 的 context: ../..）
# 【构建参数】ROLE = admin | auditor | enterprise
# 【用法】由 scripts/deploy/deploy-web.sh 调用，或：
#     docker compose -f docker/web/compose.yml build --build-arg ...
##############################################################################

ARG NODE_IMAGE=node:22-slim
ARG NGINX_IMAGE=nginx:1.27-alpine
# 容器内 npm 源（国内直连 registry.npmjs.org 易 ECONNRESET）；可用 --build-arg 覆盖
ARG NPM_REGISTRY=https://registry.npmmirror.com

# ---------------------------------------------------------------------------
# 阶段 1：生产构建
# ---------------------------------------------------------------------------
FROM ${NODE_IMAGE} AS build
ARG ROLE=admin
ARG NPM_REGISTRY
WORKDIR /app

# 先只拷工作区 manifest（npm workspaces），最大化依赖层缓存
COPY src/certplatform-web/package.json src/certplatform-web/package-lock.json ./
COPY src/certplatform-web/yzh.vue.core/package.json       ./yzh.vue.core/
COPY src/certplatform-web/cert/cert-share/package.json    ./cert/cert-share/
COPY src/certplatform-web/cert/cert-admin/package.json    ./cert/cert-admin/
COPY src/certplatform-web/cert/cert-auditor/package.json  ./cert/cert-auditor/
COPY src/certplatform-web/cert/cert-enterprise/package.json ./cert/cert-enterprise/

RUN npm config set registry "$NPM_REGISTRY" \
 && npm ci --no-audit --no-fund

# 再拷源码并做生产构建。
# ⚠️ 这里直接跑 vite build，**不跑** guards.mjs / vue-tsc：
#    守卫脚本依赖 git 基线与宿主脚本目录，不适合进镜像；类型检查在本地 CI/提交前完成。
COPY src/certplatform-web/ ./
RUN cd "cert/cert-${ROLE}" && npx vite build

# ---------------------------------------------------------------------------
# 阶段 2：nginx 静态托管
# ---------------------------------------------------------------------------
FROM ${NGINX_IMAGE} AS runtime
ARG ROLE=admin

# 按角色选择 nginx 站点配置（admin 额外代理 /swagger）
COPY docker/web/nginx/${ROLE}.conf /etc/nginx/conf.d/default.conf

# 生产构建产物
COPY --from=build /app/cert/cert-${ROLE}/dist/ /usr/share/nginx/html/

EXPOSE 80
