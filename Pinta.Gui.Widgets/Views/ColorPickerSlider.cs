using System;
using System.Globalization;
using Cairo;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;

namespace Pinta.Gui.Widgets.Views;

public sealed record SliderLayoutSettings (
    int SliderPaddingWidth,
    int SliderPaddingHeight,
    int SliderWidth,
    int MaxWidthChars);

public sealed class ColorPickerSlider : Box, IDisposable
{
	private readonly SliderLayoutSettings layout_settings;
	private readonly ColorPickerSliderViewModel view_model;
	private readonly Window parent_window;

	private readonly Scale slider_control;
	private readonly Entry input_field;
	private readonly Overlay slider_overlay;
	private readonly DrawingArea cursor_area;

	public DrawingArea Gradient { get; }

	private bool suppress_event;

	public ColorPickerSlider (ColorPickerSliderViewModel viewModel, SliderLayoutSettings layout, Window parentWindow)
	{
		// --- Initialization (as local variables) ---

		var sliderLabel = Gtk.Label.New (viewModel.Text);
		sliderLabel.WidthRequest = 50;

		var sliderControl = Gtk.Scale.New (Orientation.Horizontal, null);
		sliderControl.SetAdjustment (Adjustment.New (viewModel.Value, 0, viewModel.Max + 1, 1, 1, 1));
		sliderControl.Opacity = 0; // Hidden, used only for interaction

		var gradient = new Gtk.DrawingArea ();
		var cursorArea = new Gtk.DrawingArea ();
		cursorArea.SetDrawFunc (CursorAreaDrawingFunction);

		var sliderOverlay = new Gtk.Overlay ();
		sliderOverlay.AddOverlay (gradient);
		sliderOverlay.AddOverlay (cursorArea);
		sliderOverlay.AddOverlay (sliderControl);

		var inputField = new Gtk.Entry {
			MaxWidthChars = layout.MaxWidthChars,
			WidthRequest = 50,
			Hexpand = false
		};
		inputField.SetText (FormatValue (viewModel.Value));

		// --- Compose Layout ---
		Append (sliderLabel);
		Append (sliderOverlay);
		Append (inputField);

		// --- Assign to Fields ---
		view_model = viewModel;
		layout_settings = layout;
		parent_window = parentWindow;
		slider_control = sliderControl;
		input_field = inputField;
		slider_overlay = sliderOverlay;
		cursor_area = cursorArea;
		Gradient = gradient;

		// --- Final Setup & Subscriptions ---
		SetSliderWidth (layout.SliderWidth);
		UpdateUIFromViewModel (); // Initial sync

		slider_control.OnValueChanged += HandleSliderControlValueChanged;
		input_field.OnChanged += HandleInputFieldChanged;
		view_model.ValueChanged += HandleViewModelValueChanged;
	}

	public override void Dispose ()
	{
		slider_control.OnValueChanged -= HandleSliderControlValueChanged;
		input_field.OnChanged -= HandleInputFieldChanged;
		view_model.ValueChanged -= HandleViewModelValueChanged;
	}

	#region Event Handlers and Synchronization

	private void HandleViewModelValueChanged (object? sender, EventArgs e)
	{
		UpdateUIFromViewModel ();
	}

	private void UpdateUIFromViewModel ()
	{
		if (suppress_event) return;

		suppress_event = true;
		try {
			double value = view_model.Value;
			slider_control.SetValue (value);

			// GTK focus management workaround.
			if (parent_window.GetFocus ()?.GetParent () != input_field) {
				input_field.SetText (FormatValue (value));
			}
		} finally {
			suppress_event = false;
		}

		Gradient.QueueDraw ();
		cursor_area.QueueDraw ();
	}

	private void HandleSliderControlValueChanged (Gtk.Range sender, EventArgs args)
	{
		if (suppress_event) return;

		double value = sender.GetValue ();
		double clampedValue = Math.Min (value, view_model.Max);

		suppress_event = true;
		try {
			input_field.SetText (FormatValue (clampedValue));
		} finally {
			suppress_event = false;
		}

		view_model.Value = clampedValue;
	}

	private void HandleInputFieldChanged (Editable sender, EventArgs e)
	{
		if (suppress_event) return;

		if (!double.TryParse (sender.GetText (), CultureInfo.InvariantCulture, out double parsed))
			return;

		double clampedValue = Math.Clamp (parsed, 0, view_model.Max);

		suppress_event = true;
		try {
			if (Math.Abs (clampedValue - parsed) > 1e-9) {
				sender.SetText (FormatValue (clampedValue));
			}
			slider_control.SetValue (clampedValue);
		} finally {
			suppress_event = false;
		}

		view_model.Value = clampedValue;
	}

	private static string FormatValue (double value) => Convert.ToInt32 (value).ToString (CultureInfo.InvariantCulture);

	#endregion

	#region Layout and Drawing

	public void SetSliderWidth (int sliderWidth)
	{
		slider_control.WidthRequest = sliderWidth;
		int height = GetDesiredHeight ();
		Gradient.SetSizeRequest (sliderWidth, height);
		cursor_area.SetSizeRequest (sliderWidth, height);
		slider_overlay.SetSizeRequest (sliderWidth, height);
	}

	private int GetDesiredHeight ()
	{
		const int BASE_GRADIENT_HEIGHT = 20;
		return BASE_GRADIENT_HEIGHT + layout_settings.SliderPaddingHeight * 2;
	}

	private void CursorAreaDrawingFunction (DrawingArea area, Context context, int width, int height)
	{
		const int OUTLINE_WIDTH = 2;

		double currentValue = slider_control.GetValue ();
		double normalizedValue = Math.Clamp (currentValue / view_model.Max, 0.0, 1.0);
		double drawWidth = width - 2 * layout_settings.SliderPaddingWidth;
		double currentPosition = normalizedValue * drawWidth + layout_settings.SliderPaddingWidth;

		ReadOnlySpan<PointD> cursorPoly = [
	    new (currentPosition, height / 2.0),
	    new (currentPosition + 4, 3 * height / 4.0),
	    new (currentPosition + 4, height - OUTLINE_WIDTH / 2.0),
	    new (currentPosition - 4, height - OUTLINE_WIDTH / 2.0),
	    new (currentPosition - 4, 3 * height / 4.0),
	    new (currentPosition, height / 2.0),
	];

		context.LineWidth = OUTLINE_WIDTH;
		context.DrawPolygonal (cursorPoly, new Color (0, 0, 0), LineCap.Butt);
		context.FillPolygonal (cursorPoly, new Color (1, 1, 1));
	}

	public void DrawGradient (Context context, int width, int height, ColorGradient<Color> colors)
	{
		context.Antialias = Antialias.None;

		int drawWidth = width - layout_settings.SliderPaddingWidth * 2;
		int drawHeight = height - layout_settings.SliderPaddingHeight * 2;

		int startX = layout_settings.SliderPaddingWidth;
		int startY = layout_settings.SliderPaddingHeight;
		int endX = startX + drawWidth;

		RectangleD drawRect = new (startX, startY, drawWidth, drawHeight);

		// 1. Draw transparency background
		DrawCheckerboardBackground (context, drawRect);

		// 2. Draw color gradient overlay
		LinearGradient pat = new (
		    x0: startX,
		    y0: startY + drawHeight / 2.0,
		    x1: endX,
		    y1: startY + drawHeight / 2.0);

		// Normalize color stops to [0, 1]
		var normalized = colors.Resized (startPosition: 0, endPosition: 1);

		pat.AddColorStop (normalized.StartPosition, normalized.StartColor);

		for (int i = 0; i < normalized.StopsCount; i++)
			pat.AddColorStop (normalized.Positions[i], normalized.Colors[i]);

		pat.AddColorStop (normalized.EndPosition, normalized.EndColor);

		context.Rectangle (drawRect);
		context.SetSource (pat);
		context.Fill ();
	}

	private static void DrawCheckerboardBackground (Context context, RectangleD rect)
	{
		int bsize = (int) rect.Height / 2;
		if (bsize == 0) return;

		Color light = new (1, 1, 1);
		Color dark = new (.8, .8, .8);

		context.FillRectangle (rect, light);

		// Top row dark squares
		for (int x = (int) rect.X; x < rect.X + rect.Width; x += bsize * 2) {
			int bwidth = Math.Min (bsize, (int) (rect.X + rect.Width - x));
			if (bwidth > 0)
				context.FillRectangle (new RectangleD (x, rect.Y, bwidth, bsize), dark);
		}

		// Bottom row dark squares (offset)
		double bottomY = rect.Y + bsize;
		double bottomHeight = rect.Height - bsize;
		if (bottomHeight > 0) {
			for (int x = (int) rect.X + bsize; x < rect.X + rect.Width; x += bsize * 2) {
				int bwidth = Math.Min (bsize, (int) (rect.X + rect.Width - x));
				if (bwidth > 0)
					context.FillRectangle (new RectangleD (x, bottomY, bwidth, bottomHeight), dark);
			}
		}
	}
	#endregion
}
