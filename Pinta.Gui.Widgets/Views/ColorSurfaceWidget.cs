using System;
using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

public sealed class ColorSurfaceWidget : Gtk.Box
{
	private readonly ColorPickerViewModel view_model;

	private readonly Gtk.Box picker_surface_selector_box;
	private readonly Gtk.Overlay picker_surface_overlay;
	private readonly Gtk.DrawingArea picker_surface;
	private readonly Gtk.DrawingArea picker_surface_cursor;
	private readonly Gtk.CheckButton picker_surface_option_draw_value;

	private bool mouse_dragging = false;

	public ColorSurfaceWidget (ColorPickerViewModel viewModel)
	{
		view_model = viewModel;
		ColorPickerState initialState = viewModel.State;
		LayoutSettings initialLayout = initialState.Layout;
		int drawSize = initialLayout.PickerSurfaceDrawSize;

		// --- Selector (Hue/Sat vs Sat/Val) ---

		Gtk.CheckButton pickerSurfaceOptionDrawValue = new () {
			Active = initialState.ShowValueOnHueSat,
			Label = Translations.GetString ("Show Value"),
		};
		// Initial visibility depends on surface type
		pickerSurfaceOptionDrawValue.SetVisible (initialState.SurfaceType == ColorSurfaceType.HueAndSat);
		pickerSurfaceOptionDrawValue.OnToggled += (o, e) => view_model.SetShowValueOnHueSat (pickerSurfaceOptionDrawValue.Active);

		Gtk.ToggleButton pickerSurfaceSatVal = Gtk.ToggleButton.NewWithLabel (Translations.GetString ("Sat & Value"));
		pickerSurfaceSatVal.Active = initialState.SurfaceType == ColorSurfaceType.SatAndVal;
		pickerSurfaceSatVal.OnToggled += (_, _) => {
			if (pickerSurfaceSatVal.Active)
				view_model.SetSurfaceType (ColorSurfaceType.SatAndVal);
		};

		Gtk.ToggleButton pickerSurfaceHueSat = Gtk.ToggleButton.NewWithLabel (Translations.GetString ("Hue & Sat"));
		pickerSurfaceHueSat.Active = initialState.SurfaceType == ColorSurfaceType.HueAndSat;
		pickerSurfaceHueSat.OnToggled += (_, _) => {
			if (pickerSurfaceHueSat.Active)
				view_model.SetSurfaceType (ColorSurfaceType.HueAndSat);
		};
		pickerSurfaceHueSat.SetGroup (pickerSurfaceSatVal); // Group them so only one is active

		Gtk.Box pickerSurfaceSelectorBox = new Gtk.Box {
			Spacing = initialLayout.Spacing,
			WidthRequest = drawSize,
			Homogeneous = true,
			Halign = Gtk.Align.Center,
		};
		// Set initial orientation based on initial mode (Horizontal for Big, Vertical for Small)
		pickerSurfaceSelectorBox.SetOrientation (initialState.IsSmallMode ? Gtk.Orientation.Vertical : Gtk.Orientation.Horizontal);
		pickerSurfaceSelectorBox.Append (pickerSurfaceHueSat);
		pickerSurfaceSelectorBox.Append (pickerSurfaceSatVal);

		// --- Surface and Cursor ---

		Gtk.DrawingArea pickerSurface = new ();
		pickerSurface.SetSizeRequest (drawSize, drawSize);
		pickerSurface.SetDrawFunc ((area, context, width, height) => DrawColorSurface (context));

		Gtk.DrawingArea pickerSurfaceCursor = new ();
		pickerSurfaceCursor.SetSizeRequest (drawSize, drawSize);
		pickerSurfaceCursor.SetDrawFunc (DrawCursor);

		// Overlay the cursor on top of the surface
		Gtk.Overlay pickerSurfaceOverlay = new ();
		pickerSurfaceOverlay.AddOverlay (pickerSurface);
		pickerSurfaceOverlay.AddOverlay (pickerSurfaceCursor);
		pickerSurfaceOverlay.SetSizeRequest (drawSize, drawSize);

		// --- Interaction Handling ---

		Gtk.GestureClick click_gesture = Gtk.GestureClick.New ();
		click_gesture.SetButton (0); // Listen for all mouse buttons.
		click_gesture.OnPressed += OnSurfacePressed;
		click_gesture.OnReleased += OnSurfaceReleased;
		pickerSurfaceOverlay.AddController (click_gesture);

		// Use EventControllerMotion for dragging, matching the original implementation's mechanism.
		Gtk.EventControllerMotion motion_controller = Gtk.EventControllerMotion.New ();
		motion_controller.OnMotion += OnSurfaceMotion;
		pickerSurfaceOverlay.AddController (motion_controller);

		// --- Layout (Gtk.Box) ---

		Spacing = initialLayout.Spacing;
		WidthRequest = drawSize;
		SetOrientation (Gtk.Orientation.Vertical);
		Append (pickerSurfaceSelectorBox);
		Append (pickerSurfaceOverlay);
		Append (pickerSurfaceOptionDrawValue);

		// --- References ---
		picker_surface_selector_box = pickerSurfaceSelectorBox;
		picker_surface_overlay = pickerSurfaceOverlay;
		picker_surface = pickerSurface;
		picker_surface_cursor = pickerSurfaceCursor;
		picker_surface_option_draw_value = pickerSurfaceOptionDrawValue;

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
		int drawSize = layout.PickerSurfaceDrawSize;

		// Update Layout/Size
		Spacing = layout.Spacing;
		WidthRequest = drawSize;

		picker_surface_selector_box.Spacing = layout.Spacing;
		picker_surface_selector_box.WidthRequest = drawSize;
		// Orientation change based on Small Mode
		picker_surface_selector_box.SetOrientation (state.IsSmallMode ? Gtk.Orientation.Vertical : Gtk.Orientation.Horizontal);

		picker_surface.SetSizeRequest (drawSize, drawSize);
		picker_surface_cursor.SetSizeRequest (drawSize, drawSize);
		picker_surface_overlay.SetSizeRequest (drawSize, drawSize);

		// Update Visibility of "Show Value" option based on SurfaceType
		bool showValueVisible = state.SurfaceType == ColorSurfaceType.HueAndSat;
		if (picker_surface_option_draw_value.Visible != showValueVisible) {
			picker_surface_option_draw_value.SetVisible (showValueVisible);
		}

		// Ensure the option's active state matches the VM state
		if (picker_surface_option_draw_value.Active != state.ShowValueOnHueSat) {
			picker_surface_option_draw_value.Active = state.ShowValueOnHueSat;
		}

		// Redraw
		picker_surface.QueueDraw ();
		picker_surface_cursor.QueueDraw ();
	}

	// --- Interaction Logic (View -> ViewModel) ---

	private void HandleSurfaceInteraction (double widgetX, double widgetY)
	{
		// Convert widget coordinates (relative to the overlay) to surface coordinates (relative to the top-left of the drawn surface, excluding padding)
		int padding = LayoutSettings.PICKER_SURFACE_PADDING;

		PointI cursor = new (
			X: (int) (widgetX - padding),
			Y: (int) (widgetY - padding));

		view_model.SetColorFromPickerSurface (cursor);
	}

	private void OnSurfacePressed (Gtk.GestureClick gesture, Gtk.GestureClick.PressedSignalArgs args)
	{
		mouse_dragging = true;
		HandleSurfaceInteraction (args.X, args.Y);
	}

	private void OnSurfaceReleased (Gtk.GestureClick gesture, Gtk.GestureClick.ReleasedSignalArgs args)
	{
		mouse_dragging = false;
	}

	private void OnSurfaceMotion (Gtk.EventControllerMotion controller, Gtk.EventControllerMotion.MotionSignalArgs args)
	{
		if (!mouse_dragging) return;
		HandleSurfaceInteraction (args.X, args.Y);
	}

	// --- Drawing Logic ---

	private void DrawCursor (Gtk.DrawingArea area, Context context, int width, int height)
	{
		ColorPickerState state = view_model.State;
		int padding = LayoutSettings.PICKER_SURFACE_PADDING;
		int radius = state.Layout.PickerSurfaceRadius;

		// Get location relative to the center of the surface (excluding padding)
		PointD locBase = view_model.HsvToPickerLocation (state.CurrentColor.ToHsv ());

		// Convert to drawing area coordinates (including padding)
		PointD loc = new (locBase.X + radius + padding, locBase.Y + radius + padding);

		context.Antialias = Antialias.None;

		// Draw the cursor (a square with contrasting borders)
		RectangleD cursorRect = new (loc.X - 5, loc.Y - 5, 10, 10);

		// Fill with the current color
		context.FillRectangle (cursorRect, state.CurrentColor);

		// Outer border (Black)
		context.DrawRectangle (cursorRect, new Color (0, 0, 0), 4);

		// Inner border (White)
		context.DrawRectangle (cursorRect, new Color (1, 1, 1), 1);
	}

	private void DrawColorSurface (Context g)
	{
		ColorPickerState state = view_model.State;
		int radius = state.Layout.PickerSurfaceRadius;
		int diameter = 2 * radius;
		Size drawSize = new (diameter, diameter);
		int padding = LayoutSettings.PICKER_SURFACE_PADDING;

		// Create an image surface to draw the color gradients pixel by pixel.
		using ImageSurface surface = CairoExtensions.CreateImageSurface (
			Format.Argb32,
			drawSize.Width,
			drawSize.Height);

		Span<ColorBgra> data = surface.GetPixelData ();

		switch (state.SurfaceType) {
			case ColorSurfaceType.HueAndSat:
				DrawHueSatSurface (data, drawSize, radius, state);
				break;

			case ColorSurfaceType.SatAndVal:
				DrawSatValSurface (data, drawSize, state);
				break;

			default:
				throw new System.Diagnostics.UnreachableException ();
		}

		surface.MarkDirty ();
		// Position the generated surface onto the drawing area context, respecting padding.
		g.SetSourceSurface (surface, padding, padding);
		g.Paint ();
	}

	private static void DrawHueSatSurface (Span<ColorBgra> data, Size drawSize, int radius, ColorPickerState state)
	{
		int radiusSquared = radius * radius;
		PointI center = new (radius, radius);
		double currentValue = state.CurrentColor.ToHsv ().Val;
		// Use the current color's value if the option is enabled, otherwise use 1 (full brightness).
		double v = state.ShowValueOnHueSat ? currentValue : 1;

		for (int y = 0; y < drawSize.Height; y++) {
			for (int x = 0; x < drawSize.Width; x++) {
				PointI pixel = new (x, y);
				PointI vector = pixel - center;

				int magnitudeSquared = vector.MagnitudeSquared ();

				// Skip pixels outside the circle
				if (magnitudeSquared > radiusSquared) continue;

				double magnitude = Math.Sqrt (magnitudeSquared);

				// Calculate Hue (angle) and Saturation (distance from center)
				// Matches original: (MathF.Atan2 (vector.Y, -vector.X) + MathF.PI) / (2f * MathF.PI) * 360f;
				double h = (Math.Atan2 (vector.Y, -vector.X) + Math.PI) / (2.0 * Math.PI) * 360.0;
				double s = Math.Min (magnitude / radius, 1);

				// Alpha blending near the edge for antialiasing the circle boundary
				double d = radius - magnitude;
				double a = d < 1 ? d : 1;

				Color c = Color.FromHsv (h, s, v, a);
				data[drawSize.Width * y + x] = c.ToColorBgra ();
			}
		}
	}

	private static void DrawSatValSurface (Span<ColorBgra> data, Size drawSize, ColorPickerState state)
	{
		double currentHue = state.CurrentColor.ToHsv ().Hue;

		for (int y = 0; y < drawSize.Height; y++) {
			// Saturation: Varies vertically (1 at top, 0 at bottom)
			double s = 1.0 - (double) y / (drawSize.Height - 1);
			for (int x = 0; x < drawSize.Width; x++) {
				// Value: Varies horizontally (0 at left, 1 at right)
				double v = (double) x / (drawSize.Width - 1);
				Color c = Color.FromHsv (currentHue, s, v);
				data[drawSize.Width * y + x] = c.ToColorBgra ();
			}
		}
	}
}
