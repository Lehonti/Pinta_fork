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

	public Func<Color, ColorGradient<Color>>? GradientGenerator { get; init; }

	private Color current_color_context;

	private bool suppress_input_field_event = false;

	public ColorPickerSlider (ColorPickerSliderViewModel viewModel, SliderLayoutSettings layout, Gtk.Window parentWindow, int initialSliderWidth)
	{
		view_model = viewModel;
		layout_settings = layout;
		parent_window = parentWindow;

		int estimatedHeight = layout.PaddingHeight * 2 + 10;

		Gtk.Scale sliderControl = new () {
			WidthRequest = initialSliderWidth,
			Opacity = 0,
		};
		sliderControl.SetOrientation (Gtk.Orientation.Horizontal);
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

	public void Update (LayoutSettings layout, Color currentColorContext)
	{
		current_color_context = currentColorContext;

		int sliderWidth = layout.SliderWidth;
		slider_control.WidthRequest = sliderWidth;

		int currentHeight = GetHeight ();
		int height = currentHeight > 0 ? currentHeight : slider_overlay.HeightRequest;

		gradient_area.SetSizeRequest (sliderWidth, height);
		cursor_area.SetSizeRequest (sliderWidth, height);
		slider_overlay.WidthRequest = sliderWidth;

		UpdateValueDisplay (view_model.Value);
	}

	private void UpdateValueDisplay (double value)
	{
		if (Math.Abs (slider_control.GetValue () - value) > 0.001)
			slider_control.SetValue (value);

		if (parent_window.GetFocus ()?.Parent != input_field) {
			suppress_input_field_event = true;
			input_field.SetText (Convert.ToInt32 (value).ToString (CultureInfo.InvariantCulture));
			suppress_input_field_event = false;
		}

		gradient_area.QueueDraw ();
		cursor_area.QueueDraw ();
	}

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
			UpdateValueDisplay (parsed);
		}

		view_model.UpdateValueFromView (parsed);
	}

	private void DrawCursor (Gtk.DrawingArea area, Context context, int width, int height)
	{
		const int OUTLINE_WIDTH = 2;

		int paddingWidth = layout_settings.PaddingWidth;
		double max = view_model.Settings.MaxValue;

		double currentPosition = view_model.Value / max * (width - 2 * paddingWidth) + paddingWidth;

		ReadOnlySpan<PointD> cursorPoly = [
			new (currentPosition, height / 2),
			new (currentPosition + 4, 3 * height / 4),
			new (currentPosition + 4, height - OUTLINE_WIDTH / 2),
			new (currentPosition - 4, height - OUTLINE_WIDTH / 2),
			new (currentPosition - 4, 3 * height / 4),
			new (currentPosition, height / 2),
		];

		context.LineWidth = OUTLINE_WIDTH;

		context.DrawPolygonal (cursorPoly, Color.Black, LineCap.Butt);
		context.FillPolygonal (cursorPoly, Color.White);
	}

	private void DrawGradient (Gtk.DrawingArea area, Context context, int width, int height)
	{
		if (GradientGenerator == null) return;

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

		context.FillRectangle (
			new RectangleD (paddingWidth, paddingHeight, drawSize.Width, drawSize.Height),
			new Color (1, 1, 1));

		Color gray = new (.8, .8, .8);

		for (int x = paddingWidth; x < p.X; x += bsize * 2) {
			int bwidth = (x + bsize > p.X) ? (p.X - x) : bsize;
			context.FillRectangle (
				new RectangleD (x, paddingHeight, bwidth, bsize),
				gray);
		}

		for (int x = paddingWidth + bsize; x < p.X; x += bsize * 2) {
			int bwidth = (x + bsize > p.X) ? (p.X - x) : bsize;
			context.FillRectangle (
				new RectangleD (x, paddingHeight + drawSize.Height / 2, bwidth, bsize),
				gray);
		}

		// --- Draw the actual gradient ---

		LinearGradient pat = new (
			x0: paddingWidth,
			y0: paddingHeight,
			x1: p.X,
			y1: p.Y);

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
