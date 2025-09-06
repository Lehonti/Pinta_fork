using System;

namespace Pinta.Gui.Widgets;

public sealed class ColorPickerSliderViewModel (ColorSliderSettings settings, double initialValue)
{
	public ColorSliderSettings Settings { get; } = settings;
	public double Value { get; private set; } = initialValue;

	public Action<double>? OnValueChanged { get; init; }

	public void UpdateValueFromView (double newValue)
	{
		double clampedValue = Math.Clamp (newValue, 0, Settings.MaxValue);
		if (Math.Abs (Value - clampedValue) < 0.001) return;
		Value = clampedValue;
		OnValueChanged?.Invoke (Value);
	}

	public void UpdateValueFromModel (double newValue)
	{
		Value = Math.Clamp (newValue, 0, Settings.MaxValue);
	}
}
