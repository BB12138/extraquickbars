using System;
using System.Collections.Generic;
using extraquickbars.Configs;
using extraquickbars.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace extraquickbars.Players;

/// <summary>
/// 三套快捷栏（每套 10 格）的数据、切换、存档与死亡处理。
///
/// 核心设计：原版快捷栏（Player.inventory 的 0..9 格）本身就是“当前激活栏”的显示窗口，
/// 所以激活栏的物品只存在于 inventory[0..9]；storage[activeBar] 恒为一个空闲数组（内容无意义），
/// 其余 storage[bar] 保存对应栏的物品。切换时只搬运 Item 引用（不序列化、不复制），
/// 因此 ModItem 实例、GlobalItem 数据、前缀、收藏等全部原样保留，也不存在镜像副本导致的双重掉落。
/// </summary>
public class QuickBarPlayer : ModPlayer
{
	public const int BarCount = 3;
	public const int BarSlotCount = 10;

	// 不变量：storage[activeBar] 是空闲数组；激活栏物品在 Player.inventory[0..9]。
	private readonly Item[][] storage = CreateStorage();
	private int activeBar;
	private bool needsRestore;
	private bool deathProtected;

	/// <summary>当前激活的快捷栏下标（0 .. BarCount - 1）。</summary>
	public int ActiveBar => activeBar;

	private static QuickBarConfig BarConfig => ModContent.GetInstance<QuickBarConfig>();

	private static Item[][] CreateStorage() {
		Item[][] bars = new Item[BarCount][];
		for (int barIndex = 0; barIndex < BarCount; barIndex++) {
			bars[barIndex] = new Item[BarSlotCount];
			for (int slot = 0; slot < BarSlotCount; slot++)
				bars[barIndex][slot] = new Item();
		}
		return bars;
	}

	/// <summary>
	/// 切换到指定快捷栏。fromPlayerInput 为 true 表示由快捷键触发，此时遵循原版行为"使用物品中不切换"；
	/// 界面按钮传 false，任何时候都能切（不会被挥砍动画吞掉）。
	/// </summary>
	/// <returns>是否真的发生了切换。</returns>
	public bool SwitchTo(int newBar, bool fromPlayerInput) {
		if (newBar < 0 || newBar >= BarCount || newBar == activeBar) return false;
		if (Player.whoAmI != Main.myPlayer) return false;
		if (Player.inventory == null || Player.inventory.Length < BarSlotCount) return false;
		if (Player.dead || deathProtected || needsRestore) return false;
		// 按键（快捷键 / 装备栏跟随）遵循原版快捷栏行为：使用物品时不切换；
		// 界面按钮属于显式点击，若也被挥砍动画拦掉就会出现"点了没反应"，因此按钮路径不受此限制。
		if (fromPlayerInput && Player.itemAnimation > 0) return false;

		DepositMouseItem();

		// 注意：这里直接改写本地 inventory[0..9]，不做网络发包（本 mod 定位为客户端功能）。
		Item[] target = storage[newBar];
		for (int slot = 0; slot < BarSlotCount; slot++)
			(Player.inventory[slot], target[slot]) = (target[slot], Player.inventory[slot]);

		// 元素交换后 target 里装的是“旧激活栏”的物品，需要把数组引用轮换一下：
		// 旧栏物品归位到旧栏下标，新激活栏对应位置重新变成空闲数组。
		// （原版 Player.TrySwitchingLoadout -> EquipmentLoadout.Swap 也是交换数组引用。）
		(storage[activeBar], storage[newBar]) = (storage[newBar], storage[activeBar]);
		activeBar = newBar;
		return true;
	}

	/// <summary>
	/// 界面按钮入口：direction = -1 上一栏 / +1 下一栏，循环切换。
	/// loadoutAlsoSwitched 供调用方决定是否播放本 mod 的点击音效（联动时原版会自己播装备栏音效）。
	/// </summary>
	public bool SwitchFromButton(int direction, out bool loadoutAlsoSwitched) {
		loadoutAlsoSwitched = false;
		int target = (activeBar + direction + BarCount) % BarCount;

		if (!SwitchTo(target, fromPlayerInput: false)) return false;

		if (BarConfig?.ButtonSwitchesLoadout ?? true)
			loadoutAlsoSwitched = TryFollowLoadoutAfterManualSwitch(target);
		return true;
	}

	/// <summary>
	/// 主动切换（界面按钮 / 快捷键）之后，按配置尝试把装备栏也切到同一编号。
	/// 走原版 Player.TrySwitchingLoadout（自带守卫 / 音效 / 粒子 / 联机同步）；
	/// 被它的守卫拒绝时只切了快捷栏，不回滚 —— 保证“点了永远有响应”。
	/// </summary>
	/// <returns>装备栏是否真的跟着切换了。</returns>
	private bool TryFollowLoadoutAfterManualSwitch(int targetBar) {
		if (targetBar == Player.CurrentLoadoutIndex) return false;

		int before = Player.CurrentLoadoutIndex;
		Player.TrySwitchingLoadout(targetBar);
		return Player.CurrentLoadoutIndex != before;
	}

	// 鼠标上拿着物品时切换：优先塞进当前快捷栏空位，其次背包空位，都满则留在鼠标上。
	// 鼠标槽不受快捷栏切换影响，所以“留在鼠标上”也不会丢物品。
	private void DepositMouseItem() {
		Item mouse = Main.mouseItem;
		if (mouse == null || mouse.IsAir) return;

		for (int slot = 0; slot < BarSlotCount; slot++) {
			if (Player.inventory[slot].IsAir) {
				Player.inventory[slot] = mouse;
				Main.mouseItem = new Item();
				return;
			}
		}
		for (int slot = BarSlotCount; slot < Player.inventory.Length; slot++) {
			if (Player.inventory[slot].IsAir) {
				Player.inventory[slot] = mouse;
				Main.mouseItem = new Item();
				return;
			}
		}
	}

	// 把激活栏的 10 格与 storage[activeBar]（空闲数组）整体交换，自反操作。
	private void SwapInventoryWithActiveStorage() {
		Item[] bar = storage[activeBar];
		for (int slot = 0; slot < BarSlotCount; slot++)
			(Player.inventory[slot], bar[slot]) = (bar[slot], Player.inventory[slot]);
	}

	#region 存档

	private static List<TagCompound> SaveBar(Item[] bar) {
		// 空格子也写入（恒 10 项），这样读档时下标与数量保持稳定。
		List<TagCompound> tags = new List<TagCompound>(BarSlotCount);
		for (int slot = 0; slot < BarSlotCount; slot++)
			tags.Add(ItemIO.Save(bar[slot]));
		return tags;
	}

	private Item[] GetBarForSaving(int barIndex) {
		// 正常状态激活栏物品在 inventory[0..9]；读档未恢复、死亡暂存这两种状态只存在于同一帧内，回退到 storage。
		if (barIndex == activeBar && !needsRestore && !deathProtected)
			return Player.inventory;
		return storage[barIndex];
	}

	public override void SaveData(TagCompound tag) {
		for (int barIndex = 0; barIndex < BarCount; barIndex++)
			tag["Bar" + barIndex] = SaveBar(GetBarForSaving(barIndex));
		tag["ActiveBar"] = activeBar;
	}

	public override void LoadData(TagCompound tag) {
		for (int barIndex = 0; barIndex < BarCount; barIndex++)
			LoadBar(tag, "Bar" + barIndex, barIndex);

		activeBar = Utils.Clamp(tag.ContainsKey("ActiveBar") ? tag.GetInt("ActiveBar") : 0, 0, BarCount - 1);
		needsRestore = true;   // 真正写入延后到 OnEnterWorld / 首个 PostUpdate（那时原版数据已就绪）
	}

	private void LoadBar(TagCompound tag, string key, int barIndex) {
		Item[] bar = storage[barIndex];
		List<TagCompound> tags = null;

		if (tag.ContainsKey(key)) {
			try {
				tags = tag.Get<List<TagCompound>>(key);
			} catch (Exception exception) {
				Mod.Logger.Warn("ExtraQuickBars: 读取 " + key + " 失败，该栏按空栏处理。" + exception.Message);
			}
		}

		for (int slot = 0; slot < BarSlotCount; slot++) {
			Item item = null;
			if (tags != null && slot < tags.Count) {
				try {
					item = ItemIO.Load(tags[slot]);
				} catch (Exception exception) {
					Mod.Logger.Warn("ExtraQuickBars: 读取 " + key + " 第 " + slot + " 格失败。" + exception.Message);
				}
			}
			bar[slot] = item ?? new Item();
		}
	}

	// 把 storage[activeBar] 里的激活栏物品搬进 inventory[0..9]，并让 storage[activeBar] 重新变为空闲数组。
	private void RestoreActiveBarFromStorage() {
		Item[] bar = storage[activeBar];
		for (int slot = 0; slot < BarSlotCount; slot++) {
			Player.inventory[slot] = bar[slot];
			bar[slot] = new Item();
		}
	}

	private void ResetAllBars() {
		for (int barIndex = 0; barIndex < BarCount; barIndex++) {
			for (int slot = 0; slot < BarSlotCount; slot++)
				storage[barIndex][slot] = new Item();
		}
		activeBar = 0;
	}

	#endregion

	#region 生命周期

	public override void OnEnterWorld() {
		if (needsRestore) {
			RestoreActiveBarFromStorage();
			needsRestore = false;
		} else {
			// ModPlayer 实例在一次游戏会话内不会重建，换档/换世界时必须清掉上一局的残留数据。
			ResetAllBars();
		}
		deathProtected = false;
	}

	public override void PostUpdate() {
		if (needsRestore) {
			// 兜底：非单机时（角色数据由服务端下发）OnEnterWorld 可能早于数据到达。
			RestoreActiveBarFromStorage();
			needsRestore = false;
		}
		if (deathProtected && !Player.dead) {
			// 死亡被其它 mod 取消（PreKill 返回 false）时把暂存的激活栏换回来，避免物品卡在 storage 里。
			SwapInventoryWithActiveStorage();
			deathProtected = false;
		}

		HandleKeybinds();
	}

	private void HandleKeybinds() {
		if (Player.whoAmI != Main.myPlayer || Main.gameMenu) return;

		if (extraquickbars.SwitchToBar1Keybind?.JustPressed == true) SwitchByKeybind(0);
		if (extraquickbars.SwitchToBar2Keybind?.JustPressed == true) SwitchByKeybind(1);
		if (extraquickbars.SwitchToBar3Keybind?.JustPressed == true) SwitchByKeybind(2);
	}

	private void SwitchByKeybind(int targetBar) {
		if (!SwitchTo(targetBar, fromPlayerInput: true)) return;

		if (BarConfig?.KeybindSwitchesLoadout ?? true)
			TryFollowLoadoutAfterManualSwitch(targetBar);
	}

	public override bool CanUseItem(Item item) {
		// 鼠标悬停在快捷栏按钮上时阻止使用物品，避免点按钮的同时放下方块 / 挥动武器。
		// 注意：本钩子运行在"世界更新"阶段，此时 Main.mouseX/mouseY 是未缩放屏幕像素，
		// 而按钮矩形是 UI 空间（UI 更新阶段 Main.mouseX 才会除以 Main.UIScale），
		// 所以这里只读取 UI 阶段算好的缓存，绝不自己比较坐标（否则 Main.UIScale != 1 时会整体错位）。
		if (Player.whoAmI != Main.myPlayer) return true;
		return !QuickBarUISystem.MouseOverButtons;
	}

	#endregion

	#region 死亡

	public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
		if (Player.whoAmI != Main.myPlayer || needsRestore || deathProtected) return true;

		// 关键时机：PreKill 位于 Player.KillMe 的最前面，早于中核 / 硬核的 DropItems()。
		// 先把这 10 格挪空，原版就不会掉落它们，之后按配置处理，因此永不双重掉落。
		SwapInventoryWithActiveStorage();
		deathProtected = true;
		return true;   // 不干预死亡本身
	}

	public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
		if (!deathProtected) return;

		if (BarConfig?.DropOnDeath ?? false)
			DropActiveBar();                       // 只掉激活栏，另外两栏始终保留
		else
			SwapInventoryWithActiveStorage();      // 换回 inventory[0..9]，三栏全部保留

		deathProtected = false;
	}

	private void DropActiveBar() {
		Item[] bar = storage[activeBar];
		for (int slot = 0; slot < BarSlotCount; slot++) {
			Item item = bar[slot];
			if (item != null && !item.IsAir)
				// 用 Item 重载掉落，前缀 / 堆叠 / ModItem 数据都完整保留。
				Item.NewItem(Player.GetSource_Death("ExtraQuickBars"), Player.Center, item, false, false, false);
			bar[slot] = new Item();
		}
	}

	#endregion

	#region 装备栏联动

	public override void OnEquipmentLoadoutSwitched(int oldLoadoutIndex, int loadoutIndex) {
		if (Player.whoAmI != Main.myPlayer) return;
		if (Main.netMode == NetmodeID.Server) return;   // UI 相关逻辑只在客户端处理
		// 跟随开关只负责这一件事：关闭后按 F1/F2/F3 只切换装备栏，快捷栏不动。
		if (!(BarConfig?.FollowLoadoutSwitch ?? true)) return;

		SwitchTo(loadoutIndex, fromPlayerInput: true);   // 装备栏 0/1/2 与快捷栏 0/1/2 一一对应
	}

	#endregion
}
