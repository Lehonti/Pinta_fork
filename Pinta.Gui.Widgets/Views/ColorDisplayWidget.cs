using System;
using System.Collections.Immutable;
using Cairo;
using Pinta.Core;

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

		ImmutableArray<Gtk.DrawingArea> colorDisplays = CreateColorDisplays (initialState.Colors, initialLayout.PaletteDisplaySize);

		Gtk.ListBox colorDisplayList = new ();
		foreach (var colorDisplay in colorDisplays)
			colorDisplayList.Append (colorDisplay);
		colorDisplayList.SetSelectionMode (Gtk.SelectionMode.Single);

		int initialIndex = initialState.ActiveTarget == ColorTarget.Primary ? 0 : 1;

		if (initialIndex < colorDisplays.Length)
			colorDisplayList.SelectRow (colorDisplayList.GetRowAtIndex (initialIndex));

		colorDisplayList.OnRowSelected += OnColorDisplayRowSelected;

		if (initialState.Colors is PaletteColors) {
			string label = Translations.GetString ("Click to switch between primary and secondary color.");
			string shortcutLabel = Translations.GetString ("Shortcut key");
			Gtk.Button colorDisplaySwap = new () { TooltipText = $"{label} {shortcutLabel}: {"X"}" };
			colorDisplaySwap.SetIconName (Resources.StandardIcons.EditSwap);
			colorDisplaySwap.OnClicked += (sender, args) => viewModel.SwapColors ();
			Append (colorDisplaySwap);
		}

		// --- Initialization (Gtk.Box)

		SetOrientation (Gtk.Orientation.Vertical);
		Spacing = initialLayout.Spacing;
		Append (colorDisplayList);

		// --- References to keep

		color_displays = colorDisplays;
		color_display_list = colorDisplayList;

		viewModel.StateChanged += OnViewModelStateChanged;
	}

	private void OnViewModelStateChanged (object? sender, EventArgs e)
	{
		UpdateView (view_model.State);
	}

	private void UpdateView (ColorPickerState state)
	{
		LayoutSettings layout = state.Layout;

		Spacing = layout.Spacing;

		int displaySize = layout.PaletteDisplaySize;

		foreach (var display in color_displays)
			display.SetSizeRequest (displaySize, displaySize);

		int expectedIndex = state.ActiveTarget == ColorTarget.Primary ? 0 : 1;
		if (expectedIndex < color_displays.Length) {
			var selectedRow = color_display_list.GetSelectedRow ();
			if (selectedRow == null || selectedRow.GetIndex () != expectedIndex) {
				color_display_list.SelectRow (color_display_list.GetRowAtIndex (expectedIndex));
			}
		}

		foreach (var display in color_displays)
			display.QueueDraw ();
	}

	private void OnColorDisplayRowSelected (object? sender, Gtk.ListBox.RowSelectedSignalArgs args)
	{
		int colorIndex = args.Row?.GetIndex () ?? 0;
		ColorTarget target = ColorTargetExtensions.FromIndex (colorIndex);
		if (view_model.State.ActiveTarget == target) return;
		view_model.SetActiveTarget (target);
	}

	private ImmutableArray<Gtk.DrawingArea> CreateColorDisplays (ColorPick pick, int initialSize)
	{
		Gtk.DrawingArea CreateDisplay (Func<ColorPickerState, Color> colorSelector)
		{
			Gtk.DrawingArea display = new ();
			display.SetSizeRequest (initialSize, initialSize);
			display.SetDrawFunc ((area, context, width, height) => {
				Color color = colorSelector (view_model.State);
				DrawPaletteDisplay (context, color, view_model.State.Layout.PaletteDisplaySize);
			});
			return display;
		}

		return pick switch {
			SingleColor => [CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Primary))],
			PaletteColors => [
				CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Primary)),
				CreateDisplay (state => state.Colors.GetTargetedColor (ColorTarget.Secondary)),
			],
			_ => throw new System.Diagnostics.UnreachableException (),
		};
	}

	private static void DrawPaletteDisplay (Context g, Color c, int displaySize)
	{
		const int BORDER = LayoutSettings.PALETTE_DISPLAY_BORDER_THICKNESS;
		int xy = BORDER;
		int wh = displaySize - BORDER * 2;

		g.Antialias = Antialias.None;

		// Draw checker pattern if color is transparent

		if (c.A < 1) {

			g.FillRectangle (
				new RectangleD (xy, xy, wh, wh),
				Color.White);

			Color gray = new (.8, .8, .8);

			g.FillRectangle (
				new RectangleD (xy, xy, wh / 2, wh / 2),
				gray);

			g.FillRectangle (
				new RectangleD (xy + wh / 2, xy + wh / 2, wh / 2, wh / 2),
				gray);
		}

		g.FillRectangle (
			new RectangleD (xy, xy, wh, wh),
			c);

		g.DrawRectangle (
			new RectangleD (xy, xy, wh, wh),
			new Color (0, 0, 0), BORDER);
	}
}
