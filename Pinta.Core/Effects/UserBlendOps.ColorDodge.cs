using System;
using System.Runtime.CompilerServices;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class ColorDodgeBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "ColorDodge";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// Color Dodge lightens the bottom layer based on the top layer's darkness.
			//
			// - Blending with black has no effect
			// - Blending with white results in white

			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				throw new NotImplementedException ();
			}
		}
	}
}
