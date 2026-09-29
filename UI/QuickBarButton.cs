using extraquickbars.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace extraquickbars.UI;

/// <summary>
/// 快捷栏切换按钮：循环切换到上/下一栏。
/// 位置由 <see cref="QuickBarUISystem"/> 每帧写入（复刻原版热键栏几何），本类只负责绘制与点击。
/// </summary>
public class QuickBarButton : UIElement
{
	public const float ButtonSize = 24f;

	// -1 = 上一栏，+1 = 下一栏
	private readonly int direction;
	private bool isShown;

	public QuickBarButton(int direction) {
		this.direction = direction;
		Width.Set(ButtonSize, 0f);
		Height.Set(ButtonSize, 0f);
	}

	/// <summary>由 UI 系统每帧调用：写入位置，并在不显示时把元素移出屏幕（避免参与鼠标命中测试）。</summary>
	public void ApplyLayout(Rectangle rect, bool visible) {
		isShown = visible;
		if (visible) {
			Left.Set(rect.X, 0f);
			Top.Set(rect.Y, 0f);
		} else {
			Left.Set(-1000f, 0f);
			Top.Set(-1000f, 0f);
		}
		Recalculate();
	}

	public override void LeftClick(UIMouseEvent evt) {
		base.LeftClick(evt);
		// 若同时切了装备栏，原版会播放它自己的音效，这里就不再叠加 MenuTick。
		if (Main.LocalPlayer.GetModPlayer<QuickBarPlayer>().SwitchFromButton(direction, out bool loadoutAlsoSwitched) && !loadoutAlsoSwitched)
			SoundEngine.PlaySound(SoundID.MenuTick);
	}

	protected override void DrawSelf(SpriteBatch spriteBatch) {
		if (!isShown) return;

		CalculatedStyle dimensions = GetDimensions();
		Color backgroundColor = IsMouseHovering ? Color.White : new Color(190, 190, 190, 220);
		spriteBatch.Draw(TextureAssets.InventoryBack9.Value, dimensions.ToRectangle(), backgroundColor);

		string arrow = Language.GetTextValue(direction < 0 ? "Mods.extraquickbars.UI.LeftArrow" : "Mods.extraquickbars.UI.RightArrow");
		DynamicSpriteFont font = FontAssets.MouseText.Value;
		Vector2 textSize = font.MeasureString(arrow);
		Vector2 textPosition = new Vector2(dimensions.X + (dimensions.Width - textSize.X) / 2f, dimensions.Y + (dimensions.Height - textSize.Y) / 2f);
		spriteBatch.DrawString(font, arrow, textPosition, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
	}
}
