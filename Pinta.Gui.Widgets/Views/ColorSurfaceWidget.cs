using System;
using System.Diagnostics;
using Cairo;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;

namespace Pinta.Gui.Widgets.Views;

public sealed class ColorSurfaceWidget : Box, IDisposable
{
	private readonly ColorPickerViewModel view_model;

	private readonly Box selector_box;
	private readonly DrawingArea surface;
	private readonly DrawingArea cursor;
	private readonly Overlay overlay;
	private readonly ToggleButton hue_sat_button;
	private readonly ToggleButton sat_val_button;
	private readonly CheckButton draw_value_option;

	private bool mouse_pressed = false;

	public ColorSurfaceWidget (ColorPickerViewModel viewModel)
	{
		SetOrientation (Orientation.Vertical);

		// --- Initialization (as local variables) ---
		var vm = viewModel;

		var drawValueOption = new CheckButton { Label = Translations.GetString ("Show Value") };
		var hueSatButton = new ToggleButton { Label = Translations.GetString ("Hue & Sat") };
		var satValButton = new ToggleButton { Label = Translations.GetString ("Sat & Value") };
		hueSatButton.SetGroup (satValButton);

		var selectorBox = new Box {
			Homogeneous = true,
			Halign = Align.Center
		};
		selectorBox.Append (hueSatButton);
		selectorBox.Append (satValButton);

		var surface = new DrawingArea ();
		surface.SetDrawFunc ((_, context, _, _) => DrawColorSurface (context));

		var cursor = new DrawingArea ();
		cursor.SetDrawFunc ((_, context, _, _) => DrawColorSurfaceCursor (context));

		var overlay = new Overlay ();
		overlay.AddOverlay (surface);
		overlay.AddOverlay (cursor);

		// --- Compose Layout ---
		Append (selectorBox);
		Append (overlay);
		Append (drawValueOption);

		// --- Assign to Fields ---
		view_model = vm;
		selector_box = selectorBox;
		this.surface = surface;
		this.cursor = cursor;
		this.overlay = overlay;
		hue_sat_button = hueSatButton;
		sat_val_button = satValButton;
		draw_value_option = drawValueOption;

		// --- Final Setup & Subscriptions ---
		InitializeInteractionControllers ();
		Render (vm.State);

		view_model.StateChanged += HandleStateChanged;
		draw_value_option.OnToggled += HandleDrawValueToggled;
		hue_sat_button.OnToggled += HandleHueSatToggled;
		sat_val_button.OnToggled += HandleSatValToggled;
	}

	public override void Dispose ()
	{
		view_model.StateChanged -= HandleStateChanged;
		draw_value_option.OnToggled -= HandleDrawValueToggled;
		hue_sat_button.OnToggled -= HandleHueSatToggled;
		sat_val_button.OnToggled -= HandleSatValToggled;
	}

	private void HandleStateChanged (object? sender, EventArgs e) => Render (view_model.State);
	private void HandleDrawValueToggled (CheckButton sender, EventArgs e) => view_model.SetShowValueOnHueSat (sender.Active);
	private void HandleHueSatToggled (ToggleButton sender, EventArgs e)
	{
		if (sender.Active) view_model.SetSurfaceType (ColorSurfaceType.HueAndSat);
	}
	private void HandleSatValToggled (ToggleButton sender, EventArgs e)
	{
		if (sender.Active) view_model.SetSurfaceType (ColorSurfaceType.SatAndVal);
	}

	private void Render (ColorPickerState state)
	{
		var layout = state.Layout;
		int pickerDrawSize = layout.PickerSurfaceDrawSize;

		Spacing = layout.Spacing;
		WidthRequest = pickerDrawSize;

		selector_box.WidthRequest = pickerDrawSize;
		selector_box.Spacing = layout.Spacing;
		selector_box.SetOrientation (state.IsSmallMode ? Orientation.Vertical : Orientation.Horizontal);

		surface.SetSizeRequest (pickerDrawSize, pickerDrawSize);
		cursor.SetSizeRequest (pickerDrawSize, pickerDrawSize);
		overlay.SetSizeRequest (pickerDrawSize, pickerDrawSize);

		bool isHueSat = state.SurfaceType == ColorSurfaceType.HueAndSat;
		if (hue_sat_button.Active != isHueSat) hue_sat_button.Active = isHueSat;
		if (sat_val_button.Active != !isHueSat) sat_val_button.Active = !isHueSat;

		draw_value_option.Visible = isHueSat;
		if (draw_value_option.Active != state.ShowValueOnHueSat)
			draw_value_option.Active = state.ShowValueOnHueSat;

		surface.QueueDraw ();
		cursor.QueueDraw ();
	}

	#region Interaction
	private void InitializeInteractionControllers ()
	{
		var click = Gtk.GestureClick.New ();
		click.SetButton (0);
		click.OnPressed += (_, args) => {
			mouse_pressed = true;
			UpdateColorFromPosition (args.X, args.Y);
		};
		click.OnReleased += (_, _) => mouse_pressed = false;

		var motion = Gtk.EventControllerMotion.New ();
		motion.OnMotion += (_, args) => {
			if (mouse_pressed)
				UpdateColorFromPosition (args.X, args.Y);
		};

		overlay.AddController (click);
		overlay.AddController (motion);
	}

	private void UpdateColorFromPosition (double eventX, double eventY)
	{
		// The event coordinates are relative to the 'overlay' widget.
		// The actual drawing is padded inside the 'surface' widget, which is a child of the overlay.
		// We subtract the padding to get coordinates relative to the drawn content (the wheel/square).
		view_model.SetColorFromSurfaceInteraction (new PointD (
		    eventX - LayoutSettings.PICKER_SURFACE_PADDING,
		    eventY - LayoutSettings.PICKER_SURFACE_PADDING
		));
	}
	#endregion

	#region Drawing
	private void DrawColorSurfaceCursor (Context context)
	{
		var state = view_model.State;
		var layout = state.Layout;
		int radius = layout.PickerSurfaceRadius;

		PointD locBase = HsvToPickerLocation (state.CurrentColor.ToHsv (), radius, state.SurfaceType);
		PointD loc = new (
		    locBase.X + radius + LayoutSettings.PICKER_SURFACE_PADDING,
		    locBase.Y + radius + LayoutSettings.PICKER_SURFACE_PADDING
		);

		RectangleD cursorRect = new (loc.X - 5, loc.Y - 5, 10, 10);

		context.Antialias = Antialias.None;
		context.FillRectangle (cursorRect, state.CurrentColor);
		context.DrawRectangle (cursorRect, new Color (0, 0, 0), 4);
		context.DrawRectangle (cursorRect, new Color (1, 1, 1), 1);
	}

	private void DrawColorSurface (Context g)
	{
		var state = view_model.State;
		var layout = state.Layout;
		int radius = layout.PickerSurfaceRadius;
		int diameter = 2 * radius;
		Size drawSize = new (diameter, diameter);

		using var surface = CairoExtensions.CreateImageSurface (Format.Argb32, drawSize.Width, drawSize.Height);
		var data = surface.GetPixelData ();
		var currentHsv = state.CurrentColor.ToHsv ();

		switch (state.SurfaceType) {
			case ColorSurfaceType.HueAndSat:
				RenderHueSatSurface (data, drawSize, radius, currentHsv.Val, state.ShowValueOnHueSat);
				break;
			case ColorSurfaceType.SatAndVal:
				RenderSatValSurface (data, drawSize, currentHsv.Hue);
				break;
			default:
				throw new UnreachableException ();
		}

		surface.MarkDirty ();
		g.SetSourceSurface (surface, LayoutSettings.PICKER_SURFACE_PADDING, LayoutSettings.PICKER_SURFACE_PADDING);
		g.Paint ();
	}
	#endregion

	#region Pure Utilities
	private static PointD HsvToPickerLocation (HsvColor hsv, int radius, ColorSurfaceType surfaceType)
	{
		switch (surfaceType) {
			case ColorSurfaceType.HueAndSat: {
					double angleRad = (hsv.Hue / 360.0) * (2.0 * Math.PI);
					double angleAtan2 = angleRad - Math.PI;
					double mag = hsv.Sat * radius;
					double x = -Math.Cos (angleAtan2) * mag;
					double y = Math.Sin (angleAtan2) * mag;
					return new PointD (x, y);
				}

			case ColorSurfaceType.SatAndVal: {
					int size = radius * 2;
					double x = hsv.Val * (size - 1);
					double y = (1.0 - hsv.Sat) * (size - 1);
					return new PointD (x - radius, y - radius);
				}

			default:
				throw new UnreachableException ();
		}
		;
	}

	private static void RenderHueSatSurface (Span<ColorBgra> data, Size drawSize, int radius, double currentValue, bool showValue)
	{
		int radiusSquared = radius * radius;
		PointI center = new (radius, radius);
		double v = showValue ? currentValue : 1.0;

		for (int y = 0; y < drawSize.Height; y++) {
			for (int x = 0; x < drawSize.Width; x++) {
				PointI vector = new PointI (x, y) - center;
				int magnitudeSquared = vector.MagnitudeSquared ();

				if (magnitudeSquared > radiusSquared) continue;

				double magnitude = Math.Sqrt (magnitudeSquared);
				double h = (Math.Atan2 (vector.Y, -vector.X) + Math.PI) / (2.0 * Math.PI) * 360.0;
				double s = Math.Min (magnitude / radius, 1.0);
				double d = radius - magnitude;
				double a = d < 1.0 ? d : 1.0;

				Color c = Color.FromHsv (h, s, v, a);
				data[drawSize.Width * y + x] = c.ToColorBgra ();
			}
		}
	}

	private static void RenderSatValSurface (Span<ColorBgra> data, Size drawSize, double currentHue)
	{
		for (int y = 0; y < drawSize.Height; y++) {
			double s = 1.0 - (double) y / (drawSize.Height - 1);
			for (int x = 0; x < drawSize.Width; x++) {
				double v = (double) x / (drawSize.Width - 1);
				Color c = Color.FromHsv (currentHue, s, v);
				data[drawSize.Width * y + x] = c.ToColorBgra ();
			}
		}
	}
	#endregion
}

