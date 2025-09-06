using System;

namespace Pinta.Gui.Widgets;

// ViewModel for the ColorPickerSlider (Principle 10.4.1).
public sealed class ColorPickerSliderViewModel (ColorSliderSettings settings, double initialValue)
{
	public ColorSliderSettings Settings { get; } = settings;
	public double Value { get; private set; } = initialValue;

	// This action is invoked when the value is changed by the View (user interaction).
	// The ColorSlidersWidget subscribes to this to update the main ViewModel.
	public Action<double>? OnValueChanged { get; init; }

	// Called by the View when the user interacts with the slider or input field.
	public void UpdateValueFromView (double newValue)
	{
		double clampedValue = Math.Clamp (newValue, 0, Settings.MaxValue);
		if (Math.Abs (Value - clampedValue) < 0.001) return;

		Value = clampedValue;
		OnValueChanged?.Invoke (Value);
	}

	// Called by the ColorSlidersWidget when the main color changes from other sources.
	public void UpdateValueFromModel (double newValue)
	{
		Value = Math.Clamp (newValue, 0, Settings.MaxValue);
	}
}
