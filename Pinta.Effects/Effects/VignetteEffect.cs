/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Lehonti Ramos                                           //
/////////////////////////////////////////////////////////////////////////////////

// Copyright (c) 2007,2008 Ed Harvey
//
// MIT License: http://www.opensource.org/licenses/mit-license.php
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class VignetteEffect : BaseEffect
{
	public override string Icon
		=> Resources.Icons.EffectsPhotoVignette;

	public override bool IsTileable
		=> true;

	public override string Name
		// Translators: The vignette effect darkens the outer edges of an image, which fade into an unchanged circular area in the center (or at some other point chosen by the user), similar to what is seen during the closing scene in old cartoons
		=> Translations.GetString ("Vignette");

	public override string EffectMenuCategory
		=> Translations.GetString ("Photo");

	public override bool IsConfigurable
		=> true;

	public VignetteData Data
		=> (VignetteData) EffectData!;  // NRT - Set in constructor

	private readonly IChromeService chrome;
	private readonly IWorkspaceService workspace;
	public VignetteEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		workspace = services.GetService<IWorkspaceService> ();
		EffectData = new VignetteData ();
	}

	public override Task<bool> LaunchConfiguration ()
		=> chrome.LaunchSimpleEffectDialog (this, workspace);

	private readonly record struct VignetteSettings (
		Size CanvasSize,
		double RadiusR,
		double Amount,
		PointI CenterOffset,
		double VignetteRLinear,
		double VignetteGLinear,
		double VignetteBLinear,
		double VignetteAlpha);

	private VignetteSettings CreateSettings (ImageSurface src)
	{
		VignetteData data = Data;
		Size canvasSize = src.GetSize ();
		double r1 = Math.Max (canvasSize.Width, canvasSize.Height) * 0.5d;
		double r2 = r1 * Convert.ToDouble (data.RadiusPercentage) / 100d;
		double amount = data.Amount;
		Color color = data.VignetteColor;
		return new (
			CanvasSize: canvasSize,
			RadiusR: Math.PI / (8 * (r2 * r2)),
			Amount: amount,
			CenterOffset: data.Offset,
			VignetteRLinear: SrgbUtility.ToLinear (color.R),
			VignetteGLinear: SrgbUtility.ToLinear (color.G),
			VignetteBLinear: SrgbUtility.ToLinear (color.B),
			VignetteAlpha: color.A);
	}

	// Algorithm code ported from PDN
	protected override void Render (ImageSurface source, ImageSurface destination, RectangleI roi)
	{
		VignetteSettings settings = CreateSettings (source);
		ReadOnlySpan<ColorBgra> sourceData = source.GetReadOnlyPixelData ();
		Span<ColorBgra> destinationData = destination.GetPixelData ();
		foreach (var pixel in Tiling.GeneratePixelOffsets (roi, settings.CanvasSize))
			destinationData[pixel.memoryOffset] = GetFinalPixelColor (
				settings,
				sourceData[pixel.memoryOffset],
				pixel.coordinates - settings.CenterOffset);
	}

	private static ColorBgra GetFinalPixelColor (in VignetteSettings settings, in ColorBgra originalColor, in PointI fromCenter)
	{
		double shapeFactor = GetVignetteShapeFactor (settings, fromCenter);
		double vignetteIntensity = 1d - shapeFactor;
		double effectiveOpacity = settings.Amount * vignetteIntensity * settings.VignetteAlpha;

		if (effectiveOpacity <= 0)
			return originalColor;

		double originalRLinear = SrgbUtility.ToLinear (originalColor.R);
		double originalGLinear = SrgbUtility.ToLinear (originalColor.G);
		double originalBLinear = SrgbUtility.ToLinear (originalColor.B);

		double opacityInverse = 1d - effectiveOpacity;

		double resultRLinear = (originalRLinear * opacityInverse) + (settings.VignetteRLinear * effectiveOpacity);
		double resultGLinear = (originalGLinear * opacityInverse) + (settings.VignetteGLinear * effectiveOpacity);
		double resultBLinear = (originalBLinear * opacityInverse) + (settings.VignetteBLinear * effectiveOpacity);

		return ColorBgra.FromBgra (
			r: (byte) (0.5 + (255 * SrgbUtility.ToSrgbClamped (resultRLinear))),
			g: (byte) (0.5 + (255 * SrgbUtility.ToSrgbClamped (resultGLinear))),
			b: (byte) (0.5 + (255 * SrgbUtility.ToSrgbClamped (resultBLinear))),
			a: originalColor.A);
	}

	private static double GetVignetteShapeFactor (in VignetteSettings settings, in PointI fromCenter)
	{
		double d = fromCenter.MagnitudeSquared () * settings.RadiusR;
		if (d > Math.PI) return 0d;
		double factor = Math.Cos (d);
		if (factor <= 0) return 0d;
		double factor2 = factor * factor;
		double factor4 = factor2 * factor2;
		return factor4;
	}
}

public sealed class VignetteData : EffectData
{
	[Caption ("Offset")]
	public PointI Offset { get; set; }

	// Translators: This refers to how big the radius is as a percentage of the image's dimensions
	[Caption ("Radius Percentage")]
	[MinimumValue (10), MaximumValue (400)]
	public int RadiusPercentage { get; set; } = 50;

	[MinimumValue (0), MaximumValue (1)]
	[Caption ("Strength")]
	public double Amount { get; set; } = 1;

	[Caption ("Vignette Color")]
	public Color VignetteColor { get; set; } = Color.Black;
}
