using YZH.Core.EFDbContext;

namespace YZH.Core.BaseProvider
{
    public interface IRepositoryDbContext
    {
        BaseDbContext DbContext { get; }
    }
}
