using System;
using System.Linq;
using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

public sealed class ColorSwatchesWidget : Gtk.Box
{
	private readonly ColorPickerViewModel view_model;
	private readonly PaletteManager palette_manager;

	public ColorSwatchesWidget (ColorPickerViewModel viewModel, PaletteManager paletteManager)
	{
		const int SWATCH_WIDTH_REQUEST = 500;

		int swatchHeight = PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS;

		Gtk.GestureClick recentGesture = Gtk.GestureClick.New ();
		recentGesture.SetButton (0);
		recentGesture.OnPressed += (gesture, args) => {
			viewModel.SetColorFromRecentSwatches (new PointD (args.X, args.Y));
		};

		Gtk.GestureClick paletteGesture = Gtk.GestureClick.New ();
		paletteGesture.SetButton (0);
		paletteGesture.OnPressed += (gesture, args) => {
			viewModel.SetColorFromPaletteSwatches (new PointD (args.X, args.Y));
		};

		Gtk.DrawingArea swatchRecent = new () {
			WidthRequest = SWATCH_WIDTH_REQUEST,
			HeightRequest = swatchHeight,
		};
		swatchRecent.SetDrawFunc (DrawRecentSwatches);
		swatchRecent.AddController (recentGesture);

		Gtk.DrawingArea swatchPalette = new () {
			WidthRequest = SWATCH_WIDTH_REQUEST,
			HeightRequest = swatchHeight,
		};
		swatchPalette.SetDrawFunc (DrawPaletteSwatches);
		swatchPalette.AddController (paletteGesture);

		// --- Initialization (Gtk.Box)

		SetOrientation (Gtk.Orientation.Vertical);

		Spacing = viewModel.State.Layout.Spacing;

		Append (swatchRecent);
		Append (swatchPalette);

		// --- References to keep

		view_model = viewModel;
		palette_manager = paletteManager;

		// --- Event Subscription ---

		viewModel.StateChanged += OnViewModelStateChanged;
	}

	private void OnViewModelStateChanged (object? sender, EventArgs e)
	{
		Spacing = view_model.State.Layout.Spacing;
	}

	private void DrawRecentSwatches (Gtk.DrawingArea area, Context g, int width, int height)
	{
		var recent = palette_manager.RecentlyUsedColors;
		int recentColors = palette_manager.MaxRecentlyUsedColor / PaletteWidget.PALETTE_ROWS;

		RectangleD recentPaletteBounds = new (
			0,
			0,
			PaletteWidget.SWATCH_SIZE * recentColors,
			PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS);

		for (int i = 0; i < recent.Count; i++) {

			RectangleD swatchBounds = PaletteWidget.GetSwatchBounds (
				palette_manager,
				i,
				recentPaletteBounds,
				true);

			g.FillRectangle (
				swatchBounds,
				recent.ElementAt (i));
		}
	}

	private void DrawPaletteSwatches (Gtk.DrawingArea area, Context g, int width, int height)
	{
		RectangleD paletteRect = new (
			0,
			0,
			width - PaletteWidget.PALETTE_MARGIN,
			PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS);

		Palette currentPalette = palette_manager.CurrentPalette;

		for (int i = 0; i < currentPalette.Colors.Count; i++) {
			RectangleD swatchBounds = PaletteWidget.GetSwatchBounds (palette_manager, i, paletteRect);
			g.FillRectangle (swatchBounds, currentPalette.Colors[i]);
		}
	}
}
