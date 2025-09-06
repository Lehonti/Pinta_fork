using Pinta.Core;
using Gtk;
using System;
using System.Collections.Immutable;
using Cairo;

namespace Pinta.Gui.Widgets;

public sealed class ColorDisplayWidget : Gtk.Box
{
	private readonly ColorPickerViewModel view_model;
	private readonly ImmutableArray<Gtk.DrawingArea> color_displays;
	private readonly Gtk.ListBox color_display_list;

	public ColorDisplayWidget (ColorPickerViewModel viewModel)
	{
		view_model = viewModel;
		ColorPickerState initialState = viewModel.State;
		LayoutSettings initialLayout = initialState.Layout;

		// --- Color Displays (Drawing Areas) ---
		ImmutableArray<Gtk.DrawingArea> colorDisplays = CreateColorDisplays (initialState.Colors, initialLayout.PaletteDisplaySize);

		// --- ListBox Container ---
		Gtk.ListBox colorDisplayList = new ();
		foreach (var colorDisplay in colorDisplays)
			colorDisplayList.Append (colorDisplay);

		// Set initial selected row
		colorDisplayList.SetSelectionMode (Gtk.SelectionMode.Single);
		int initialIndex = initialState.ActiveTarget == ColorTarget.Primary ? 0 : 1;

		if (initialIndex < colorDisplays.Length) {
			colorDisplayList.SelectRow (colorDisplayList.GetRowAtIndex (initialIndex));
		}

		// Handle selection change
		colorDisplayList.OnRowSelected += OnColorDisplayRowSelected;

		// --- Swap Button (if applicable) ---
		if (initialState.Colors is PaletteColors) {
			string label = Translations.GetString ("Click to switch between primary and secondary color.");
			string shortcut_label = Translations.GetString ("Shortcut key");
			// Shortcut 'X' is handled globally by the ColorPickerDialog
			Gtk.Button colorDisplaySwap = new () { TooltipText = $"{label} {shortcut_label}: {"X"}" };
			colorDisplaySwap.SetIconName (Resources.StandardIcons.EditSwap);
			colorDisplaySwap.OnClicked += (sender, args) => view_model.SwapColors ();
			Append (colorDisplaySwap);
		}

		// --- Layout (Gtk.Box) ---
		SetOrientation (Gtk.Orientation.Vertical);
		Spacing = initialLayout.Spacing;
		Append (colorDisplayList);

		// --- References ---
		color_displays = colorDisplays;
		color_display_list = colorDisplayList;

		// --- Event Subscription ---
		view_model.StateChanged += OnViewModelStateChanged;
	}

	private void OnViewModelStateChanged (object? sender, EventArgs e)
	{
		UpdateView (view_model.State);
	}

	private void UpdateView (ColorPickerState state)
	{
		LayoutSettings layout = state.Layout;

		// Update Layout
		Spacing = layout.Spacing;
		int displaySize = layout.PaletteDisplaySize;

		foreach (var display in color_displays)
			display.SetSizeRequest (displaySize, displaySize);

		// Update selection (if it changed in the model, e.g. via swap)
		int expectedIndex = state.ActiveTarget == ColorTarget.Primary ? 0 : 1;
		if (expectedIndex < color_displays.Length) {
			var selectedRow = color_display_list.GetSelectedRow ();
			if (selectedRow == null || selectedRow.GetIndex () != expectedIndex) {
				color_display_list.SelectRow (color_display_list.GetRowAtIndex (expectedIndex));
			}
		}

		// Redraw displays (the colors might have changed)
		foreach (var display in color_displays)
			display.QueueDraw ();
	}

	private void OnColorDisplayRowSelected (object? sender, Gtk.ListBox.RowSelectedSignalArgs args)
	{
		int colorIndex = args.Row?.GetIndex () ?? 0;
		ColorTarget target = ColorTargetExtensions.FromIndex (colorIndex);
		// Update the ViewModel if the target changed due to user interaction.
		if (view_model.State.ActiveTarget != target) {
			view_model.SetActiveTarget (target);
		}
	}

	// --- Initialization Helpers ---

	private ImmutableArray<Gtk.DrawingArea> CreateColorDisplays (ColorPick pick, int initialSize)
	{
		Gtk.DrawingArea CreateDisplay (Func<ColorPickerState, Color> colorSelector)
		{
			Gtk.DrawingArea display = new ();
			display.SetSizeRequest (initialSize, initialSize);
			display.SetDrawFunc ((area, context, width, height) => {
				// Fetch the current color and layout from the ViewModel state when drawing
				Color color = colorSelector (view_model.State);
				DrawPaletteDisplay (context, color, view_model.State.Layout.PaletteDisplaySize);
			});
			return display;
		}

		return pick switch {
			SingleColor => [CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Primary))], // Use a selector that accesses the color from the current state.
			PaletteColors => [
								CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Primary)),
					CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Secondary))
							],
			_ => throw new System.Diagnostics.UnreachableException (),
		};
	}

	// --- Drawing Logic ---

	private static void DrawPaletteDisplay (Context g, Color c, int displaySize)
	{
		const int BORDER = LayoutSettings.PALETTE_DISPLAY_BORDER_THICKNESS;
		int xy = BORDER;
		int wh = displaySize - BORDER * 2;

		g.Antialias = Antialias.None;

		// Draw checker pattern if color is transparent
		if (c.A < 1) {
			// White background
			g.FillRectangle (
				new RectangleD (xy, xy, wh, wh),
				new Color (1, 1, 1));

			// Gray checkers
			Color gray = new (.8, .8, .8);
			g.FillRectangle (
				new RectangleD (xy, xy, wh / 2, wh / 2),
				gray);
			g.FillRectangle (
				new RectangleD (xy + wh / 2, xy + wh / 2, wh / 2, wh / 2),
				gray);
		}

		// Draw the color itself
		g.FillRectangle (
			new RectangleD (xy, xy, wh, wh),
			c);

		// Draw border
		g.DrawRectangle (
			new RectangleD (xy, xy, wh, wh),
			new Color (0, 0, 0), BORDER);
	}
}
