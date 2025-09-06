using System;
using Gtk;
using Pinta.Core;
using Pinta.Gui.Widgets.ViewModels;
using Pinta.Gui.Widgets.Views;

namespace Pinta.Gui.Widgets;

public sealed class ColorPickerDialog : Dialog
{
	private readonly ColorPickerViewModel view_model;
	private readonly Button shrink_button;
	private readonly Box top_box;
	private readonly Box main_vbox;
	private readonly IDisposable[] disposable_widgets;

	public ColorPick Colors => view_model.FinalColors;

	internal ColorPickerDialog (
	    Window? parentWindow,
	    PaletteManager paletteManager,
	    ColorPick adjustable,
	    bool primarySelected,
	    bool livePaletteMode,
	    string windowTitle)
	{
		// --- Initialize Dependencies & ViewModel ---
		var vm = new ColorPickerViewModel (adjustable, primarySelected, livePaletteMode ? paletteManager : null);

		// --- Initialize UI Components (as local variables) ---
		var titleBar = new HeaderBar ();
		var shrinkButton = InitializeHeaderBar (titleBar, vm);

		// Assuming ColorSurfaceWidget and ColorSwatchesWidget are similarly refactored to be IDisposable.
		// If not, they would be omitted from the disposable_widgets array.
		var colorDisplayWidget = new ColorDisplayWidget (vm);
		var colorSurfaceWidget = new ColorSurfaceWidget (vm); // Assuming this class exists and is updated
		var colorSlidersWidget = new ColorSlidersWidget (vm, this);
		var colorSwatchesWidget = new ColorSwatchesWidget (vm, paletteManager); // Assuming this class exists

		var topBox = new Box ();
		var mainVbox = new Box ();
		mainVbox.SetOrientation (Orientation.Vertical);

		var contentArea = this.GetContentAreaBox ();
		InitializeMainLayout (topBox, mainVbox, contentArea, colorDisplayWidget, colorSurfaceWidget, colorSlidersWidget, colorSwatchesWidget);
		InitializeInteractionControllers (vm);
		InitializeWindowProperties (parentWindow, titleBar, windowTitle, livePaletteMode);

		// --- Assign to Fields ---
		view_model = vm;
		shrink_button = shrinkButton;
		top_box = topBox;
		main_vbox = mainVbox;
		disposable_widgets = [
	    colorDisplayWidget,
	    colorSurfaceWidget,
	    colorSlidersWidget,
	    colorSwatchesWidget
	];

		// --- Final Setup & Subscriptions ---
		Render (vm.State); // Initial render
		view_model.StateChanged += HandleStateChanged;
		OnResponse += HandleDialogResponse;
	}

	private void HandleStateChanged (object? sender, EventArgs e) => Render (view_model.State);

	private void Render (ColorPickerState state)
	{
		var layout = state.Layout;
		this.GetContentAreaBox ().SetAllMargins (layout.Margins);
		main_vbox.Spacing = layout.Spacing;
		top_box.Spacing = layout.Spacing;

		shrink_button.SetIconName (
		    state.IsSmallMode
		    ? Resources.StandardIcons.WindowMaximize
		    : Resources.StandardIcons.WindowMinimize);

		DefaultWidth = 1;
		DefaultHeight = 1;
	}

	#region Initialization

	private Button InitializeHeaderBar (HeaderBar titleBar, ColorPickerViewModel vm)
	{
		var resetButton = new Button { Label = Translations.GetString ("Reset Color") };
		resetButton.OnClicked += (s, a) => vm.ResetColors ();

		var shrinkButton = new Button ();
		shrinkButton.OnClicked += (s, a) => vm.ToggleSmallMode ();

		var okButton = new Button { Label = Translations.GetString ("OK") };
		okButton.OnClicked += (s, a) => Response ((int) ResponseType.Ok);
		okButton.AddCssClass (AdwaitaStyles.SuggestedAction);

		var cancelButton = new Button { Label = Translations.GetString ("Cancel") };
		cancelButton.OnClicked += (s, a) => Response ((int) ResponseType.Cancel);

		titleBar.PackStart (resetButton);
		titleBar.PackStart (shrinkButton);
		titleBar.PackEnd (okButton);
		titleBar.PackEnd (cancelButton);
		titleBar.SetShowTitleButtons (false);

		return shrinkButton;
	}

	private static void InitializeMainLayout (
	    Box topBox, Box mainVbox, Box contentArea,
	    ColorDisplayWidget display,
	    ColorSurfaceWidget surface,
	    ColorSlidersWidget sliders,
	    ColorSwatchesWidget swatches)
	{
		topBox.SetOrientation (Orientation.Horizontal);
		topBox.Append (display);
		topBox.Append (surface);
		topBox.Append (sliders);

		mainVbox.Append (topBox);
		mainVbox.Append (swatches);

		contentArea.Append (mainVbox);
	}

	private void InitializeInteractionControllers (ColorPickerViewModel vm)
	{
		var keyboard_gesture = EventControllerKey.New ();
		keyboard_gesture.OnKeyPressed += (s, e) => {
			if (e.GetKey ().Value == Gdk.Constants.KEY_x) {
				vm.SwapColors ();
				return true;
			}
			return false;
		};
		AddController (keyboard_gesture);
	}

	private void InitializeWindowProperties (Window? parentWindow, HeaderBar titleBar, string windowTitle, bool liveMode)
	{
		SetOpacity (0.995f);
		SetTitlebar (titleBar);
		Title = Translations.GetString (windowTitle);
		TransientFor = parentWindow;
		Modal = !liveMode;
		IconName = Resources.Icons.ImageResizeCanvas;
		this.SetDefaultResponse (ResponseType.Cancel);

		if (liveMode)
			IsActivePropertyDefinition.Notify (this, HandleActiveWindowChanged);
	}
	#endregion

	#region Cleanup and Event Handlers

	private void HandleDialogResponse (Dialog sender, ResponseSignalArgs args)
	{
		// This is the primary cleanup point for the entire dialog.
		var response = (ResponseType) args.ResponseId;
		if (response == ResponseType.Ok) {
			view_model.CommitFinalChangesToPalette ();
		}

		view_model.StateChanged -= HandleStateChanged;

		if (TransientFor is not null) // Check if live mode was enabled.
			IsActivePropertyDefinition.Unnotify (this, HandleActiveWindowChanged);

		// Dispose all child widgets and the ViewModel.
		foreach (var widget in disposable_widgets)
			widget.Dispose ();

		view_model.Dispose ();

		// After cleanup, close the dialog.
		if (response == ResponseType.Ok || response == ResponseType.Cancel)
			Close ();
	}

	private void HandleActiveWindowChanged (object? sender, NotifySignalArgs args)
	{
		if (IsActive) {
			SetOpacity (1.0f);
			return;
		}
		SetOpacity (0.85f);
		view_model.CommitFinalChangesToPalette ();
	}

	#endregion
}
