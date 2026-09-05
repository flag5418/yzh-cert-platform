using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Services;
using VOL.Entity.Admin.Platform.Wf;
using VOL.Entity.DomainModels;
using VOL.CERT.IServices.Admin.Platform.Wf;
using VOL.CERT.IRepositories.Admin.Platform.Wf;
using YZH.Core;
using YZH.Core.Validation;

namespace VOL.CERT.Services.Admin.Platform.Wf
{
    /// <summary>
    /// 工作流定义 Service
    /// 继承 YZHTableServiceBase：自动获得审计填充、逻辑删除、唯一校验、生命周期钩子
    /// </summary>
    public class WorkflowDefinitionService
        : YZHTableServiceBase<WorkflowDefinition, IWorkflowDefinitionRepository>
        , IWorkflowDefinitionService
        , IDependency
    {
        public WorkflowDefinitionService() { }
        public WorkflowDefinitionService(IWorkflowDefinitionRepository repository) : base(repository) { }

        #region 业务扩展方法

        /// <summary>
        /// 按类型分页查询（业务参数走 OnGetPageDataBefore 注入过滤条件）
        /// </summary>
        public async Task<PageGridData<WorkflowDefinition>> GetPageDataAsync(
            PageDataOptions options, string workflowType = null, bool? isActive = null)
        {
            // 通过查询参数传递业务过滤条件
            if (!string.IsNullOrWhiteSpace(workflowType))
            {
                options.Filter ??= new List<SearchParameters>();
                options.Filter.Add(new SearchParameters { Name = "WorkflowType", Value = workflowType });
            }

            if (isActive.HasValue)
            {
                options.Filter ??= new List<SearchParameters>();
                options.Filter.Add(new SearchParameters { Name = "IsActive", Value = isActive.Value.ToString() });
            }

            // 调用基类 GetPageData（内部走 YZH 管道：审计/校验/删除）
            return GetPageData(options);
        }

        /// <summary>
        /// 按类型获取列表（用于下拉选择等场景）
        /// </summary>
        public async Task<List<WorkflowDefinition>> GetListAsync(string workflowType = null, bool? isActive = null)
        {
            var query = repository.FindAsIQueryable(x => x.Enable);
            if (!string.IsNullOrWhiteSpace(workflowType))
                query = query.Where(x => x.WorkflowType == workflowType);
            if (isActive.HasValue)
                query = query.Where(x => x.IsActive == isActive.Value);
            return await query.OrderBy(x => x.WorkflowCode).ToListAsync();
        }

        /// <summary>
        /// 按编码查询（唯一键快捷查询）
        /// </summary>
        public async Task<WorkflowDefinition?> GetByCodeAsync(string workflowCode)
        {
            return await repository.FindAsIQueryable(
                x => x.WorkflowCode == workflowCode && x.Enable).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 切换激活状态（ToggleActive 是业务特殊操作，不走标准 Del/Update）
        /// </summary>
        public async Task<bool> ToggleActiveAsync(long id)
        {
            var entity = await repository.FindAsIQueryable(x => x.Id == id).FirstOrDefaultAsync();
            if (entity == null) return false;

            entity.IsActive = !entity.IsActive;
            repository.Update(entity, new[] { "IsActive" });
            repository.SaveChanges();
            return true;
        }

        /// <summary>
        /// 保存（新增或修改）- 走 YZH 基类管道（审计+校验+生命周期钩子）
        /// </summary>
        public async Task<bool> SaveAsync(WorkflowDefinition entity)
        {
            // 使用反射构建字典（Vol 框架 ToDictionary 需要传 Expression 参数，无法直接推断）
            var mainData = new Dictionary<string, object>();
            foreach (var prop in typeof(WorkflowDefinition).GetProperties().Where(p => p.CanRead))
            {
                mainData[prop.Name] = prop.GetValue(entity) ?? string.Empty;
            }
            var saveModel = new SaveModel { MainData = mainData };

            // 根据 Id 判断新增/更新
            if (entity.Id > 0)
            {
                var result = await UpdateAsync(saveModel);
                return result.Status;
            }
            else
            {
                var result = await AddAsync(saveModel);
                return result.Status;
            }
        }

        /// <summary>
        /// 删除 - 走 YZH 基类管道（逻辑删除 + 审计）
        /// </summary>
        public async Task<bool> DeleteAsync(long id)
        {
            var result = await DelAsync(new object[] { id });
            return result.Status;
        }

        #endregion

        #region 生命周期钩子（按需覆写）

        /// <summary>
        /// 新增前校验：workflow_code 唯一性已在 [UniqueField] 自动校验，此处可补业务校验
        /// </summary>
        protected override EntityValidationResult OnAdding(WorkflowDefinition entity)
        {
            // TODO: 如有需要，可补 workflow_config JSON 格式校验
            return base.OnAdding(entity);
        }

        #endregion
    }
}
