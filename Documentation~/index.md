# UForce FPV Flight

See the package [README](../README.md) for setup and the signal chain. This page lists the conventions a project needs when it integrates the core.

## Axes and signs

- **`RcCommand`:**
  - Roll + rolls right.
  - Pitch + is stick forward (nose down).
  - Yaw + turns the nose right.
  - Throttle is 0..1.
- **Body axes** (Unity, left-handed):
  - x is right; positive rotation lowers the nose.
  - y is up; positive rotation turns the nose right.
  - z is forward; positive rotation rolls left.
  - `FlightTelemetry` rates use the radio convention, in deg/s.

## Timing

- Read the radio in `Update` with `RcInputReader.Update`.
- Call `FpvFlightController.Tick` once per `FixedUpdate`.
- The loop is stable at 50 Hz: the rate-loop time constant is clamped to at least 1.5 physics steps. No project-wide timestep change is needed.

## Failsafe defaults (Betaflight)

| Setting | Default | Betaflight equivalent |
|---|---|---|
| Rx loss timeout | 0.1 s | rx frame timeout |
| Failsafe delay (stage 1 duration) | 1.5 s | `failsafe_delay` 15 |
| Stage 1 throttle | Low | `rxfail` auto |
| Stage 2 | Drop | `failsafe_procedure` DROP |
| Recovery | Re-arm with a switch cycle | — |

Link loss while disarmed only blocks arming; it never latches a drop.

## Battery

- `AirframeDefinition.MaxThrust` is measured on a full, unloaded pack.
- In flight, thrust follows (loaded voltage / full-pack voltage)².
- Loaded voltage comes from the pack's internal resistance and the induced power the thrust needs (momentum theory ÷ `PropulsionEfficiency`), solved as a fixed point every step.
- Below 2% charge the ESCs brown out.

## Derived values

`AirframeMath` computes hover throttle, top level speed (with the tilt it needs), climb speed, free-fall speed, hover current and hover flight time. The inspectors show them; the flight loop never reads them.
