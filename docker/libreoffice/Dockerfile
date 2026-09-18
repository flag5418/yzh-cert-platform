# ============================================================
# yzh-libreoffice —— 文档 → PDF 预览转换镜像
#
# 基线：woahbase/alpine-libreoffice:latest（Alpine + LibreOffice headless）
# 增补：中文字体（CJK）
#
# 为什么必须装中文字体：
#   基线镜像不含任何 CJK 字体（fc-list | grep -i cjk 为 0），LibreOffice 转换
#   中文文档（如「XASL-QM 质量手册.doc」）时会因找不到字形而把汉字渲染成
#   「豆腐块」——PDF 能打开但中文全是方块，等同于无法预览。
#
# 构建与生效：
#   cd docker && docker compose build libreoffice && docker compose up -d libreoffice
# 校验：
#   docker exec yzh-libreoffice fc-list | grep -i cjk | wc -l   # 应 > 0
# ============================================================
FROM woahbase/alpine-libreoffice:latest

USER root

# font-noto-cjk：思源黑体/宋体（Noto Sans/Serif CJK，覆盖简繁日韩）
# ttf-dejavu 等基础字体已在基线镜像中，此处不重复安装
RUN apk add --no-cache font-noto-cjk \
 && fc-cache -f \
 && fc-list | grep -ci cjk

# 基线镜像默认工作目录与入口命令保持不变（compose 以 tail -f /dev/null 保持常驻）
