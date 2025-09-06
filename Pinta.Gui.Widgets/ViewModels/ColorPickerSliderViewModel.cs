using System;

namespace Pinta.Gui.Widgets.ViewModels;

public sealed record ColorSliderSettings (
    double Max,
    string Text
);

// ViewModel for the ColorPickerSlider (Principle 10.4.1).
public sealed class ColorPickerSliderViewModel
{
	private readonly ColorSliderSettings settings;
	private double value;

	// Event to notify the View or parent ViewModel.
	public event EventHandler<EventArgs>? ValueChanged;

	public ColorPickerSliderViewModel (ColorSliderSettings settings, double initialValue)
	{
		this.settings = settings;
		value = Clamp (initialValue);
	}

	public double Max => settings.Max;
	public string Text => settings.Text;

	public double Value {
		get => value;
		set {
			double clampedValue = Clamp (value);
			// Optimization: Check if value actually changed.
			if (Math.Abs (this.value - clampedValue) > 1e-9) {
				this.value = clampedValue;
				OnValueChanged ();
			}
		}
	}

	private double Clamp (double value) => Math.Clamp (value, 0, settings.Max);

	private void OnValueChanged ()
	{
		ValueChanged?.Invoke (this, EventArgs.Empty);
	}
}
