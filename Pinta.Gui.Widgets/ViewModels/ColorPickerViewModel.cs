using System;
using Pinta.Core;
using Cairo;
using System.Diagnostics;
using System.Linq;

namespace Pinta.Gui.Widgets;

public sealed class ColorPickerViewModel
{
	private readonly ColorPick original_colors;
	private readonly PaletteManager? palette_manager;
	private readonly bool live_palette_mode;

	public ColorPickerState State { get; private set; }

	public event EventHandler? StateChanged;

	public ColorPickerViewModel (
		ColorPick initialColors,
		bool primarySelected,
		bool livePaletteMode,
		bool showSwatches,
		PaletteManager? paletteManager = null)
	{
		original_colors = initialColors;
		live_palette_mode = livePaletteMode;
		palette_manager = paletteManager;
		State = ColorPickerState.Default (initialColors, primarySelected, showSwatches);

		// Only subscribe to external palette changes if in live mode.
		if (live_palette_mode && palette_manager != null) {
			palette_manager.PrimaryColorChanged += OnPalettePrimaryColorChanged;
			palette_manager.SecondaryColorChanged += OnPaletteSecondaryColorChanged;
		}
	}

	// Used for cleanup when the dialog closes.
	public void Dispose ()
	{
		if (live_palette_mode && palette_manager != null) {
			palette_manager.PrimaryColorChanged -= OnPalettePrimaryColorChanged;
			palette_manager.SecondaryColorChanged -= OnPaletteSecondaryColorChanged;
		}
	}

	private void UpdateState (ColorPickerState newState)
	{
		if (State == newState) return;
		State = newState;
		StateChanged?.Invoke (this, EventArgs.Empty);
	}

	// --- State Update Methods ---

	public void SetCurrentColor (Color color)
	{
		UpdateState (State.WithCurrentColor (color));
	}

	public void SetColorFromHsv (double? hue = null, double? sat = null, double? value = null)
	{
		Color newColor = State.CurrentColor.CopyHsv (hue: hue, sat: sat, value: value);
		SetCurrentColor (newColor);
	}

	public void SetColorFromRgb (double? r = null, double? g = null, double? b = null)
	{
		Color c = State.CurrentColor;
		Color newColor = new (
			r ?? c.R,
			g ?? c.G,
			b ?? c.B,
			c.A
		);
		SetCurrentColor (newColor);
	}

	public void SetAlpha (double alpha)
	{
		Color c = State.CurrentColor;
		if (Math.Abs (c.A - alpha) < 0.001) return;
		Color newColor = c with { A = alpha };
		SetCurrentColor (newColor);
	}

	public bool SetColorFromHex (string hex)
	{
		Color? newColor = Color.FromHex (hex);
		if (newColor.HasValue) {
			SetCurrentColor (newColor.Value);
			return true;
		}
		return false;
	}

	public void ToggleSmallMode ()
	{
		UpdateState (State with { IsSmallMode = !State.IsSmallMode });
	}

	public void SetSurfaceType (ColorSurfaceType type)
	{
		UpdateState (State with { SurfaceType = type });
	}

	public void SetShowValueOnHueSat (bool show)
	{
		UpdateState (State with { ShowValueOnHueSat = show });
	}

	public void SetActiveTarget (ColorTarget target)
	{
		if (State.Colors is SingleColor && target != ColorTarget.Primary)
			return; // Ignore attempt to select secondary if not available

		UpdateState (State with { ActiveTarget = target });
	}

	public void SwapColors ()
	{
		UpdateState (State.SwapColors ());
	}

	public void ResetColors ()
	{
		// Ensure the target remains valid if the color type changes (e.g., from PaletteColors back to SingleColor)
		ColorTarget newTarget = State.ActiveTarget;
		if (original_colors is SingleColor && State.ActiveTarget == ColorTarget.Secondary) {
			newTarget = ColorTarget.Primary;
		}

		UpdateState (State with { Colors = original_colors, ActiveTarget = newTarget });
	}

	// --- Color Surface Interaction Logic (Pure Functions) ---

	public PointD HsvToPickerLocation (HsvColor hsv)
	{
		int radius = State.Layout.PickerSurfaceRadius;
		switch (State.SurfaceType) {
			case ColorSurfaceType.HueAndSat: {
					// Polar coordinates
					double rad = hsv.Hue * (Math.PI / 180.0);
					double mag = hsv.Sat * radius;
					double x = Math.Cos (rad) * mag;
					double y = Math.Sin (rad) * mag;
					// Invert Y axis as per original implementation requirement
					return new (x, -y);
				}

			case ColorSurfaceType.SatAndVal: {
					// Cartesian coordinates
					int size = radius * 2;
					double x = hsv.Val * (size - 1);
					double y = size - hsv.Sat * (size - 1);
					// Translate center to (0,0)
					return new (x - radius, y - radius);
				}
			default:
				throw new UnreachableException ();
		}
	}

	public void SetColorFromPickerSurface (PointI cursor)
	{
		int radius = State.Layout.PickerSurfaceRadius;
		int size = radius * 2;

		if (State.SurfaceType == ColorSurfaceType.HueAndSat) {
			PointI centre = new (radius, radius);
			PointI vecCursor = cursor - centre;

			double hue = (Math.Atan2 (vecCursor.Y, -vecCursor.X) + Math.PI) / (2f * Math.PI) * 360f;
			double sat = Math.Min (vecCursor.Magnitude () / radius, 1);

			SetColorFromHsv (hue: hue, sat: sat);
		} else if (State.SurfaceType == ColorSurfaceType.SatAndVal) {
			// Clamp cursor position within the square boundaries
			int clampedX = Math.Clamp (cursor.X, 0, size - 1);
			int clampedY = Math.Clamp (cursor.Y, 0, size - 1);

			double s = 1f - (double) clampedY / (size - 1);
			double v = (double) clampedX / (size - 1);

			SetColorFromHsv (sat: s, value: v);
		}
	}

	// --- Live Palette Management ---

	// Called when the dialog loses focus in live mode.
	public void CommitLivePaletteChanges ()
	{
		if (!live_palette_mode || palette_manager == null || State.Colors is not PaletteColors paletteColors)
			return;

		// Only update if necessary to avoid spamming the recent colors palette.
		if (palette_manager.PrimaryColor != paletteColors.Primary)
			palette_manager.PrimaryColor = paletteColors.Primary;

		if (palette_manager.SecondaryColor != paletteColors.Secondary)
			palette_manager.SecondaryColor = paletteColors.Secondary;
	}

	private void OnPalettePrimaryColorChanged (object? sender, EventArgs _)
	{
		Color newPrimary = ((PaletteManager) sender!).PrimaryColor;
		ColorPick newColors = State.Colors switch {
			SingleColor sc => sc with { Color = newPrimary },
			PaletteColors pc => pc with { Primary = newPrimary },
			_ => throw new UnreachableException (),
		};
		UpdateState (State with { Colors = newColors });
	}

	private void OnPaletteSecondaryColorChanged (object? sender, EventArgs _)
	{
		if (State.Colors is not PaletteColors paletteColors) return;
		Color newSecondary = ((PaletteManager) sender!).SecondaryColor;
		ColorPick newColors = paletteColors with { Secondary = newSecondary };
		UpdateState (State with { Colors = newColors });
	}

	// --- Swatch Interaction Logic ---

	public void SetColorFromRecentSwatches (PointD relPos)
	{
		if (palette_manager == null) return;

		// RectangleD argument is empty as in the original implementation.
		int recent_index = PaletteWidget.GetSwatchAtLocation (palette_manager, relPos, new RectangleD (), true);

		if (recent_index < 0 || recent_index >= palette_manager.RecentlyUsedColors.Count)
			return;

		SetCurrentColor (palette_manager.RecentlyUsedColors.ElementAt (recent_index));
	}

	public void SetColorFromPaletteSwatches (PointD relPos)
	{
		if (palette_manager == null) return;

		int index = PaletteWidget.GetSwatchAtLocation (palette_manager, relPos, new RectangleD ());

		if (index < 0 || index >= palette_manager.CurrentPalette.Colors.Count)
			return;

		SetCurrentColor (palette_manager.CurrentPalette.Colors[index]);
	}
}
