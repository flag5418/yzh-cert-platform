using Microsoft.EntityFrameworkCore;
using System.Linq;
namespace VOL.Core.EFDbContext
{
    public abstract class BaseDbContext : DbContext
    {
        public BaseDbContext()
        {

        }
        public BaseDbContext(DbContextOptions<VOLContext> options)
            : base(options)
        {

        }
        public IQueryable<TEntity> Set<TEntity>(bool filterDeleted) where TEntity : class
        {
            return base.Set<TEntity>();
        }
    }
}
