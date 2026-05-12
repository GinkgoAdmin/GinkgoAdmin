using SqlSugar;

namespace Ginkgo.Domain.Modules
{
	[SugarTable("ginkgo_Modules_Installed")]
	[SugarIndex("IX_InstalledModule_ModuleId", nameof(ModuleId), OrderByType.Asc)]
	public sealed class InstalledModuleEntity : Ginkgo.Domain.AuditableEntity
	{
		// 目标库缺少 DeletedAt/DeletedBy 列，屏蔽这两列的映射，避免 SELECT 引用不存在的列
		[SugarColumn(IsIgnore = true)] public new DateTime? DeletedAt { get; set; }
		[SugarColumn(IsIgnore = true)] public new Guid? DeletedBy { get; set; }

		[SugarColumn(Length = 200, IsNullable = false)]
		public string ModuleId { get; set; } = string.Empty;

		[SugarColumn(Length = 200, IsNullable = false)]
		public string Name { get; set; } = string.Empty;

		[SugarColumn(Length = 50, IsNullable = false)]
		public string Version { get; set; } = string.Empty;

		public bool HasClient { get; set; }

		/// <summary>
		/// 是否启用（启用：程序启动时加载；禁用：启动时跳过）。
		/// </summary>
		[SugarColumn(ColumnDescription = "是否启用（启用：程序启动时加载；禁用：启动时跳过）")]
		public bool Enabled { get; set; } = true;

		[SugarColumn(Length = 200, IsNullable = true)]
		public string? Publisher { get; set; }

		[SugarColumn(Length = 500, IsNullable = true)]
		public string? Homepage { get; set; }

		public DateTime InstalledAtUtc { get; set; } = DateTime.Now;

		/// <summary>
		/// 插件菜单根编码（来自 install.json 的 Menus.RootCode），卸载时用于定位并移除关联菜单
		/// </summary>
		[SugarColumn(Length = 200, IsNullable = true)]
		public string? MenuRootCode { get; set; }
	}
}
