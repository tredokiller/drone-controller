using System.Collections.Generic;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Tests
{
	// Fresh ScriptableObject instances with the package defaults. Dispose destroys them.
	public sealed class TestAssets : System.IDisposable
	{
		private readonly List<Object> r_created = new();

		public TestAssets()
		{
			Airframe = Create<AirframeDefinition>();
			Battery = Create<BatteryDefinition>();
			Rates = Create<RatesProfile>();
			Profile = Create<FlightControllerProfile>();
		}

		public AirframeDefinition Airframe { get; }
		public BatteryDefinition Battery { get; }
		public RatesProfile Rates { get; }
		public FlightControllerProfile Profile { get; }

		public T Create<T>() where T : ScriptableObject
		{
			T instance = ScriptableObject.CreateInstance<T>();
			r_created.Add(instance);
			return instance;
		}

		public FpvFlightSimulation CreateSimulation(bool hasBattery = true) =>
			new(Airframe, hasBattery ? Battery : null, Rates, Profile, 7u);

		public void Dispose()
		{
			foreach (Object created in r_created)
			{
				Object.DestroyImmediate(created);
			}

			r_created.Clear();
		}
	}

	public sealed class FakeLinkModel : IRcLinkModel
	{
		public RcLinkState State = RcLinkState.Ideal;

		public RcLinkState Sample() => State;
	}
}
