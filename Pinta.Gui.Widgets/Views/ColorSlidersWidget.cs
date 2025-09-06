using System;
using System.Collections.Generic;
using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

public sealed class ColorSlidersWidget : Gtk.Box
{
	private readonly ColorPickerViewModel main_view_model;
	private readonly Dictionary<ColorChannel, ColorPickerSliderViewModel> slider_view_models = [];
	private readonly Dictionary<ColorChannel, ColorPickerSlider> slider_views = [];

	private readonly Gtk.Entry hex_entry;

	public ColorSlidersWidget (ColorPickerViewModel mainViewModel, Gtk.Window parentWindow)
	{
		main_view_model = mainViewModel;
		LayoutSettings initialLayout = mainViewModel.State.Layout;
		Color initialColor = mainViewModel.State.CurrentColor;

		Gtk.Entry hexEntry = new () {
			Text_ = initialColor.ToHex (),
			MaxWidthChars = 10,
		};
		hexEntry.OnChanged += OnHexEntryChanged;

		Gtk.Label hexLabel = new () {
			Label_ = Translations.GetString ("Hex"),
			WidthRequest = 50,
		};

		Gtk.Box hexBox = new () { Spacing = initialLayout.Spacing };
		hexBox.Append (hexLabel);
		hexBox.Append (hexEntry);

		SliderLayoutSettings sliderLayout = new (
			PaddingWidth: LayoutSettings.CPS_PADDING_WIDTH,
			PaddingHeight: LayoutSettings.CPS_PADDING_HEIGHT
		);

		InitializeSliders (initialColor, parentWindow, sliderLayout, initialLayout.SliderWidth);

		// --- Layout (Gtk.Box) ---

		SetOrientation (Gtk.Orientation.Vertical);
		Spacing = initialLayout.Spacing;

		Append (hexBox);
		Append (slider_views[ColorChannel.Hue]);
		Append (slider_views[ColorChannel.Saturation]);
		Append (slider_views[ColorChannel.Value]);
		Append (new Gtk.Separator ());
		Append (slider_views[ColorChannel.Red]);
		Append (slider_views[ColorChannel.Green]);
		Append (slider_views[ColorChannel.Blue]);
		Append (new Gtk.Separator ());
		Append (slider_views[ColorChannel.Alpha]);

		// --- References ---
		hex_entry = hexEntry;

		// --- Event Subscription ---
		mainViewModel.StateChanged += OnMainViewModelStateChanged;
	}

	private void InitializeSliders (Color initialColor, Gtk.Window parentWindow, SliderLayoutSettings layout, int initialSliderWidth)
	{
		HsvColor initialHsv = initialColor.ToHsv ();

		// Helper to create a slider ViewModel
		ColorPickerSliderViewModel CreateViewModel (ColorChannel channel, string label, int max, double initialValue, Action<double> onValueChanged)
		{
			// MaxWidthChars = 3 (e.g., 360 or 255)
			ColorSliderSettings settings = new (channel, label, max, 3);
			return new ColorPickerSliderViewModel (settings, initialValue) {
				// Define the action that updates the main ViewModel when this slider changes
				OnValueChanged = onValueChanged
			};
		}

		ColorPickerSliderViewModel viewModelHue = CreateViewModel (
			ColorChannel.Hue,
			Translations.GetString ("Hue"),
			360,
			initialHsv.Hue,
			value => main_view_model.SetColorFromHsv (hue: value));

		ColorPickerSliderViewModel viewModelSaturation = CreateViewModel (
			ColorChannel.Saturation,
			Translations.GetString ("Sat"),
			100,
			initialHsv.Sat * 100.0,
			value => main_view_model.SetColorFromHsv (sat: value / 100.0));

		ColorPickerSliderViewModel viewModelValue = CreateViewModel (
			ColorChannel.Value,
			Translations.GetString ("Value"),
			100,
			initialHsv.Val * 100.0,
			value => main_view_model.SetColorFromHsv (value: value / 100.0));

		ColorPickerSliderViewModel viewModelRed = CreateViewModel (
			ColorChannel.Red,
			Translations.GetString ("Red"),
			255,
			initialColor.R * 255.0,
			value => main_view_model.SetColorFromRgb (r: value / 255.0));

		ColorPickerSliderViewModel viewModelGreen = CreateViewModel (
			ColorChannel.Green,
			Translations.GetString ("Green"),
			255,
			initialColor.G * 255.0,
			value => main_view_model.SetColorFromRgb (g: value / 255.0));

		ColorPickerSliderViewModel viewModelBlue = CreateViewModel (
			ColorChannel.Blue,
			Translations.GetString ("Blue"),
			255,
			initialColor.B * 255.0,
			value => main_view_model.SetColorFromRgb (b: value / 255.0));

		ColorPickerSliderViewModel viewModelAlpha = CreateViewModel (
			ColorChannel.Alpha,
			Translations.GetString ("Alpha"),
			255,
			initialColor.A * 255.0,
			value => main_view_model.SetAlpha (alpha: value / 255.0));

		slider_view_models.Add (ColorChannel.Hue, viewModelHue);
		slider_view_models.Add (ColorChannel.Saturation, viewModelSaturation);
		slider_view_models.Add (ColorChannel.Value, viewModelValue);

		slider_view_models.Add (ColorChannel.Red, viewModelRed);
		slider_view_models.Add (ColorChannel.Green, viewModelGreen);
		slider_view_models.Add (ColorChannel.Blue, viewModelBlue);
		slider_view_models.Add (ColorChannel.Alpha, viewModelAlpha);

		void CreateView (ColorChannel channel, Func<Color, ColorGradient<Color>> gradientGenerator)
		{
			ColorPickerSlider view = new (slider_view_models[channel], layout, parentWindow, initialSliderWidth) {
				GradientGenerator = gradientGenerator
			};

			slider_views.Add (channel, view);
		}

		CreateView (
			ColorChannel.Hue,
			c => ColorGradient.Create (
				startColor: c.CopyHsv (hue: 0),
				endColor: c.CopyHsv (hue: 360),
				startPosition: 0,
				endPosition: 360,
				new Dictionary<double, Color> {
					[60] = c.CopyHsv (hue: 60),
					[120] = c.CopyHsv (hue: 120),
					[180] = c.CopyHsv (hue: 180),
					[240] = c.CopyHsv (hue: 240),
					[300] = c.CopyHsv (hue: 300),
				}
			)
		);

		CreateView (ColorChannel.Saturation, c => ColorGradient.Create (c.CopyHsv (sat: 0), c.CopyHsv (sat: 1)));
		CreateView (ColorChannel.Value, c => ColorGradient.Create (c.CopyHsv (value: 0), c.CopyHsv (value: 1)));
		CreateView (ColorChannel.Red, c => ColorGradient.Create (c with { R = 0 }, c with { R = 1 }));
		CreateView (ColorChannel.Green, c => ColorGradient.Create (c with { G = 0 }, c with { G = 1 }));
		CreateView (ColorChannel.Blue, c => ColorGradient.Create (c with { B = 0 }, c with { B = 1 }));
		CreateView (ColorChannel.Alpha, c => ColorGradient.Create (c with { A = 0 }, c with { A = 1 }));
	}

	private void OnMainViewModelStateChanged (object? sender, EventArgs e)
	{
		UpdateView (main_view_model.State);
	}

	private void OnHexEntryChanged (Gtk.Editable sender, EventArgs _)
	{
		if ((hex_entry.GetRoot () as Gtk.Window)?.GetFocus ()?.Parent != sender) return;
		main_view_model.SetColorFromHex (sender.GetText ());
	}

	private void UpdateView (ColorPickerState state)
	{
		Color currentColor = state.CurrentColor;
		HsvColor currentHsv = currentColor.ToHsv ();
		LayoutSettings layout = state.Layout;

		Spacing = layout.Spacing;

		if (GetFirstChild () is Gtk.Box hexBox)
			hexBox.Spacing = layout.Spacing;

		if ((hex_entry.GetRoot () as Gtk.Window)?.GetFocus ()?.Parent != hex_entry)
			hex_entry.SetText (currentColor.ToHex ());

		slider_view_models[ColorChannel.Hue].UpdateValueFromModel (currentHsv.Hue);
		slider_view_models[ColorChannel.Saturation].UpdateValueFromModel (currentHsv.Sat * 100.0);
		slider_view_models[ColorChannel.Value].UpdateValueFromModel (currentHsv.Val * 100.0);
		slider_view_models[ColorChannel.Red].UpdateValueFromModel (currentColor.R * 255.0);
		slider_view_models[ColorChannel.Green].UpdateValueFromModel (currentColor.G * 255.0);
		slider_view_models[ColorChannel.Blue].UpdateValueFromModel (currentColor.B * 255.0);
		slider_view_models[ColorChannel.Alpha].UpdateValueFromModel (currentColor.A * 255.0);

		foreach (var view in slider_views.Values)
			view.Update (layout, currentColor);
	}
}
