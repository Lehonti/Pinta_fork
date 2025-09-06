using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Cairo;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;

namespace Pinta.Gui.Widgets.Views;

public sealed class ColorDisplayWidget : Box, IDisposable
{
	private readonly ColorPickerViewModel view_model;
	private readonly ListBox color_display_list;
	private readonly ImmutableArray<DrawingArea> color_displays;
	private readonly Button? swap_button;

	public ColorDisplayWidget (ColorPickerViewModel viewModel)
	{
		SetOrientation (Orientation.Vertical);

		// --- Initialization ---
		var vm = viewModel;
		var displays = CreateColorDisplays (vm.State.Colors);
		var displayList = new ListBox ();
		displayList.SetSelectionMode (SelectionMode.Single);
		foreach (var d in displays)
			displayList.Append (d);

		Button? swapButton = null;
		if (vm.State.Colors is PaletteColors) {
			string label = Translations.GetString ("Click to switch between primary and secondary color.");
			string shortcut_label = Translations.GetString ("Shortcut key");
			swapButton = new Button {
				TooltipText = $"{label} {shortcut_label}: {"X"}",
				IconName = Resources.StandardIcons.EditSwap
			};
			Append (swapButton);
		}
		Append (displayList);

		// --- Assign to Fields ---
		view_model = vm;
		color_displays = displays;
		color_display_list = displayList;
		swap_button = swapButton;

		// --- Final Setup & Subscriptions ---
		Render (vm.State);
		view_model.StateChanged += HandleStateChanged;
		color_display_list.OnRowSelected += HandleRowSelected;
		if (swap_button != null) swap_button.OnClicked += HandleSwapClicked;
	}

	public override void Dispose ()
	{
		view_model.StateChanged -= HandleStateChanged;
		color_display_list.OnRowSelected -= HandleRowSelected;
		if (swap_button != null) swap_button.OnClicked -= HandleSwapClicked;
	}

	private void HandleStateChanged (object? sender, EventArgs e) => Render (view_model.State);
	private void HandleRowSelected (ListBox sender, ListBox.RowSelectedSignalArgs args)
	{
		int colorIndex = args.Row?.GetIndex () ?? 0;
		view_model.SetActiveTarget (ColorTargetExtensions.FromIndex (colorIndex));
	}
	private void HandleSwapClicked (Button sender, EventArgs args) => view_model.SwapColors ();

	private void Render (ColorPickerState state)
	{
		Spacing = state.Layout.Spacing;
		foreach (var display in color_displays) {
			display.SetSizeRequest (state.Layout.PaletteDisplaySize, state.Layout.PaletteDisplaySize);
			display.QueueDraw ();
		}

		int activeIndex = state.ActiveTarget == ColorTarget.Primary ? 0 : 1;
		if (color_display_list.GetRowAtIndex (activeIndex) is { } row && !row.IsSelected ()) {
			color_display_list.SelectRow (row);
		}
	}

	private ImmutableArray<DrawingArea> CreateColorDisplays (ColorPick pick)
	{
		DrawingArea CreateDisplay (ColorTarget target)
		{
			var display = new DrawingArea ();
			display.SetDrawFunc ((_, context, _, _) =>
			    DrawPaletteDisplay (context, view_model.State.Colors.GetTargetedColor (target)));
			return display;
		}

		return pick switch {
			SingleColor => [CreateDisplay (ColorTarget.Primary)],
			PaletteColors => [CreateDisplay (ColorTarget.Primary), CreateDisplay (ColorTarget.Secondary)],
			_ => throw new UnreachableException (),
		};
	}

	private void DrawPaletteDisplay (Context g, Color c)
	{
		var layout = view_model.State.Layout;
		int xy = LayoutSettings.PALETTE_DISPLAY_BORDER_THICKNESS;
		int wh = layout.PaletteDisplaySize - LayoutSettings.PALETTE_DISPLAY_BORDER_THICKNESS * 2;
		RectangleD rect = new (xy, xy, wh, wh);

		g.Antialias = Antialias.None;

		if (c.A < 1.0) {
			g.FillRectangle (rect, new Color (1, 1, 1));
			g.FillRectangle (new RectangleD (xy, xy, wh / 2.0, wh / 2.0), new Color (.8, .8, .8));
			g.FillRectangle (new RectangleD (xy + wh / 2.0, xy + wh / 2.0, wh / 2.0, wh / 2.0), new Color (.8, .8, .8));
		}

		g.FillRectangle (rect, c);
		g.DrawRectangle (rect, new Color (0, 0, 0), LayoutSettings.PALETTE_DISPLAY_BORDER_THICKNESS);
	}
}
