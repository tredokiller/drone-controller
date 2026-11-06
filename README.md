# UForce FPV Flight

A Betaflight-like FPV flight core for Unity 6. A drone flown with it behaves like a real FPV quad on Betaflight, and it is flown with a real radio (Radiomaster TX12 over USB). Keyboard and gamepad work for debugging.

The core has no dependency on Mirror, Zenject, HUD or UI. The only package dependency is the Input System, and only the `RadioInput` assembly uses it.

## Install

Add the package to `Packages/manifest.json`:

```json
"com.uforce.fpv-flight": "file:C:/Unity/Projects/Drone System/My project/Packages/com.uforce.fpv-flight"
```

Once the repository has a remote, use a git URL with a tag instead, for example `https://github.com/<org>/fpv-flight.git#v0.1.0`.

## What it simulates

Each `FixedUpdate`, one `RcCommand` goes through this chain:

1. **Radio link** (`RcLink`): latency, packet loss and hold-last-frame, driven by the project's `IRcLinkModel`.
2. **Failsafe**, as in Betaflight:
   - After 0.1 s without a frame, stage 1 starts: sticks centered, throttle low (or hold/fixed).
   - After `FailsafeDelay` (1.5 s by default), stage 2 drops the drone: disarm, motors stop, it falls.
3. **Arming**: arms only on the arm switch's off-to-on edge with throttle low. Any blocker at that moment means the switch must be cycled.
4. **Rates**: Betaflight RC rate, super rate and expo per axis. RC rate 1.00, super 0.70, expo 0 gives 666.7°/s at full stick.
5. **Flight mode**:
   - **Acro**: the stick sets the angular rate, and a released stick holds attitude.
   - **Angle**: the stick sets tilt (up to `AngleLimit`), and self-levelling feeds the same rate loop. Switching works in flight, even when inverted.
6. **Rate loop**: angular acceleration toward the commanded rate, with I-term relax and anti-windup. Authority drops with battery sag and payload.
7. **Airframe physics**:
   - Collective thrust along body up, with first-order motor lag and Betaflight-style idle.
   - Quadratic drag along the airflow, using the box-projected drag area. There is no speed limit.
   - Mass is frame + battery + payload (the warhead).
   - The battery drains and its voltage sags under load. Thrust scales with (loaded voltage / full-pack voltage)².
   - Wind and turbulence come from an `IWindProvider`.

Hover throttle, top speed, climb speed and flight time are **derived** from thrust, mass and drag; they are never set. The airframe and controller inspectors show them, and `FlightTelemetry.HoverThrottleEstimate` gives the live value.

## Quick start

1. Import **Basic Setup** from the Package Manager samples.
2. Run **Tools ▸ UForce ▸ FPV Flight ▸ Build Demo Scene** and press Play.
3. Keyboard: X arm, M flight mode, W/S throttle (non-centering), A/D yaw, arrow keys pitch and roll.
4. Radiomaster TX12:
   - Plug in USB and choose "USB Joystick (HID)" on the radio.
   - The demo actions use AETR: roll `stick/x`, pitch `stick/y`, yaw `rx`, throttle `z`, arm `ry`, mode `slider1`.
   - If your channel order or end points differ, run the calibration wizard in **Window ▸ UForce ▸ FPV Flight Monitor ▸ Calibration**.

## Integrating in a project

```csharp
// Update: read the radio once per frame.
_reader.Update(Time.unscaledDeltaTime);

// FixedUpdate: tick the flight core once per physics step.
_flight.Tick(_reader.Current, Time.fixedDeltaTime);
```

- `FpvFlightController` (on the Rigidbody) takes an `AirframeDefinition`, `BatteryDefinition`, `RatesProfile` and `FlightControllerProfile`. It configures the Rigidbody itself: mass, inertia, center of mass, damping 0 and max angular velocity.
- **Radio model:** implement `IRcLinkModel` and pass it to `SetLinkModel`. Return `RcLinkState` with quality, latency, packet loss probability and `IsLost`. A null model means an ideal link.
- **Weather:** implement `IWindProvider` and pass it to `SetWindProvider`, or use `ConstantWindProvider`.
- **Payload:** call `SetPayloadMass(kg)` for the warhead.
- **Battery:** call `SetBattery(definition, stateOfCharge)` to pick the pack and its charge.
- **Other controls:** `ForceDisarm()`, `ResetState()`, `SetArmingBlocked(bool)`.
- **Read-outs:** `Telemetry` (allocation-free) and `MotorOutput` for sound and propellers. Events: `OnArmed`, `OnDisarmed`, `OnFailsafeStageChanged`.
- **Input:** `RcInputReader` reads six `InputAction`s: Roll, Pitch, Yaw, Throttle, Arm, FlightMode.
  - It applies an `RcCalibrationProfile`: per-axis min, center, max, inversion and deadzone; a throttle bottom deadzone; switch positions; and the mode mapping.
  - A per-radio calibration saved through `IRcCalibrationStore` overrides the profile.
  - Inversion lives only in the profile. Do not add invert processors to the bindings.

## Assemblies

| Assembly | Depends on | Contents |
|---|---|---|
| `UForce.FpvFlight` | UnityEngine (Physics) | Flight core, configs, link and failsafe, wind interface |
| `UForce.FpvFlight.RadioInput` | Core, Input System | Radio reader, calibration, calibration session, TX12 layout fix |
| `UForce.FpvFlight.Editor` | Core, RadioInput | Derived-value inspectors, rates graph, FPV Flight Monitor, GameObject menu |
| `UForce.FpvFlight.Tests` | Core, RadioInput | EditMode tests (enable with `"testables": ["com.uforce.fpv-flight"]`) |

The OSD and video assemblies come next, in this package. They will compile only when their dependencies (uGUI/TMP, URP) are present.

## Samples

- **Basic Setup**: demo input actions, default profiles, `FpvFlightDemoDriver`, `FpvTelemetryOverlay` and the demo scene builder.
- **Airframe Presets**: drone types as data.
  - Strike FPV: Komandor's drone. 145 km/h empty and about 137 km/h with a 0.6 kg warhead, both derived.
  - Interceptor P1-SUN and Interceptor Sting, from the Interceptor Drones configs.
