##############################################################################
# anydoc - 文档转 Markdown 转换服务 (Word/PPT/Excel/PDF/EPUB/CSV -> Markdown)
# 官方镜像: 无官方 Docker 镜像，基于 node:22-slim + @firecrawl/anydoc 自建
# 用法: docker exec yzh-anydoc anydoc /tmp/anydoc/xxx.docx
# 构建加速:
#   - 基础镜像若拉取慢，可先用镜像源拉取后 tag:
#     docker pull docker.m.daocloud.io/library/node:22-slim && docker tag docker.m.daocloud.io/library/node:22-slim node:22-slim
#   - npm registry 默认 npmmirror 国内镜像，可通过 --build-arg NPM_REGISTRY 覆盖
##############################################################################

FROM node:22-slim

# npm registry 加速（国内用 npmmirror，海外可覆盖为官方源）
ARG NPM_REGISTRY=https://registry.npmmirror.com

# 安装 anydoc CLI（二进制通过 optionalDependencies 从 npm registry 分发）
RUN npm install -g @firecrawl/anydoc --registry=${NPM_REGISTRY}

# 验证安装
RUN anydoc --version

# 转换工作目录（挂载宿主 ./anydoc/tmp 到 /tmp/anydoc）
WORKDIR /tmp/anydoc

# 保持容器运行，等待 docker exec 调用（与 libreoffice 容器模式一致）
CMD ["tail", "-f", "/dev/null"]
