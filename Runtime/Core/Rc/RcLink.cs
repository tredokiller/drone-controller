using UnityEngine;

namespace UForce.FpvFlight.Rc
{
	// Receiver side of the radio link: frames arrive after the link latency, some are dropped,
	// and the receiver holds the last valid frame like a real RX does.
	public sealed class RcLink
	{
		private const int CAPACITY = 512;
		private const double DELIVERY_EPSILON = 1e-6;

		private readonly RcCommand[] r_commands = new RcCommand[CAPACITY];
		private readonly double[] r_deliveryTimes = new double[CAPACITY];
		private readonly uint r_seed;

		private DeterministicRandom _random;
		private int _head;
		private int _count;
		private double _time;
		private double _lastDeliveryTime;
		private float _latency;

		public RcLink(uint seed)
		{
			r_seed = seed;
			Reset();
		}

		public RcCommand Current { get; private set; }
		public bool HasFrame { get; private set; }
		public float TimeSinceLastFrame { get; private set; }
		public RcLinkState State { get; private set; }
		public float Latency => _latency;

		public void Reset()
		{
			_random = new DeterministicRandom(r_seed);
			_head = 0;
			_count = 0;
			_time = 0.0;
			_lastDeliveryTime = double.NegativeInfinity;
			_latency = 0f;
			Current = RcCommand.Neutral;
			HasFrame = false;
			TimeSinceLastFrame = float.PositiveInfinity;
			State = RcLinkState.Ideal;
		}

		// latencySlewRate limits how fast latency may grow (seconds per second), so a step in the
		// reported latency stretches frame spacing instead of opening a gap that looks like rx loss.
		public void Tick(in RcCommand command, in RcLinkState state, float deltaTime, float latencySlewRate)
		{
			_time += deltaTime;
			State = state;
			_latency = latencySlewRate > 0f
				? Mathf.MoveTowards(_latency, state.LatencySeconds, latencySlewRate * deltaTime)
				: state.LatencySeconds;

			if (!state.IsLost && _random.NextFloat() >= state.PacketLossProbability)
			{
				double deliveryTime = System.Math.Max(_time + _latency, _lastDeliveryTime);
				Enqueue(command, deliveryTime);
				_lastDeliveryTime = deliveryTime;
			}

			bool isDelivered = false;
			while (_count > 0 && r_deliveryTimes[_head] <= _time + DELIVERY_EPSILON)
			{
				Current = r_commands[_head];
				_head = (_head + 1) % CAPACITY;
				_count--;
				isDelivered = true;
			}

			if (isDelivered)
			{
				HasFrame = true;
				TimeSinceLastFrame = 0f;
			}
			else if (HasFrame)
			{
				TimeSinceLastFrame += deltaTime;
			}
		}

		private void Enqueue(in RcCommand command, double deliveryTime)
		{
			if (_count == CAPACITY)
			{
				_head = (_head + 1) % CAPACITY;
				_count--;
			}

			int index = (_head + _count) % CAPACITY;
			r_commands[index] = command;
			r_deliveryTimes[index] = deliveryTime;
			_count++;
		}
	}
}
