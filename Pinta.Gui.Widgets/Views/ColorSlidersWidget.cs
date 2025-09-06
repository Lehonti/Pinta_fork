using System.Collections.Generic;

namespace Pinta.Gui.Widgets.Views;

public sealed class ColorSlidersWidget : Gtk.Box
{
	private readonly Dictionary<Channel, ColorPickerSliderViewModel> slider_view_models = [];
	private readonly Dictionary<Channel, ColorPickerSlider> slider_views = [];

	public ColorSlidersWidget (ColorPickerViewModel mainViewModel, Gtk.Window parentWindow)
	{
		// --- TODO
	}
}
