using NUnit.Framework;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Tests
{
	public sealed class RcLinkTests
	{
		private const float DELTA_TIME = 0.02f;

		[Test]
		public void WhenLinkIdeal_ThenFrameArrivesSameStep()
		{
			RcLink link = new(1u);
			RcCommand command = new(0.5f, 0f, 0f, 0.3f, true, FlightMode.Acro);

			link.Tick(command, RcLinkState.Ideal, DELTA_TIME, 0f);

			Assert.IsTrue(link.HasFrame);
			Assert.That(link.Current.Roll, Is.EqualTo(0.5f));
			Assert.That(link.TimeSinceLastFrame, Is.EqualTo(0f));
		}

		[Test]
		public void WhenLatencySet_ThenFrameArrivesLate()
		{
			RcLink link = new(1u);
			RcLinkState state = new(1f, 0.1f, 0f, false);
			RcCommand command = new(0f, 0f, 0f, 0.7f, false, FlightMode.Acro);

			for (int step = 0; step < 5; step++)
			{
				link.Tick(command, state, DELTA_TIME, 0f);
				Assert.IsFalse(link.HasFrame, $"Frame arrived after {step + 1} steps.");
			}

			link.Tick(command, state, DELTA_TIME, 0f);
			Assert.IsTrue(link.HasFrame);
			Assert.That(link.Current.Throttle, Is.EqualTo(0.7f));
		}

		[Test]
		public void WhenPacketsDropped_ThenDeliveryRateMatchesProbability()
		{
			RcLink link = new(3u);
			RcLinkState state = new(0.5f, 0f, 0.3f, false);
			int delivered = 0;
			const int steps = 10000;

			for (int step = 0; step < steps; step++)
			{
				link.Tick(RcCommand.Neutral, state, DELTA_TIME, 0f);
				if (link.TimeSinceLastFrame == 0f)
				{
					delivered++;
				}
			}

			Assert.That(delivered / (float)steps, Is.EqualTo(0.7f).Within(0.03f));
		}

		[Test]
		public void WhenLinkLost_ThenLastFrameIsHeld()
		{
			RcLink link = new(1u);
			RcCommand command = new(0.2f, -0.4f, 0.1f, 0.6f, true, FlightMode.Angle);
			link.Tick(command, RcLinkState.Ideal, DELTA_TIME, 0f);

			for (int step = 0; step < 10; step++)
			{
				link.Tick(RcCommand.Neutral, RcLinkState.Lost, DELTA_TIME, 0f);
			}

			Assert.That(link.Current.Pitch, Is.EqualTo(-0.4f));
			Assert.That(link.Current.Mode, Is.EqualTo(FlightMode.Angle));
			Assert.That(link.TimeSinceLastFrame, Is.EqualTo(0.2f).Within(1e-4f));
		}

		[Test]
		public void WhenLatencyJumps_ThenNoGapLooksLikeRxLoss()
		{
			RcLink link = new(1u);
			RcLinkState low = new(1f, 0.04f, 0f, false);
			RcLinkState high = new(0.2f, 0.44f, 0f, false);

			for (int step = 0; step < 20; step++)
			{
				link.Tick(RcCommand.Neutral, low, DELTA_TIME, 1f);
			}

			float worstGap = 0f;
			for (int step = 0; step < 100; step++)
			{
				link.Tick(RcCommand.Neutral, high, DELTA_TIME, 1f);
				worstGap = Mathf.Max(worstGap, link.TimeSinceLastFrame);
			}

			Assert.That(worstGap, Is.LessThan(0.1f));
			Assert.That(link.Latency, Is.EqualTo(0.44f).Within(1e-3f));
		}
	}
}
