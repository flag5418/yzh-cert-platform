using VOL.Core.EFDbContext;

namespace VOL.Core.BaseProvider
{
    public interface IRepositoryDbContext
    {
        BaseDbContext DbContext { get; }
    }
}
