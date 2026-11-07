using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	// Radio calibration for a whole application, ported from Interceptor's ControllerCalibrationService: the per-radio store,
	// the "calibration was offered" flag, hot-plug detection of radios without a calibration, and the wizard session.
	// UI-free: a menu screen starts the session, ticks it and calls Continue / Skip / Save. Input readers should be
	// suspended while the session is open (IsSuspended, SuspendedChanged).
	public sealed class RcCalibrationService : IDisposable
	{
		private const string OFFERED_KEY = "UForce.FpvFlight.RcCalibration.Offered";

		private readonly RcCalibrationProfile r_defaults;
		private readonly IRcCalibrationStore r_store;

		public RcCalibrationService(RcCalibrationProfile defaults, IRcCalibrationStore store)
		{
			r_defaults = defaults;
			r_store = store ?? throw new ArgumentNullException(nameof(store));
			InputSystem.onDeviceChange += HandleDeviceChange;
		}

		public event Action<InputDevice> UncalibratedControllerConnected;
		public event Action<RcCalibration> Changed;
		public event Action<bool> SuspendedChanged;

		public RcCalibrationSession Session { get; private set; }
		public bool IsSuspended => Session != null;
		public IRcCalibrationStore Store => r_store;

		public bool IsCalibrationOffered
		{
			get => PlayerPrefs.GetInt(OFFERED_KEY, 0) == 1;
			set
			{
				PlayerPrefs.SetInt(OFFERED_KEY, value ? 1 : 0);
				PlayerPrefs.Save();
			}
		}

		// The first connected radio joystick, or null.
		public InputDevice Controller
		{
			get
			{
				foreach (InputDevice device in InputSystem.devices)
				{
					if (device.added && RcDevices.IsRadio(device))
					{
						return device;
					}
				}

				return null;
			}
		}

		public bool IsCalibrated(InputDevice device) => device != null && r_store.TryLoad(RcDevices.GetDeviceKey(device), out _);

		public RcCalibration Resolve(InputDevice device)
		{
			if (r_defaults)
			{
				return r_defaults.Resolve(device, r_store);
			}

			return device != null && r_store.TryLoad(RcDevices.GetDeviceKey(device), out RcCalibration saved) ? saved : new RcCalibration();
		}

		// A calibrated radio opens on the summary for review; any other starts the full wizard.
		public RcCalibrationSession StartSession()
		{
			InputDevice controller = Controller;
			RcCalibrationSession session = controller != null && IsCalibrated(controller)
				? RcCalibrationSession.OpenExisting(Resolve(controller), controller)
				: new RcCalibrationSession(r_defaults ? r_defaults.Calibration : null);
			SetSession(session);
			return session;
		}

		// Starts the full wizard again, keeping the session open.
		public RcCalibrationSession RestartSession()
		{
			SetSession(new RcCalibrationSession(r_defaults ? r_defaults.Calibration : null));
			return Session;
		}

		public void CancelSession() => SetSession(null);

		public bool SaveSession()
		{
			if (Session == null || !Session.IsComplete || string.IsNullOrEmpty(Session.Result.DeviceKey))
			{
				return false;
			}

			RcCalibration result = Session.Result.Clone();
			r_store.Save(result);
			IsCalibrationOffered = true;
			SetSession(null);
			Changed?.Invoke(result);
			return true;
		}

		// Call every frame while a session is open. A radio unplugged mid-wizard restarts it, as in Interceptor.
		public void Tick()
		{
			if (Session == null)
			{
				return;
			}

			if (Session.IsDeviceLost)
			{
				RestartSession();
				return;
			}

			Session.Tick();
		}

		public void Dispose()
		{
			InputSystem.onDeviceChange -= HandleDeviceChange;
			SetSession(null);
		}

		private void SetSession(RcCalibrationSession session)
		{
			bool wasSuspended = IsSuspended;
			Session = session;
			if (wasSuspended != IsSuspended)
			{
				SuspendedChanged?.Invoke(IsSuspended);
			}
		}

		private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
		{
			if (change == InputDeviceChange.Added && RcDevices.IsRadio(device) && !IsCalibrated(device))
			{
				UncalibratedControllerConnected?.Invoke(device);
			}
		}
	}
}
