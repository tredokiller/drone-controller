using System;

namespace UForce.FpvFlight.Audio
{
	// Butterworth (Q = 1/sqrt(2)) high- or low-pass biquad, ported from PortableAudio (TIR) BiquadFilter.
	internal sealed class FpvBiquadFilter
	{
		private readonly double r_b0;
		private readonly double r_b1;
		private readonly double r_b2;
		private readonly double r_a1;
		private readonly double r_a2;

		private double _z1;
		private double _z2;

		public FpvBiquadFilter(int sampleRate, double frequency, bool isHighPass)
		{
			double omega = 2d * Math.PI * frequency / sampleRate;
			double cosine = Math.Cos(omega);
			double alpha = Math.Sin(omega) / Math.Sqrt(2d);
			double a0 = 1d + alpha;

			r_b0 = (1d + (isHighPass ? cosine : -cosine)) / (2d * a0);
			r_b1 = (isHighPass ? -2d : 2d) * r_b0;
			r_b2 = r_b0;
			r_a1 = -2d * cosine / a0;
			r_a2 = (1d - alpha) / a0;
		}

		public double Process(double input)
		{
			double output = r_b0 * input + _z1;
			_z1 = r_b1 * input - r_a1 * output + _z2;
			_z2 = r_b2 * input - r_a2 * output;

			if (Math.Abs(_z1) < FpvMicrophoneDsp.DENORMAL_THRESHOLD)
			{
				_z1 = 0d;
			}

			if (Math.Abs(_z2) < FpvMicrophoneDsp.DENORMAL_THRESHOLD)
			{
				_z2 = 0d;
			}

			return output;
		}

		public void Reset()
		{
			_z1 = 0d;
			_z2 = 0d;
		}
	}
}
