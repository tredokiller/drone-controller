using NUnit.Framework;
using UForce.FpvFlight.Rates;

namespace UForce.FpvFlight.Tests
{
	public sealed class BetaflightRatesTests
	{
		[Test]
		public void WhenDefaultRatesAtFullStick_ThenRateIsAbout667()
		{
			Assert.That(BetaflightRates.Evaluate(1f, 1f, 0.7f, 0f), Is.EqualTo(666.67f).Within(0.5f));
			Assert.That(BetaflightRates.Evaluate(-1f, 1f, 0.7f, 0f), Is.EqualTo(-666.67f).Within(0.5f));
		}

		[Test]
		public void WhenStickCentered_ThenRateIsZero()
		{
			Assert.That(BetaflightRates.Evaluate(0f, 1f, 0.7f, 0.3f), Is.EqualTo(0f).Within(1e-4f));
		}

		[Test]
		public void WhenHalfStick_ThenRateMatchesConfigurator()
		{
			// Betaflight Configurator: RC 1.00, super 0.70, expo 0.00 at half stick = 153.8 deg/s.
			Assert.That(BetaflightRates.Evaluate(0.5f, 1f, 0.7f, 0f), Is.EqualTo(153.85f).Within(0.1f));
			// With expo 0.50 the center softens: 86.5 deg/s.
			Assert.That(BetaflightRates.Evaluate(0.5f, 1f, 0.7f, 0.5f), Is.EqualTo(86.54f).Within(0.1f));
			// Without super rate the curve is linear: 200·RC rate·stick.
			Assert.That(BetaflightRates.Evaluate(1f, 1f, 0f, 0f), Is.EqualTo(200f).Within(0.01f));
		}

		[Test]
		public void WhenExpoSet_ThenFullStickRateIsUnchanged()
		{
			Assert.That(BetaflightRates.Evaluate(1f, 1f, 0.7f, 0.8f), Is.EqualTo(666.67f).Within(0.5f));
		}

		[Test]
		public void WhenRcRateAboveTwo_ThenIncrementalBoostApplies()
		{
			Assert.That(BetaflightRates.Evaluate(1f, 2.5f, 0f, 0f), Is.EqualTo(1954f).Within(0.5f));
		}

		[Test]
		public void WhenRateExceedsLimit_ThenRateIsClamped()
		{
			Assert.That(BetaflightRates.Evaluate(1f, 2.55f, 0.75f, 0f), Is.EqualTo(BetaflightRates.DEFAULT_RATE_LIMIT).Within(0.01f));
			Assert.That(BetaflightRates.Evaluate(1f, 1f, 0.7f, 0f, 500f), Is.EqualTo(500f).Within(0.01f));
		}

		[Test]
		public void WhenProfileHasDefaults_ThenEveryAxisReachesAbout667()
		{
			using TestAssets assets = new();
			Assert.That(assets.Rates.MaxRollRate, Is.EqualTo(666.67f).Within(0.5f));
			Assert.That(assets.Rates.MaxPitchRate, Is.EqualTo(666.67f).Within(0.5f));
			Assert.That(assets.Rates.MaxYawRate, Is.EqualTo(666.67f).Within(0.5f));
		}
	}
}
