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
		view_model = viewModel;
		palette_manager = paletteManager;

		const int SWATCH_WIDTH_REQUEST = 500;

		int swatchHeight = PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS;

		Gtk.DrawingArea swatchRecent = new () {
			WidthRequest = SWATCH_WIDTH_REQUEST,
			HeightRequest = swatchHeight,
		};
		swatchRecent.SetDrawFunc (DrawRecentSwatches);

		Gtk.DrawingArea swatchPalette = new () {
			WidthRequest = SWATCH_WIDTH_REQUEST,
			HeightRequest = swatchHeight,
		};
		swatchPalette.SetDrawFunc (DrawPaletteSwatches);

		// --- Interaction Handling ---
		// Set up click handlers on the drawing areas, delegating the logic to the ViewModel.
		SetupInteraction (swatchRecent, view_model.SetColorFromRecentSwatches);
		SetupInteraction (swatchPalette, view_model.SetColorFromPaletteSwatches);

		// --- Layout (Gtk.Box) ---
		SetOrientation (Gtk.Orientation.Vertical);
		Spacing = viewModel.State.Layout.Spacing;
		Append (swatchRecent);
		Append (swatchPalette);

		// --- Event Subscription ---
		view_model.StateChanged += OnViewModelStateChanged;
	}

	private void OnViewModelStateChanged (object? sender, EventArgs e)
	{
		Spacing = view_model.State.Layout.Spacing;
	}

	private static void SetupInteraction (Gtk.DrawingArea area, Action<PointD> onClick)
	{
		Gtk.GestureClick clickGesture = Gtk.GestureClick.New ();
		clickGesture.SetButton (0);
		clickGesture.OnPressed += (gesture, args) => {
			onClick (new PointD (args.X, args.Y));
		};
		area.AddController (clickGesture);
	}

	private void DrawRecentSwatches (Gtk.DrawingArea area, Context g, int width, int height)
	{
		var recent = palette_manager.RecentlyUsedColors;
		int recent_cols = palette_manager.MaxRecentlyUsedColor / PaletteWidget.PALETTE_ROWS;

		RectangleD recent_palette_rect = new (
			0,
			0,
			PaletteWidget.SWATCH_SIZE * recent_cols,
			PaletteWidget.SWATCH_SIZE * PaletteWidget.PALETTE_ROWS);

		for (int i = 0; i < recent.Count; i++) {
			RectangleD swatchBounds = PaletteWidget.GetSwatchBounds (palette_manager, i, recent_palette_rect, true);
			g.FillRectangle (swatchBounds, recent.ElementAt (i));
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
