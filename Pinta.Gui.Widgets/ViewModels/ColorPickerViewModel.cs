using System;
using System.Diagnostics;
using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets.ViewModels;

public sealed class ColorPickerViewModel : IDisposable
{
	private readonly PaletteManager? palette_manager;
	private readonly ColorPick original_colors;
	private readonly bool live_palette_mode;

	private ColorPickerState state;

	public event EventHandler<EventArgs>? StateChanged;

	public ColorPickerViewModel (
	    ColorPick initialColors,
	    bool primarySelected,
	    PaletteManager? paletteManager = null)
	{
		original_colors = initialColors;
		palette_manager = paletteManager;
		live_palette_mode = (paletteManager != null);

		state = InitializeState (initialColors, primarySelected, live_palette_mode);

		if (live_palette_mode) {
			InitializeLivePaletteHandlers ();
		}
	}

	public ColorPickerState State => state;
	public ColorPick FinalColors => state.Colors;

	#region State Management (UDF)
	private void UpdateState (Func<ColorPickerState, ColorPickerState> transform)
	{
		ColorPickerState newState = transform (state);
		if (newState == state) return;
		state = newState;
		StateChanged?.Invoke (this, EventArgs.Empty);
	}

	private static ColorPickerState InitializeState (ColorPick adjustable, bool primarySelected, bool livePaletteMode)
	{
		// Correctly determine the target based on the context provided by the caller.
		ColorTarget initialTarget = primarySelected ? ColorTarget.Primary : ColorTarget.Secondary;

		// The 'adjustable' parameter can be a SingleColor record that is *intended* for the secondary slot.
		// The UI in SingleColor mode doesn't allow switching, so this initialTarget is the single source of truth for the session.
		// The previous check that forced the target to Primary in SingleColor mode was the source of the bug.

		return new ColorPickerState (
		    Colors: adjustable,
		    ActiveTarget: initialTarget, // This is now correctly set to Primary or Secondary from the start.
		    SurfaceType: ColorSurfaceType.HueAndSat,
		    IsSmallMode: false,
		    ShowValueOnHueSat: true,
		    ShowSwatches: !livePaletteMode
		);
	}
	#endregion

	#region Public Commands
	public void SetCurrentColor (Color color) => UpdateState (s => s.WithCurrentColor (color));
	public void SetColorFromHex (string hex)
	{
		if (Color.FromHex (hex) is { } parsedColor)
			SetCurrentColor (parsedColor);
	}
	public void SwapColors () => UpdateState (s => s.SwapColors ());
	public void ResetColors () => UpdateState (s => s with { Colors = original_colors });
	public void ToggleSmallMode () => UpdateState (s => s with { IsSmallMode = !s.IsSmallMode });
	public void SetActiveTarget (ColorTarget target)
	{
		// In SingleColor mode, the ActiveTarget is determined at creation and is immutable for the dialog's lifetime.
		// The UI only shows one swatch, so there's nothing for the user to switch to. This prevents any change.
		if (state.Colors is SingleColor) return;

		UpdateState (s => s with { ActiveTarget = target });
	}
	public void SetSurfaceType (ColorSurfaceType type) => UpdateState (s => s with { SurfaceType = type });
	public void SetShowValueOnHueSat (bool show) => UpdateState (s => s with { ShowValueOnHueSat = show });
	public void SetHue (double hue) => UpdateState (s => s.WithCurrentColor (s.CurrentColor.CopyHsv (hue: hue)));
	public void SetSaturation (double sat) => UpdateState (s => s.WithCurrentColor (s.CurrentColor.CopyHsv (sat: sat / 100.0)));
	public void SetValue (double val) => UpdateState (s => s.WithCurrentColor (s.CurrentColor.CopyHsv (value: val / 100.0)));
	public void SetRed (double r) => UpdateState (s => s.WithCurrentColor (s.CurrentColor with { R = r / 255.0 }));
	public void SetGreen (double g) => UpdateState (s => s.WithCurrentColor (s.CurrentColor with { G = g / 255.0 }));
	public void SetBlue (double b) => UpdateState (s => s.WithCurrentColor (s.CurrentColor with { B = b / 255.0 }));
	public void SetAlpha (double a) => UpdateState (s => s.WithCurrentColor (s.CurrentColor with { A = a / 255.0 }));
	public void SetColorFromSurfaceInteraction (PointD relativeCursor)
	{
		int radius = state.Layout.PickerSurfaceRadius;
		int diameter = radius * 2;

		if (state.SurfaceType == ColorSurfaceType.HueAndSat) {
			PointD center = new (radius, radius);
			PointD vecCursor = relativeCursor - center;
			double hue = (Math.Atan2 (vecCursor.Y, -vecCursor.X) + Math.PI) / (2.0 * Math.PI) * 360.0;
			double sat = Math.Min (vecCursor.Magnitude () / radius, 1.0);
			UpdateState (s => s.WithCurrentColor (s.CurrentColor.CopyHsv (hue: hue, sat: sat)));
		} else if (state.SurfaceType == ColorSurfaceType.SatAndVal) {
			double clampedX = Math.Clamp (relativeCursor.X, 0, diameter - 1);
			double clampedY = Math.Clamp (relativeCursor.Y, 0, diameter - 1);
			double sat = 1.0 - clampedY / (diameter - 1);
			double val = clampedX / (diameter - 1);
			UpdateState (s => s.WithCurrentColor (s.CurrentColor.CopyHsv (sat: sat, value: val)));
		}
	}
	#endregion

	#region Live Palette Management
	private void InitializeLivePaletteHandlers ()
	{
		if (palette_manager == null) return;
		palette_manager.PrimaryColorChanged += HandleExternalPrimaryColorChanged;
		palette_manager.SecondaryColorChanged += HandleExternalSecondaryColorChanged;
	}

	public void CommitFinalChangesToPalette ()
	{
		if (palette_manager == null) return;

		switch (state.Colors) {
			case SingleColor singleColor:
				// When editing a single color, it could be for either the primary or secondary slot,
				// depending on how the dialog was opened. `ActiveTarget` now correctly reflects this.
				if (state.ActiveTarget == ColorTarget.Primary) {
					if (palette_manager.PrimaryColor != singleColor.Color)
						palette_manager.PrimaryColor = singleColor.Color;
				} else { // ColorTarget.Secondary
					if (palette_manager.SecondaryColor != singleColor.Color)
						palette_manager.SecondaryColor = singleColor.Color;
				}
				break;

			case PaletteColors paletteColors:
				if (palette_manager.PrimaryColor != paletteColors.Primary)
					palette_manager.PrimaryColor = paletteColors.Primary;
				if (palette_manager.SecondaryColor != paletteColors.Secondary)
					palette_manager.SecondaryColor = paletteColors.Secondary;
				break;
		}
	}

	private void HandleExternalPrimaryColorChanged (object? sender, EventArgs _)
	{
		var newPrimary = ((PaletteManager) sender!).PrimaryColor;
		UpdateState (s => s.Colors switch {
			SingleColor sc => s.ActiveTarget == ColorTarget.Primary ? s with { Colors = sc with { Color = newPrimary } } : s,
			PaletteColors pc => s with { Colors = pc with { Primary = newPrimary } },
			_ => throw new UnreachableException (),
		});
	}

	private void HandleExternalSecondaryColorChanged (object? sender, EventArgs _)
	{
		var newSecondary = ((PaletteManager) sender!).SecondaryColor;
		UpdateState (s => s.Colors switch {
			SingleColor sc => s.ActiveTarget == ColorTarget.Secondary ? s with { Colors = sc with { Color = newSecondary } } : s,
			PaletteColors pc => s with { Colors = pc with { Secondary = newSecondary } },
			_ => throw new UnreachableException (),
		});
	}

	public void Dispose ()
	{
		if (palette_manager != null) {
			palette_manager.PrimaryColorChanged -= HandleExternalPrimaryColorChanged;
			palette_manager.SecondaryColorChanged -= HandleExternalSecondaryColorChanged;
		}
	}
	#endregion
}

