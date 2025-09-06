using System;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

public sealed class ColorPickerDialog : Gtk.Dialog
{
	private readonly ColorPickerViewModel view_model;
	private readonly bool live_palette_mode;

	private readonly Gtk.Box top_box;

	// These fields are kept to adhere to the instruction of minimizing mutation of fields after assignment.
	private readonly ColorSwatchesWidget? color_swatches_widget;

	private readonly Gtk.Button shrink_button;

	// The public interface remains unchanged.
	internal ColorPickerDialog (
		Gtk.Window? parentWindow,
		PaletteManager paletteManager,
		ColorPick adjustable,
		bool primarySelected,
		bool livePaletteMode,
		string windowTitle)
	{
		live_palette_mode = livePaletteMode;

		// Determine if swatches should be shown (not shown in live palette mode)
		bool showSwatches = !livePaletteMode;

		// --- Initialize ViewModel ---
		// ViewModel needs the PaletteManager if in live mode OR if swatches are shown.
		bool vmNeedsPaletteManager = livePaletteMode || showSwatches;
		ColorPickerViewModel viewModel = new (
			initialColors: adjustable,
			primarySelected: primarySelected,
			livePaletteMode: livePaletteMode,
			showSwatches: showSwatches,
			paletteManager: vmNeedsPaletteManager ? paletteManager : null
		);

		LayoutSettings initialLayout = viewModel.State.Layout;

		// --- HeaderBar / TitleBar ---
		Gtk.Button resetButton = new () { Label = Translations.GetString ("Reset Color") };
		resetButton.OnClicked += (s, e) => viewModel.ResetColors ();

		Gtk.Button shrinkButton = new ();
		shrinkButton.OnClicked += OnShrinkButtonClicked;
		// Initial icon is set based on initial state (IsSmallMode=false)
		shrinkButton.SetIconName (Resources.StandardIcons.WindowMinimize);

		Gtk.Button okButton = new () { Label = Translations.GetString ("OK") };
		okButton.OnClicked += OnOkButtonClicked;
		okButton.AddCssClass (AdwaitaStyles.SuggestedAction);

		Gtk.Button cancelButton = new () { Label = Translations.GetString ("Cancel") };
		cancelButton.OnClicked += OnCancelButtonClicked;

		Gtk.HeaderBar titleBar = new ();
		titleBar.PackStart (resetButton);
		titleBar.PackStart (shrinkButton);
		titleBar.PackEnd (okButton);
		titleBar.PackEnd (cancelButton);
		titleBar.SetShowTitleButtons (false);

		// --- Widgets Initialization (The Views) ---

		// 1. Color Display (Left)
		ColorDisplayWidget colorDisplayWidget = new (viewModel);

		// 2. Color Surface (Middle)
		ColorSurfaceWidget colorSurfaceWidget = new (viewModel);

		// 3. Sliders (Right)
		// We pass 'this' (the dialog window) so sliders can check the focus state.
		ColorSlidersWidget colorSlidersWidget = new (viewModel, this);

		// 4. Swatches (Bottom)
		ColorSwatchesWidget? colorSwatchesWidget = null;
		if (showSwatches) {
			colorSwatchesWidget = new (viewModel, paletteManager);
			// Initial visibility depends on SmallMode (initially false)
			colorSwatchesWidget.SetVisible (!viewModel.State.IsSmallMode);
		}

		// --- Layout Assembly ---

		// Top part: Display, Surface, Sliders
		Gtk.Box topBox = new () { Spacing = initialLayout.Spacing };
		topBox.Append (colorDisplayWidget);
		topBox.Append (colorSurfaceWidget);
		topBox.Append (colorSlidersWidget);

		// Main VBox
		Gtk.Box mainVbox = new () { Spacing = initialLayout.Spacing };
		mainVbox.SetOrientation (Gtk.Orientation.Vertical);
		mainVbox.Append (topBox);
		if (colorSwatchesWidget != null) {
			mainVbox.Append (colorSwatchesWidget);
		}

		Gtk.Box contentArea = this.GetContentAreaBox ();
		contentArea.SetAllMargins (initialLayout.Margins);
		contentArea.Append (mainVbox);

		// --- Initialization (Gtk.Window/Dialog) ---

		SetTitlebar (titleBar);
		Title = Translations.GetString (windowTitle);
		TransientFor = parentWindow;
		Modal = false; // As per original implementation
		IconName = Resources.Icons.ImageResizeCanvas;
		// Set small default size so it shrinks to fit content
		DefaultWidth = 1;
		DefaultHeight = 1;
		this.SetDefaultResponse (Gtk.ResponseType.Cancel);

		// Wayland transparency workaround (from original code)
		SetOpacity (0.995f);

		// --- Keyboard Shortcuts ---
		Gtk.EventControllerKey keyboard_gesture = Gtk.EventControllerKey.New ();
		keyboard_gesture.OnKeyPressed += KeyboardGesture_OnKeyPressed;
		AddController (keyboard_gesture);

		// --- Live Palette Mode Setup ---
		if (livePaletteMode) {
			// Handle focus changes for opacity and committing changes
			IsActivePropertyDefinition.Notify (this, ActiveWindowChangeHandler);
		}

		// Handle dialog response (cleanup ViewModel and handlers)
		OnResponse += ColorPickerDialog_OnResponse;

		// --- References to keep ---
		view_model = viewModel;
		top_box = topBox;
		color_swatches_widget = colorSwatchesWidget;
		shrink_button = shrinkButton;

		// --- Event Subscription ---
		// Subscribe the dialog shell to VM changes for layout updates.
		// Child widgets subscribe themselves independently for content updates.
		view_model.StateChanged += OnViewModelStateChanged;
	}

	// Public property required by the external interface
	public ColorPick Colors => view_model.State.Colors;

	// --- Event Handlers ---

	private void OnViewModelStateChanged (object? sender, EventArgs e)
	{
		UpdateView (view_model.State);
	}

	private void UpdateView (ColorPickerState state)
	{
		// Handles updates that affect the dialog shell itself (Layout and Visibility).
		LayoutSettings layout = state.Layout;

		// Update Margins and Spacing
		this.GetContentAreaBox ().SetAllMargins (layout.Margins);
		top_box.Spacing = layout.Spacing;

		// Update Swatch visibility
		if (color_swatches_widget != null) {
			// Show swatches if configured AND not in small mode.
			bool shouldBeVisible = state.ShowSwatches && !state.IsSmallMode;
			if (color_swatches_widget.Visible != shouldBeVisible) {
				color_swatches_widget.SetVisible (shouldBeVisible);
			}
		}

		// Update Shrink Button Icon
		shrink_button.SetIconName (
			state.IsSmallMode
			? Resources.StandardIcons.WindowMaximize
			: Resources.StandardIcons.WindowMinimize);

		// Force resize (necessary when switching between small/big mode)
		DefaultWidth = 1;
		DefaultHeight = 1;
	}

	private void OnShrinkButtonClicked (Gtk.Button button, EventArgs args)
	{
		view_model.ToggleSmallMode ();
	}

	private void OnOkButtonClicked (Gtk.Button button, EventArgs args)
	{
		Response ((int) Gtk.ResponseType.Ok);
		Close ();
	}

	private void OnCancelButtonClicked (Gtk.Button button, EventArgs args)
	{
		Response ((int) Gtk.ResponseType.Cancel);
		Close ();
	}

	private bool KeyboardGesture_OnKeyPressed (
		Gtk.EventControllerKey _,
		Gtk.EventControllerKey.KeyPressedSignalArgs e)
	{
		// 'X' key swaps colors globally
		if (e.GetKey ().Value == Gdk.Constants.KEY_x) {
			view_model.SwapColors ();
			return true;
		}
		return false;
	}

	// --- Live Palette Mode Handlers ---

	void ActiveWindowChangeHandler (object? _, NotifySignalArgs __)
	{
		if (!live_palette_mode) return;

		if (IsActive) {
			// On Focus Gain: Increase opacity (Wayland workaround part 2)
			SetOpacity (1.0f);
			return;
		}

		// On Focus Loss: Decrease opacity and commit changes
		SetOpacity (0.85f);
		view_model.CommitLivePaletteChanges ();
	}

	// --- Dialog Cleanup ---

	void ColorPickerDialog_OnResponse (Gtk.Dialog _, ResponseSignalArgs args)
	{
		Gtk.ResponseType response = (Gtk.ResponseType) args.ResponseId;

		// Only perform cleanup on actual closing responses (OK, Cancel, or DeleteEvent/Close button)
		if (response != Gtk.ResponseType.Cancel && response != Gtk.ResponseType.Ok && response != Gtk.ResponseType.DeleteEvent)
			return;

		// Cleanup ViewModel (unsubscribes from PaletteManager)
		view_model.Dispose ();

		// Cleanup Live Palette Handlers
		if (live_palette_mode) {
			IsActivePropertyDefinition.Unnotify (this, ActiveWindowChangeHandler);
		}

		// Ensure this handler doesn't run multiple times if multiple close signals are received.
		OnResponse -= ColorPickerDialog_OnResponse;
	}
}
