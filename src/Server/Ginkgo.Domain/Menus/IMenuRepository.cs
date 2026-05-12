using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Menus
{
    /// <summary>
    /// 菜单仓储契约。
    /// </summary>
    public interface IMenuRepository
    {
        /// <summary>
        /// 获取全部菜单，按 ParentId、Order 排序。
        /// </summary>
        Task<List<Menu>> GetAllOrderedAsync(CancellationToken ct = default);
    }
}

