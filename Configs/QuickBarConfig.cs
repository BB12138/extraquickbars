using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace extraquickbars.Configs;

/// <summary>
/// 额外快捷栏的客户端配置。ConfigScope.ClientSide 保证只影响本机玩家，服务端不需要读取。
///
/// 各字段的显示名与说明文字来自 Localization 文件（tML 会自动生成同名的键，
/// 形如 Mods.extraquickbars.Configs.QuickBarConfig.&lt;字段名&gt;.Label / .Tooltip）。
/// </summary>
public class QuickBarConfig : ModConfig
{
	public override ConfigScope Mode => ConfigScope.ClientSide;

	[Header("$Mods.extraquickbars.Configs.QuickBarConfig.Headers.General")]
	[DefaultValue(true)]
	public bool FollowLoadoutSwitch = true;

	[DefaultValue(true)]
	public bool ButtonSwitchesLoadout = true;

	[DefaultValue(true)]
	public bool KeybindSwitchesLoadout = true;

	[DefaultValue(false)]
	public bool DropOnDeath = false;
}
