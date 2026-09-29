using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace extraquickbars;

// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
#pragma warning disable CS8981 // 类名必须与 Mod 内部名（本工程为小写 extraquickbars）完全一致，这是 tModLoader 的硬性要求。
public class extraquickbars : Mod
{
	// 客户端快捷键：默认“未绑定”。F1/F2/F3 是原版“装备栏 1/2/3”的切换键，本 mod 不应抢占它们；
	// 需要独立快捷键的玩家可在“设置 - 控制”里自行绑定（Keys.None 不会与任何键冲突）。
	public static ModKeybind SwitchToBar1Keybind { get; private set; }
	public static ModKeybind SwitchToBar2Keybind { get; private set; }
	public static ModKeybind SwitchToBar3Keybind { get; private set; }

	public override void Load() {
		if (Main.dedServ) return;

		SwitchToBar1Keybind = KeybindLoader.RegisterKeybind(this, "SwitchToBar1", Keys.None);
		SwitchToBar2Keybind = KeybindLoader.RegisterKeybind(this, "SwitchToBar2", Keys.None);
		SwitchToBar3Keybind = KeybindLoader.RegisterKeybind(this, "SwitchToBar3", Keys.None);
	}

	public override void Unload() {
		SwitchToBar1Keybind = null;
		SwitchToBar2Keybind = null;
		SwitchToBar3Keybind = null;
	}
}
#pragma warning restore CS8981
