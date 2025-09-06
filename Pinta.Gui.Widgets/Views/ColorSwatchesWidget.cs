using System;
using System.Collections.Generic;
using Cairo;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;

namespace Pinta.Gui.Widgets.Views;

public sealed class ColorSwatchesWidget : Box, IDisposable
{
	private readonly ColorPickerViewModel view_model;
	private readonly PaletteManager palette_manager;
	private readonly DrawingArea swatch_recent;
	private readonly DrawingArea swatch_palette;

	public ColorSwatchesWidget (ColorPickerViewModel viewModel, PaletteManager paletteManager)
	{
		SetOrientation (Orientation.Vertical);

		// --- Initialization ---
		int height = PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS;

		var recent = new DrawingArea { HeightRequest = height };
		recent.SetDrawFunc (SwatchRecentDraw);

		var palette = new DrawingArea { HeightRequest = height };
		palette.SetDrawFunc (SwatchPaletteDraw);

		// --- Compose Layout ---
		Append (recent);
		Append (palette);

		// --- Assign to Fields ---
		view_model = viewModel;
		palette_manager = paletteManager;
		swatch_recent = recent;
		swatch_palette = palette;

		// --- Final Setup & Subscriptions ---
		InitializeInteractionControllers ();
		Render (viewModel.State);

		view_model.StateChanged += HandleStateChanged;
		palette_manager.PrimaryColorChanged += HandlePaletteOrRecentChanged;
		palette_manager.SecondaryColorChanged += HandlePaletteOrRecentChanged;
		palette_manager.RecentColorsChanged += HandlePaletteOrRecentChanged;
	}

	public override void Dispose ()
	{
		view_model.StateChanged -= HandleStateChanged;
		palette_manager.PrimaryColorChanged -= HandlePaletteOrRecentChanged;
		palette_manager.SecondaryColorChanged -= HandlePaletteOrRecentChanged;
		palette_manager.RecentColorsChanged -= HandlePaletteOrRecentChanged;
	}

	private void HandleStateChanged (object? sender, EventArgs e) => Render (view_model.State);
	private void HandlePaletteOrRecentChanged (object? sender, EventArgs e)
	{
		swatch_recent.QueueDraw ();
		swatch_palette.QueueDraw ();
	}

	private void Render (ColorPickerState state)
	{
		Visible = !state.IsSmallMode && state.ShowSwatches;
		if (!Visible) return;

		Spacing = state.Layout.Spacing;
		swatch_recent.QueueDraw ();
		swatch_palette.QueueDraw ();
	}

	#region Interaction
	private void InitializeInteractionControllers ()
	{
		var click = Gtk.GestureClick.New ();
		click.SetButton (0);
		click.OnPressed += HandleClickPressed;
		AddController (click);
	}

	private void HandleClickPressed (GestureClick gesture, Gtk.GestureClick.PressedSignalArgs args)
	{
		// The click coordinates (args.X, args.Y) are relative to 'this' widget.
		// We translate them to the coordinate space of the child drawing areas.

		// Check if click was in recent colors swatch
		if (TranslateCoordinates (swatch_recent, args.X, args.Y, out double recentX, out double recentY)) {
			if (recentX >= 0 && recentX < swatch_recent.GetWidth () && recentY >= 0 && recentY < swatch_recent.GetHeight ()) {
				HandleSwatchClick ([.. palette_manager.RecentlyUsedColors], new PointD (recentX, recentY), true);
				return;
			}
		}

		// Check if click was in palette swatch
		if (TranslateCoordinates (swatch_palette, args.X, args.Y, out double paletteX, out double paletteY)) {
			if (paletteX >= 0 && paletteX < swatch_palette.GetWidth () && paletteY >= 0 && paletteY < swatch_palette.GetHeight ()) {
				HandleSwatchClick (palette_manager.CurrentPalette.Colors, new PointD (paletteX, paletteY), false);
			}
		}
	}

	private void HandleSwatchClick (IReadOnlyList<Color> colors, PointD relativePos, bool isRecent)
	{
		int index = PaletteWidget.GetSwatchAtLocation (palette_manager, relativePos, new RectangleD (), isRecent);

		if (index >= 0 && index < colors.Count) {
			view_model.SetCurrentColor (colors[index]);
		}
	}
	#endregion

	#region Drawing
	private void SwatchRecentDraw (DrawingArea area, Context g, int width, int height)
	{
		var recent = palette_manager.RecentlyUsedColors;
		int recent_cols = palette_manager.MaxRecentlyUsedColor / PaletteWidget.PALETTE_ROWS;

		RectangleD rect = new (0, 0,
		    PaletteWidget.SWATCH_SIZE * recent_cols,
		    PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS);

		int i = 0;
		foreach (var color in recent) {
			g.FillRectangle (PaletteWidget.GetSwatchBounds (palette_manager, i, rect, true), color);
			i++;
		}
	}

	private void SwatchPaletteDraw (DrawingArea area, Context g, int width, int height)
	{
		RectangleD rect = new (0, 0,
		    width - PaletteWidget.PALETTE_MARGIN,
		    PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS);

		var currentPalette = palette_manager.CurrentPalette;

		for (int i = 0; i < currentPalette.Colors.Count; i++)
			g.FillRectangle (PaletteWidget.GetSwatchBounds (palette_manager, i, rect), currentPalette.Colors[i]);
	}
	#endregion
}

