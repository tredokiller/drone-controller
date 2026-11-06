# Changelog

## [0.1.0] - 2026-11-06

### Added

- Flight core:
  - Betaflight rates (RC rate, super rate, expo).
  - Acro and angle modes on one rate loop.
  - Motor lag and idle.
  - Quadratic drag along the airflow.
  - Battery drain and voltage sag.
  - Wind and turbulence through `IWindProvider`.
- Radio link through `IRcLinkModel`: latency, packet loss and hold-last-frame.
- Betaflight-like failsafe and arming:
  - Stage 1 centers the sticks, then stage 2 drops the drone.
  - Arming happens only on the arm switch edge with throttle low.
- `RadioInput`:
  - Radiomaster TX12 and other joystick radios, with calibration profiles and per-radio stored calibration.
  - The calibration session (wizard logic) and the TX12 slider layout fix, both ported from Interceptor Drones.
  - Keyboard and gamepad debug input.
- Editor:
  - Derived values (hover throttle, top speed, climb, flight time, full-throttle sag).
  - Rates graph.
  - The FPV Flight Monitor (radio channels, telemetry, calibration wizard).
  - A GameObject ▸ UForce ▸ FPV Drone menu item.
- Samples: Basic Setup and Airframe Presets (strike FPV, two interceptors).
- EditMode tests: rates, link, failsafe and arming, battery, derived math, and Rigidbody flight checks.
