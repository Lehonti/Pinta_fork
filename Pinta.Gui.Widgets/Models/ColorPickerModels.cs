using System;
using System.Diagnostics;
using Cairo;

namespace Pinta.Gui.Widgets;

public enum ColorTarget
{
	Primary,
	Secondary
}

public static class ColorTargetExtensions
{
	public static ColorTarget FromIndex (int index)
		=> index switch {
			0 => ColorTarget.Primary,
			1 => ColorTarget.Secondary,
			_ => throw new ArgumentOutOfRangeException (nameof (index), $"Invalid color target index: {index}")
		};
}

public abstract record ColorPick
{
	protected ColorPick () { }

	internal Color GetTargetedColor (ColorTarget target)
		 => this switch {
			 SingleColor sc => target == ColorTarget.Primary ? sc.Color : throw new InvalidOperationException (),
			 PaletteColors pc => target == ColorTarget.Primary ? pc.Primary : pc.Secondary,
			 _ => throw new UnreachableException ()
		 };

	internal ColorPick WithTargetedColor (ColorTarget target, Color color)
		=> this switch {
			SingleColor sc => target == ColorTarget.Primary ? sc with { Color = color } : throw new InvalidOperationException (),
			PaletteColors pc => target == ColorTarget.Primary ? pc with { Primary = color } : pc with { Secondary = color },
			_ => throw new UnreachableException ()
		};
}

public sealed record SingleColor (Color Color) : ColorPick;

public sealed record PaletteColors (Color Primary, Color Secondary) : ColorPick
{
	public PaletteColors Swapped () => new (Secondary, Primary);
}

public enum ColorSurfaceType
{
	HueAndSat,
	SatAndVal,
}

public sealed record LayoutSettings (int Margins, int PaletteDisplaySize, int PickerSurfaceRadius, int SliderWidth, int Spacing)
{
	public const int PICKER_SURFACE_PADDING = 10;
	public const int PALETTE_DISPLAY_BORDER_THICKNESS = 3;
	public const int CPS_PADDING_HEIGHT = 10;
	public const int CPS_PADDING_WIDTH = 14;

	public int PickerSurfaceDrawSize => (PickerSurfaceRadius + PICKER_SURFACE_PADDING) * 2;

	public static LayoutSettings Big { get; } = new (12, 50, 100, 200, 6);
	public static LayoutSettings Small { get; } = new (6, 40, 75, 150, 2);
}

public sealed record ColorPickerState (
	ColorPick Colors,
	ColorTarget ActiveTarget,
	ColorSurfaceType SurfaceType,
	bool IsSmallMode,
	bool ShowValueOnHueSat,
	bool ShowSwatches)
{
	public Color CurrentColor
		=> Colors.GetTargetedColor (ActiveTarget);

	public LayoutSettings Layout
		=> IsSmallMode ? LayoutSettings.Small : LayoutSettings.Big;

	public ColorPickerState WithCurrentColor (Color color)
	{
		if (CurrentColor.Equals (color)) return this;
		return this with { Colors = Colors.WithTargetedColor (ActiveTarget, color) };
	}

	public ColorPickerState SwapColors ()
	{
		if (Colors is not PaletteColors pc) return this;
		return this with { Colors = pc.Swapped () };
	}
}
