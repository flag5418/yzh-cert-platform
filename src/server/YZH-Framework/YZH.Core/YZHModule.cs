using System;
using System.Linq;
using System.Reflection;
using Autofac;
using YZH.Core.Audit;
using YZH.Core.CodeRule;
using YZH.Core.DeleteStrategy;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Excel;
using YZH.Core.Extractor.Models;
using YZH.Core.Extractor.Pdf;
using YZH.Core.Extractor.Text;
using YZH.Core.Extractor.Word;
using YZH.Core.Validation;
using YZH.Core.AI.Clients;
using YZH.Core.Workflow;
using YZH.Core.Skills;

namespace YZH.Core
{
    /// <summary>
    /// YZH-Framework Autofac 模块注册入口。
    /// 
    /// 设计原则（严格遵循 YZH-建设原则-V1.md §3.1）：
    /// - 通过 Vol 的 Autofac 容器挂载，不修改 Vol 源码
    /// - 在 vol.api 的 Startup 或 Program.cs 中一行代码集成
    /// - 分阶段注册，每阶段只注册已完成的组件
    /// 
    /// 集成方式（在 Vol 项目的 Startup.cs 中）：
    /// <code>
    /// public IServiceProvider ConfigureServices(IServiceCollection services)
    /// {
    ///     // ... Vol 原有配置 ...
    ///     
    ///     // 集成 YZH Framework（仅此一行）
    ///     builder.RegisterModule(new YZHModule());
    ///     
    ///     // ... 其他配置 ...
    /// }
    /// </code>
    /// 
    /// 注册策略：
    /// - 接口注册为 InstancePerLifetimeScope（每个请求一个实例）
    /// - 特性类不需要注册（.NET 运行时自动处理）
    /// - 服务实现按需注册，避免过度设计
    /// 
    /// 状态：[DONE] Phase 1 基础骨架完成
    /// Phase 2 将补充：ICodeRule 实现、校验服务、审计服务注册
    /// </summary>
    public class YZHModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // ============================================================
            // Phase 1: 基础设施注册（已完成）
            // ============================================================

            RegisterBaseInfrastructure(builder);

            // ============================================================
            // Phase 2: 核心能力注册（部分完成）
            // ============================================================

            RegisterExtractorServices(builder);
            RegisterLlmServices(builder);
            RegisterWorkflowServices(builder);

            // TODO:P2 - 取消以下注释以启用 Phase 2 组件
            // RegisterCodeRuleServices(builder);
            // RegisterValidationServices(builder);
            // RegisterAuditServices(builder);

            // ============================================================
            // Phase 3: 高级能力注册（待实现）
            // ============================================================

            // TODO:P3 - 取消以下注释以启用 Phase 3 组件
            // RegisterDeleteStrategyServices(builder);
            // RegisterIdempotentServices(builder);
            // RegisterMultiTenantServices(builder);
        }

        #region Phase 1: 基础设施

        /// <summary>
        /// 注册基础基础设施组件
        /// 包括：实体元数据服务、特性扫描器等
        /// </summary>
        private void RegisterBaseInfrastructure(ContainerBuilder builder)
        {
            // 注册当前程序集中的所有服务
            // 使用扫描模式，自动发现并注册实现了 IXxxService 接口的类
            var coreAssembly = Assembly.GetExecutingAssembly();

            // TODO:P2 - 实现自动扫描注册逻辑
            // 当前版本：手动注册已知的服务
            
            // 示例：当 YZHEntityMetadataService 实现后取消注释
            // builder.RegisterType<YZHEntityMetadataService>()
            //        .As<IYZHEntityMetadataService>()
            //        .InstancePerLifetimeScope();
            
            Console.WriteLine("[YZH] Module loaded successfully (Phase 1 - Base Infrastructure)");
        }

        #endregion

        #region Phase 2: 核心能力（部分完成）

        /// <summary>
        /// 注册文件提取能力（提取引擎的基础能力，见 docs/20-架构决策/文件数据提取能力落地-V1.md）。
        /// 状态：[DONE] Word(docx)/Excel/PDF(文本层)/纯文本 基本逻辑；[TODO:P2] 图片 OCR 第三方实现（IOcrExtractor）接入后在此注册。
        /// 说明：.doc（OLE2）因 NPOI NuGet 包无 HWPF 暂不支持，由提取器内部返回 Unsupported，不影响注册。
        /// </summary>
        private void RegisterExtractorServices(ContainerBuilder builder)
        {
            builder.RegisterType<NpoiWordExtractor>()
                   .Keyed<ITextExtractor>(ExtractSourceType.Word)
                   .InstancePerLifetimeScope();
            builder.RegisterType<NpoiExcelExtractor>()
                   .Keyed<ITextExtractor>(ExtractSourceType.Excel)
                   .InstancePerLifetimeScope();
            builder.RegisterType<PdfPigPdfExtractor>()
                   .Keyed<ITextExtractor>(ExtractSourceType.Pdf)
                   .InstancePerLifetimeScope();
            builder.RegisterType<PlainTextExtractor>()
                   .Keyed<ITextExtractor>(ExtractSourceType.Text)
                   .InstancePerLifetimeScope();
            builder.RegisterType<FileExtractorService>()
                   .As<IFileExtractor>()
                   .InstancePerLifetimeScope();

            // [TODO:P2] 图片 OCR：IOcrExtractor 第三方实现（腾讯云/百度 OCR）接入后，
            // 在此注册 Keyed<IOcrExtractor>(ExtractSourceType.Image) 并由 FileExtractorService 消费。
            Console.WriteLine("[YZH] Extractor services registered (Word/Excel/PDF/Text)");
        }

        /// <summary>
        /// 注册 LLM Gateway 服务（S1 完成）。
        /// Provider 顺序：QwenApiProvider（云端默认）/ OllamaProvider（本地断网兜底）/ MockProvider（测试）
        /// </summary>
        private void RegisterLlmServices(ContainerBuilder builder)
        {
            builder.RegisterType<QwenApiProvider>()
                   .As<ILlmProvider>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<OllamaProvider>()
                   .As<ILlmProvider>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<MockProvider>()
                   .As<ILlmProvider>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<LlmClient>()
                   .As<ILlmClient>()
                   .InstancePerLifetimeScope();
            Console.WriteLine("[YZH] LLM Gateway services registered (Qwen/Ollama/Mock)");
        }

        /// <summary>
        /// 注册工作流基础服务（S2/S4 补充 Skill/Engine 注册）。
        /// </summary>
        private void RegisterWorkflowServices(ContainerBuilder builder)
        {
            // S2: SkillRegistry + 内置 Skill
            builder.RegisterType<SkillRegistry>()
                   .As<ISkillRegistry>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<DocumentExtractSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<LlmExtractSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<CompareSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<GetFieldSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<GetTableSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<AssembleSkill>()
                   .As<ISkillNode>()
                   .InstancePerLifetimeScope();
            Console.WriteLine("[YZH] Workflow services registered (SkillRegistry + 6 built-in Skills)");
        }

        /// <summary>
        /// 注册编码规则相关服务
        /// TODO:P2 - Phase 2 实现
        /// </summary>
        private void RegisterCodeRuleServices(ContainerBuilder builder)
        {
            // TODO: 注册 ICodeRule 的默认实现
            // builder.RegisterType<DefaultCodeRuleService>()
            //        .As<ICodeRule>()
            //        .InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注册校验相关服务
        /// TODO:P2 - Phase 2 实现
        /// </summary>
        private void RegisterValidationServices(ContainerBuilder builder)
        {
            // TODO: 注册校验执行器
            // builder.RegisterType<YZHValidationExecutor>()
            //        .As<IYZHValidationExecutor>()
            //        .InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注册审计相关服务
        /// TODO:P2 - Phase 2 实现
        /// </summary>
        private void RegisterAuditServices(ContainerBuilder builder)
        {
            // TODO: 注册审计日志服务
            // builder.RegisterType<YZHAuditLogService>()
            //        .As<IYZHAuditLogService>()
            //        .InstancePerLifetimeScope();
        }

        #endregion

        #region Phase 3: 高级能力（待实现）

        /// <summary>
        /// 注册删除策略相关服务
        /// TODO:P3 - Phase 3 实现
        /// </summary>
        private void RegisterDeleteStrategyServices(ContainerBuilder builder)
        {
            // TODO: 注册删除策略工厂
            // builder.RegisterType<YZHDeleteStrategyFactory>()
            //        .As<IDeleteStrategyFactory>()
            //        .InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注册接口幂等性服务（Redis 防重复提交）
        /// TODO:P3 - Phase 3 实现
        /// </summary>
        private void RegisterIdempotentServices(ContainerBuilder builder)
        {
            // TODO: 注册幂等性过滤器和服务
        }

        /// <summary>
        /// 注册多租户隔离服务
        /// TODO:P3 - Phase 3 实现
        /// </summary>
        private void RegisterMultiTenantServices(ContainerBuilder builder)
        {
            // TODO: 注册多租户过滤器和服务
        }

        #endregion
    }
}
