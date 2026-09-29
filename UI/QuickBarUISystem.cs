using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace extraquickbars.UI;

/// <summary>
/// 在“原版热键栏”层之后绘制两个切换按钮。
/// 通过 [Autoload(Side = ModSide.Client)] 保证服务端完全不加载本 UI 代码。
/// 按钮位置复刻原版 Main.GUIHotbarDrawInner 的几何公式，因此热键栏缩放动画时也不会错位。
/// </summary>
[Autoload(Side = ModSide.Client)]
public class QuickBarUISystem : ModSystem
{
	public const string InterfaceLayerName = "ExtraQuickBars: Quick Bar Buttons";

	// 原版热键栏有两条绘制路径，按钮的基准位置随之不同：
	//  · 背包关闭：Main.GUIHotbarDrawInner —— X 起点 20，逐格 X += (int)(InventoryBack 宽 * hotbarScale[slot]) + 4
	//  · 背包打开：Main.DrawInventory   —— Main.inventoryScale 固定 0.85，X(col) = (int)(20 + col * 56 * 0.85)
	// ↓ 想手动调整按钮位置时，主要改这几个常量（尤其 HotbarOriginY 与 ButtonGap）。
	private const float HotbarOriginX = 20f;
	private const float HotbarOriginY = 26f;   // 按钮 Y 坐标（原版热键栏 Y 为 20，这里下移 3px；背包开/关共用）
	private const float ButtonGap = 8f;        // 按钮与热键栏右边缘的水平间距
	private const float ButtonSpacing = 2f;    // 两个按钮之间的间距
	private const float InventoryGridScale = 0.85f;   // 背包打开时 Main.DrawInventory 固定使用的 Main.inventoryScale
	private const float InventoryGridSpacing = 56f;   // 背包打开时相邻格的步进（未乘缩放）
	private const int HotbarSlotCount = 10;           // 快捷栏格数（与 QuickBarPlayer.BarSlotCount 一致）

	// 两种状态各自的“额外偏移”（都基于上面的基准值，互不影响，调哪一态就只影响哪一态）：
	//  · InventoryClosed* → 背包关闭时（左上角那排热键栏）
	//  · InventoryOpen*   → 背包打开时（DrawInventory 网格的第 0 行右侧）
	private const float InventoryClosedOffsetX = 0f;
	private const float InventoryClosedOffsetY = 0f;
	private const float InventoryOpenOffsetX = 0f;
	private const float InventoryOpenOffsetY = 0f;

	private static UserInterface quickBarInterface;
	private static QuickBarUIState quickBarState;

	/// <summary>
	/// 鼠标是否悬停在两个按钮上。由 UI 更新阶段每帧刷新（那里鼠标坐标才是 UI 空间），
	/// 供 <see cref="Players.QuickBarPlayer.CanUseItem"/> 在世界更新阶段读取。
	/// </summary>
	public static bool MouseOverButtons { get; private set; }

	public override void Load() {
		if (Main.dedServ) return;   // 双保险：[Autoload(Side = ModSide.Client)] 已保证服务端不加载本类
		quickBarState = new QuickBarUIState();
		quickBarInterface = new UserInterface();
		quickBarInterface.SetState(quickBarState);
	}

	public override void Unload() {
		quickBarState = null;
		quickBarInterface = null;
	}

	public override void UpdateUI(GameTime gameTime) {
		if (quickBarInterface == null || quickBarState == null) return;

		bool visible = ShouldShowButtons();
		quickBarState.UpdateButtonLayout(visible);

		// 悬停判定必须在本阶段做：只有 UI 更新阶段 Main.mouseX/mouseY 才是 UI 空间（PlayerInput.SetZoom_UI），
		// 与按钮矩形同空间；而世界更新阶段（CanUseItem 所在）的鼠标是未缩放屏幕像素。
		MouseOverButtons = visible && IsMouseOverButtonsInUISpace();

		// 隐藏时按钮被移到屏幕外（-1000），本就不会被点到；
		// 仍然每帧 Update，避免 UserInterface 的按下/松开状态机（InputPointerCache.WasDown）失同步。
		quickBarInterface.Update(gameTime);
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
		int hotbarIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Hotbar");
		if (hotbarIndex == -1) return;

		layers.Insert(hotbarIndex + 1, new LegacyGameInterfaceLayer(InterfaceLayerName, () => {
			if (quickBarInterface != null && ShouldShowButtons())
				quickBarInterface.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
			return true;
		}, InterfaceScaleType.UI));
	}

	/// <summary>按钮显示条件：世界内正常游玩时显示（**打开背包时同样显示**）；主菜单、幽灵状态、服务端不显示。</summary>
	public static bool ShouldShowButtons() {
		if (Main.dedServ || Main.gameMenu) return false;

		Player localPlayer = Main.LocalPlayer;
		return localPlayer != null && localPlayer.active && !localPlayer.ghost;
	}

	/// <summary>热键栏右边缘（UI 坐标像素）。背包打开/关闭对应原版两条不同的绘制路径，见顶部注释。</summary>
	public static float HotbarRightEdge() {
		if (Main.playerInventory) return InventoryHotbarRightEdge();

		float rightEdge = HotbarOriginX;
		for (int slot = 0; slot < Main.hotbarScale.Length; slot++)
			rightEdge += (int)(TextureAssets.InventoryBack.Width() * Main.hotbarScale[slot]) + 4f;   // 与 GUIHotbarDrawInner 完全一致
		return rightEdge;
	}

	// 背包打开时，0..9 格是 Main.DrawInventory 那个“10 列 × 5 行”网格的第 0 行（row = 0 → Y = 20），
	// 第 9 列的 X = (int)(20 + 9 * 56 * 0.85)，格宽 = InventoryBack 宽 * 0.85。
	private static float InventoryHotbarRightEdge() {
		float lastColumnX = (int)(HotbarOriginX + (HotbarSlotCount - 1) * InventoryGridSpacing * InventoryGridScale);
		return lastColumnX + (int)(TextureAssets.InventoryBack.Width() * InventoryGridScale);
	}

	/// <summary>
	/// 两个按钮的矩形（UI 坐标）。绘制与鼠标命中共用同一份几何计算，
	/// **想手动调位置只改这里的常量即可，两边会自动同步**。
	/// </summary>
	public static void GetButtonRects(out Rectangle leftButton, out Rectangle rightButton) {
		bool inventoryOpen = Main.playerInventory;
		float offsetX = inventoryOpen ? InventoryOpenOffsetX : InventoryClosedOffsetX;
		float offsetY = inventoryOpen ? InventoryOpenOffsetY : InventoryClosedOffsetY;
		int x = (int)(HotbarRightEdge() + ButtonGap + offsetX);
		int y = (int)(HotbarOriginY + offsetY);
		int size = (int)QuickBarButton.ButtonSize;
		leftButton = new Rectangle(x, y, size, size);
		rightButton = new Rectangle(x + size + (int)ButtonSpacing, y, size, size);
	}

	/// <summary>按钮命中测试。只能在 UI 更新阶段调用（此时 Main.MouseScreen 才是 UI 空间，与按钮矩形同空间）。</summary>
	private static bool IsMouseOverButtonsInUISpace() {
		GetButtonRects(out Rectangle leftButton, out Rectangle rightButton);
		Vector2 mouse = Main.MouseScreen;
		return leftButton.Contains((int)mouse.X, (int)mouse.Y) || rightButton.Contains((int)mouse.X, (int)mouse.Y);
	}

	/// <summary>按钮容器（两者都由本系统每帧重算位置）。</summary>
	private class QuickBarUIState : UIState
	{
		private QuickBarButton leftButton;
		private QuickBarButton rightButton;

		public override void OnInitialize() {
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			leftButton = new QuickBarButton(-1);
			rightButton = new QuickBarButton(1);
			Append(leftButton);
			Append(rightButton);
		}

		public void UpdateButtonLayout(bool visible) {
			GetButtonRects(out Rectangle leftRect, out Rectangle rightRect);
			leftButton.ApplyLayout(leftRect, visible);
			rightButton.ApplyLayout(rightRect, visible);
		}
	}
}
