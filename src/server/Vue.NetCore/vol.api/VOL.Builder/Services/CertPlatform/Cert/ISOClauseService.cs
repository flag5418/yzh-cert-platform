using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class ISOClauseService : IISOClauseService
    {
        private readonly IISOClauseRepository _repository;

        public ISOClauseService(IISOClauseRepository repository)
        {
            _repository = repository;
        }

        public static IISOClauseService Instance =>
            AutofacContainerModule.GetService<IISOClauseService>();

        /// <summary>
        /// 获取所有 ISO 标准列表（供标准条款页面选择）
        /// </summary>
        public async Task<List<object>> GetStandardsAsync()
        {
            var standards = await _repository.DbContext.Set<ISOStandard>()
                .Where(x => x.Enable == true)
                .Select(x => new
                {
                    code = x.Code,
                    standardName = x.StandardName,
                    standardCode = x.StandardCode
                })
                .ToListAsync();
            return standards.Select(x => (object)x).ToList();
        }

        /// <summary>
        /// 获取指定标准的条款树形数据
        /// </summary>
        public async Task<List<ClauseTreeNode>> GetClauseTreeAsync(string standardCode)
        {
            var clauses = await _repository.FindAsIQueryable(x => x.StandardCode == standardCode && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            // 构建 code -> node 映射
            var nodeMap = new Dictionary<string, ClauseTreeNode>();
            foreach (var c in clauses)
            {
                nodeMap[c.Code] = new ClauseTreeNode
                {
                    Id = c.Id,
                    Code = c.Code,
                    Label = $"{c.ClauseNumber} {c.Title}",
                    ClauseNumber = c.ClauseNumber,
                    Title = c.Title,
                    Description = c.Description,
                    SortOrder = c.SortOrder,
                    ParentCode = c.ParentCode
                };
            }

            // 构建树
            var rootNodes = new List<ClauseTreeNode>();
            foreach (var c in clauses)
            {
                if (!string.IsNullOrWhiteSpace(c.ParentCode) && nodeMap.ContainsKey(c.ParentCode))
                {
                    nodeMap[c.ParentCode].Children.Add(nodeMap[c.Code]);
                }
                else
                {
                    rootNodes.Add(nodeMap[c.Code]);
                }
            }

            return rootNodes;
        }

        /// <summary>
        /// 获取条款列表（平铺）
        /// </summary>
        public async Task<List<ISOClause>> GetListAsync(string standardCode)
        {
            var query = _repository.FindAsIQueryable(x => x.Enable == true);
            if (!string.IsNullOrWhiteSpace(standardCode))
                query = query.Where(x => x.StandardCode == standardCode);
            return await query.OrderBy(x => x.SortOrder).ToListAsync();
        }

        /// <summary>
        /// 保存条款（新增/编辑）
        /// </summary>
        public async Task<bool> SaveAsync(ISOClause entity)
        {
            if (entity.Id > 0)
            {
                var existing = await _repository.FindFirstAsync(x => x.Id == entity.Id);
                if (existing == null) return false;
                existing.StandardCode = entity.StandardCode;
                existing.ParentCode = entity.ParentCode;
                existing.ClauseNumber = entity.ClauseNumber;
                existing.Title = entity.Title;
                existing.Description = entity.Description;
                existing.SortOrder = entity.SortOrder;
                _repository.Update(existing, new[] { "StandardCode", "ParentCode", "ClauseNumber", "Title", "Description", "SortOrder" }, true);
                return true;
            }

            // 新建时自动生成 Code
            if (string.IsNullOrWhiteSpace(entity.Code))
                entity.Code = System.Guid.NewGuid().ToString("N");
            entity.CreateDate = System.DateTime.Now;
            entity.Creator = UserContext.Current?.UserName;
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 删除条款（软删除）
        /// </summary>
        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return false;

            // 检查是否有子条款
            var hasChildren = await _repository.FindAsIQueryable(x => x.ParentCode == entity.Code && x.Enable == true).AnyAsync();
            if (hasChildren)
                return false;

            // 软删除
            entity.MarkAsDeleted(UserContext.Current?.UserId ?? 0, UserContext.Current?.UserName);
            _repository.Update(entity, new[] { "Enable", "DeleteID", "Deleter", "DeleteTime" }, true);
            return true;
        }
    }
}
