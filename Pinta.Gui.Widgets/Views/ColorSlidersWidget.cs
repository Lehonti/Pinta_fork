using System;
using System.Collections.Generic;
using Cairo;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;

namespace Pinta.Gui.Widgets.Views;

public sealed class ColorSlidersWidget : Box, IDisposable
{
	private readonly ColorPickerViewModel main_view_model;
	private readonly Window parent_window;
	private readonly Entry hex_entry;

	private readonly Dictionary<string, ColorPickerSliderViewModel> slider_view_models = [];
	private readonly Dictionary<string, ColorPickerSlider> slider_views = [];

	public ColorSlidersWidget (ColorPickerViewModel mainViewModel, Window parentWindow)
	{
		SetOrientation (Orientation.Vertical);

		// --- Initialization ---
		var hexEntry = new Entry { MaxWidthChars = 10 };
		var hexLabel = new Label { Label_ = Translations.GetString ("Hex"), WidthRequest = 50 };
		var hexBox = new Box ();
		hexBox.SetOrientation (Orientation.Horizontal);
		hexBox.Append (hexLabel);
		hexBox.Append (hexEntry);

		// --- Assign to Fields ---
		main_view_model = mainViewModel;
		parent_window = parentWindow;
		hex_entry = hexEntry;

		Append (hexBox);
		InitializeSliders (mainViewModel, parentWindow); // Populates the dictionaries and appends widgets
		Append (new Separator ());
		Append (slider_views["R"]);
		Append (slider_views["G"]);
		Append (slider_views["B"]);
		Append (new Separator ());
		Append (slider_views["A"]);

		// --- Final Setup & Subscriptions ---
		Render (main_view_model.State);
		main_view_model.StateChanged += HandleStateChanged;
		hex_entry.OnChanged += HandleHexEntryChanged;
	}

	public override void Dispose ()
	{
		main_view_model.StateChanged -= HandleStateChanged;
		hex_entry.OnChanged -= HandleHexEntryChanged;
		foreach (var view in slider_views.Values)
			view.Dispose ();
	}

	private void HandleStateChanged (object? sender, EventArgs e) => Render (main_view_model.State);

	private void HandleHexEntryChanged (Editable sender, EventArgs _)
	{
		if (parent_window.GetFocus ()?.GetParent () != sender) return;
		main_view_model.SetColorFromHex (sender.GetText ());
	}

	private void Render (ColorPickerState state)
	{
		var layout = state.Layout;
		Spacing = layout.Spacing;

		foreach (var sliderView in slider_views.Values)
			sliderView.SetSliderWidth (layout.SliderWidth);

		UpdateChildViewModels (state.CurrentColor);

		if (parent_window.GetFocus ()?.GetParent () != hex_entry)
			hex_entry.SetText (state.CurrentColor.ToHex ());
	}

	private void UpdateChildViewModels (Color color)
	{
		HsvColor hsv = color.ToHsv ();
		slider_view_models["H"].Value = hsv.Hue;
		slider_view_models["S"].Value = hsv.Sat * 100.0;
		slider_view_models["V"].Value = hsv.Val * 100.0;
		slider_view_models["R"].Value = color.R * 255.0;
		slider_view_models["G"].Value = color.G * 255.0;
		slider_view_models["B"].Value = color.B * 255.0;
		slider_view_models["A"].Value = color.A * 255.0;
	}

	private void InitializeSliders (ColorPickerViewModel mainVM, Window parent)
	{
		var layoutSettings = CreateSliderLayoutSettings (mainVM.State.Layout);

		// Define and create each slider, then add to the layout
		InitializeSlider ("H", 360, Translations.GetString ("Hue"), c => c.ToHsv ().Hue, mainVM.SetHue, layoutSettings, parent,
		    c => (c.CopyHsv (hue: 0), c.CopyHsv (hue: 360), new () {
			    [60] = c.CopyHsv (hue: 60),
			    [120] = c.CopyHsv (hue: 120),
			    [180] = c.CopyHsv (hue: 180),
			    [240] = c.CopyHsv (hue: 240),
			    [300] = c.CopyHsv (hue: 300)
		    })
		);
		InitializeSlider ("S", 100, Translations.GetString ("Sat"), c => c.ToHsv ().Sat * 100.0, mainVM.SetSaturation, layoutSettings, parent, c => (c.CopyHsv (sat: 0), c.CopyHsv (sat: 1), null));
		InitializeSlider ("V", 100, Translations.GetString ("Value"), c => c.ToHsv ().Val * 100.0, mainVM.SetValue, layoutSettings, parent, c => (c.CopyHsv (value: 0), c.CopyHsv (value: 1), null));
		InitializeSlider ("R", 255, Translations.GetString ("Red"), c => c.R * 255.0, mainVM.SetRed, layoutSettings, parent, c => (c with { R = 0 }, c with { R = 1 }, null));
		InitializeSlider ("G", 255, Translations.GetString ("Green"), c => c.G * 255.0, mainVM.SetGreen, layoutSettings, parent, c => (c with { G = 0 }, c with { G = 1 }, null));
		InitializeSlider ("B", 255, Translations.GetString ("Blue"), c => c.B * 255.0, mainVM.SetBlue, layoutSettings, parent, c => (c with { B = 0 }, c with { B = 1 }, null));
		InitializeSlider ("A", 255, Translations.GetString ("Alpha"), c => c.A * 255.0, mainVM.SetAlpha, layoutSettings, parent, c => (c with { A = 0 }, c with { A = 1 }, null));

		// Append HSV sliders in order
		Append (slider_views["H"]);
		Append (slider_views["S"]);
		Append (slider_views["V"]);
	}

	private void InitializeSlider (
	    string key, double max, string text,
	    Func<Color, double> initialValueSelector,
	    Action<double> mainVMUpdater,
	    SliderLayoutSettings layoutSettings,
	    Window parent,
	    Func<Color, (Color start, Color end, Dictionary<double, Color>? stops)> gradientColorsSelector
	)
	{
		var sliderVM = new ColorPickerSliderViewModel (
		    new ColorSliderSettings (max, text),
		    initialValueSelector (main_view_model.State.CurrentColor)
		);
		sliderVM.ValueChanged += (s, e) => mainVMUpdater (sliderVM.Value);
		slider_view_models[key] = sliderVM;

		var sliderView = new ColorPickerSlider (sliderVM, layoutSettings, parent);
		sliderView.Gradient.SetDrawFunc ((_, c, w, h) => {
			var (start, end, stops) = gradientColorsSelector (main_view_model.State.CurrentColor);
			var gradient = stops == null
			    ? ColorGradient.Create (start, end)
			    : ColorGradient.Create (start, end, 0, max, stops);
			sliderView.DrawGradient (c, w, h, gradient);
		});
		slider_views[key] = sliderView;
	}

	private static SliderLayoutSettings CreateSliderLayoutSettings (LayoutSettings layout)
	{
		return new SliderLayoutSettings (
		    SliderPaddingHeight: LayoutSettings.CPS_PADDING_HEIGHT,
		    SliderPaddingWidth: LayoutSettings.CPS_PADDING_WIDTH,
		    SliderWidth: layout.SliderWidth,
		    MaxWidthChars: 3
		);
	}
}
