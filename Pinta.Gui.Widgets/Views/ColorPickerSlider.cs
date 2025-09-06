using System;
using System.Globalization;
using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

public sealed class ColorPickerSlider : Gtk.Box
{
	private readonly ColorPickerSliderViewModel view_model;
	private readonly SliderLayoutSettings layout_settings;
	private readonly Gtk.Window parent_window; // Required to check focus state

	private readonly Gtk.Scale slider_control;
	private readonly Gtk.Entry input_field;
	private readonly Gtk.Overlay slider_overlay;
	private readonly Gtk.DrawingArea cursor_area;
	private readonly Gtk.DrawingArea gradient_area;

	// Function to generate the gradient based on the current color state.
	// Initialized by ColorSlidersWidget.
	public Func<Color, ColorGradient<Color>>? GradientGenerator { get; init; }

	// The current color from the main view model, needed for gradient generation.
	private Color current_color_context;

	private bool suppress_input_field_event = false;

	public ColorPickerSlider (ColorPickerSliderViewModel viewModel, SliderLayoutSettings layout, Gtk.Window parentWindow, int initialSliderWidth)
	{
		view_model = viewModel;
		layout_settings = layout;
		parent_window = parentWindow;

		// Use an estimated height during construction based on padding constants.
		int estimatedHeight = layout.PaddingHeight * 2 + 10;

		// --- Widget Creation ---

		Gtk.Scale sliderControl = new () {
			WidthRequest = initialSliderWidth,
			Opacity = 0, // Hide the GTK scale widget visualization, we draw our own
		};
		sliderControl.SetOrientation (Gtk.Orientation.Horizontal);
		// Max + 1 for adjustment upper bound as per original implementation
		sliderControl.SetAdjustment (Gtk.Adjustment.New (0, 0, viewModel.Settings.MaxValue + 1, 1, 1, 1));
		sliderControl.SetValue (viewModel.Value);
		sliderControl.OnValueChanged += OnSliderControlValueChanged;

		Gtk.DrawingArea cursorArea = new ();
		cursorArea.SetSizeRequest (initialSliderWidth, estimatedHeight);
		cursorArea.SetDrawFunc (DrawCursor);

		Gtk.DrawingArea gradientArea = new ();
		gradientArea.SetSizeRequest (initialSliderWidth, estimatedHeight);
		gradientArea.SetDrawFunc (DrawGradient);

		Gtk.Label sliderLabel = new () { WidthRequest = 50 };
		sliderLabel.SetLabel (viewModel.Settings.Label);

		Gtk.Overlay sliderOverlay = new () {
			WidthRequest = initialSliderWidth,
			HeightRequest = estimatedHeight,
		};
		sliderOverlay.AddOverlay (gradientArea);
		sliderOverlay.AddOverlay (cursorArea);
		sliderOverlay.AddOverlay (sliderControl);

		Gtk.Entry inputField = new () {
			MaxWidthChars = viewModel.Settings.MaxWidthChars,
			WidthRequest = 50,
			Hexpand = false,
		};
		inputField.SetText (Convert.ToInt32 (viewModel.Value).ToString (CultureInfo.InvariantCulture));
		inputField.OnChanged += OnInputFieldChanged;

		// --- Initialization (Gtk.Box) ---

		Append (sliderLabel);
		Append (sliderOverlay);
		Append (inputField);

		// --- References to keep ---

		cursor_area = cursorArea;
		gradient_area = gradientArea;
		slider_control = sliderControl;
		slider_overlay = sliderOverlay;
		input_field = inputField;
	}

	// Called by the parent widget (ColorSlidersWidget) when the ViewModel changes or layout changes.
	public void Update (LayoutSettings layout, Color currentColorContext)
	{
		current_color_context = currentColorContext;

		// Update layout
		int sliderWidth = layout.SliderWidth;
		slider_control.WidthRequest = sliderWidth;

		// Use the realized height if available, otherwise use the request height.
		int currentHeight = GetHeight ();
		int height = currentHeight > 0 ? currentHeight : slider_overlay.HeightRequest;

		gradient_area.SetSizeRequest (sliderWidth, height);
		cursor_area.SetSizeRequest (sliderWidth, height);
		slider_overlay.WidthRequest = sliderWidth;

		// Update value visualization
		UpdateValueDisplay (view_model.Value);
	}

	private void UpdateValueDisplay (double val)
	{
		// Update the slider control value
		if (Math.Abs (slider_control.GetValue () - val) > 0.001) {
			slider_control.SetValue (val);
		}

		// Update input field text, unless the user is currently editing it.
		if (parent_window.GetFocus ()?.Parent != input_field) {
			// Prevent OnInputFieldChanged from firing and updating the ViewModel in a loop.
			suppress_input_field_event = true;
			input_field.SetText (Convert.ToInt32 (val).ToString (CultureInfo.InvariantCulture));
			suppress_input_field_event = false;
		}

		// Redraw
		gradient_area.QueueDraw ();
		cursor_area.QueueDraw ();
	}

	// --- Event Handlers (View -> ViewModel) ---

	private void OnSliderControlValueChanged (object? sender, EventArgs e)
	{
		// Triggered by user interaction or programmatically.
		double newValue = slider_control.GetValue ();
		view_model.UpdateValueFromView (newValue);
	}

	private void OnInputFieldChanged (Gtk.Editable inputField, EventArgs e)
	{
		if (suppress_input_field_event)
			return;

		string text = inputField.GetText ();

		bool success = double.TryParse (
			text,
			CultureInfo.InvariantCulture,
			out double parsed);

		if (!success)
			return; // Ignore invalid input

		if (parsed > view_model.Settings.MaxValue) {
			parsed = view_model.Settings.MaxValue;
			// If clamped, update the display immediately. This also suppresses the event during the update.
			UpdateValueDisplay (parsed);
		}

		view_model.UpdateValueFromView (parsed);
	}

	// --- Drawing Logic ---

	private void DrawCursor (Gtk.DrawingArea area, Context context, int width, int height)
	{
		const int OUTLINE_WIDTH = 2;
		int paddingWidth = layout_settings.PaddingWidth;
		double max = view_model.Settings.MaxValue;

		// Calculate the horizontal position of the cursor
		double currentPosition = view_model.Value / max * (width - 2 * paddingWidth) + paddingWidth;

		// Define the shape of the cursor (matching the original implementation)
		ReadOnlySpan<PointD> cursorPoly = [
			new (currentPosition, height / 2),
			new (currentPosition + 4, 3 * height / 4),
			new (currentPosition + 4, height - OUTLINE_WIDTH / 2),
			new (currentPosition - 4, height - OUTLINE_WIDTH / 2),
			new (currentPosition - 4, 3 * height / 4),
			new (currentPosition, height / 2),
		];

		context.LineWidth = OUTLINE_WIDTH;

		// Draw outline (Black)
		context.DrawPolygonal (
			cursorPoly,
			new Color (0, 0, 0),
			LineCap.Butt);

		// Fill interior (White)
		context.FillPolygonal (
			cursorPoly,
			new Color (1, 1, 1));
	}

	private void DrawGradient (Gtk.DrawingArea area, Context context, int width, int height)
	{
		if (GradientGenerator == null) return;

		// Generate the gradient based on the current color context
		ColorGradient<Color> colors = GradientGenerator (current_color_context);

		context.Antialias = Antialias.None;

		int paddingWidth = layout_settings.PaddingWidth;
		int paddingHeight = layout_settings.PaddingHeight;

		Size drawSize = new (
			Width: width - paddingWidth * 2,
			Height: height - paddingHeight * 2);

		PointI p = new (
			X: paddingWidth + drawSize.Width,
			Y: paddingHeight + drawSize.Height);

		// --- Draw transparency background (checkerboard pattern) ---

		int bsize = drawSize.Height / 2;

		// White base
		context.FillRectangle (
			new RectangleD (paddingWidth, paddingHeight, drawSize.Width, drawSize.Height),
			new Color (1, 1, 1));

		Color gray = new (.8, .8, .8);

		// Gray blocks (top row)
		for (int x = paddingWidth; x < p.X; x += bsize * 2) {
			int bwidth = (x + bsize > p.X) ? (p.X - x) : bsize;
			context.FillRectangle (
				new RectangleD (x, paddingHeight, bwidth, bsize),
				gray);
		}

		// Gray blocks (bottom row, offset)
		for (int x = paddingWidth + bsize; x < p.X; x += bsize * 2) {
			int bwidth = (x + bsize > p.X) ? (p.X - x) : bsize;
			context.FillRectangle (
				// Use drawSize.Height / 2 for Y coordinate as in the original implementation
				new RectangleD (x, paddingHeight + drawSize.Height / 2, bwidth, bsize),
				gray);
		}

		// --- Draw the actual gradient ---

		// The original implementation defined the gradient end point using p.Y for y1.
		LinearGradient pat = new (
			x0: paddingWidth,
			y0: paddingHeight,
			x1: p.X,
			y1: p.Y);

		// Normalize the gradient stops to the range [0, 1] for Cairo.
		var normalized = colors.Resized (startPosition: 0, endPosition: 1);

		pat.AddColorStop (normalized.StartPosition, normalized.StartColor);

		for (int i = 0; i < normalized.StopsCount; i++)
			pat.AddColorStop (normalized.Positions[i], normalized.Colors[i]);

		pat.AddColorStop (normalized.EndPosition, normalized.EndColor);

		context.Rectangle (
			paddingWidth,
			paddingHeight,
			drawSize.Width,
			drawSize.Height);

		context.SetSource (pat);
		context.Fill ();
	}
}
