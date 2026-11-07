# Changelog

## [Unreleased]

### Added

- FPV video (`Video`) and the Interceptor Drones OSD (`Osd`), ported from Interceptor Drones. `FpvVideoNoSignal` draws the no-signal static into a video output.
- Radio channels as in Interceptor Drones' TX12 layout: payload arm (SF), detonate (SD), camera tilt (SC, 3 positions), thermal (SA), with gamepad and keyboard bindings. `RcAuxState`, press counters and `SwitchChanged`.
- Horizon flight mode; the 3-position mode switch defaults to angle / horizon / acro.
- Arming inhibits: payload armed, crashed, held after a respawn, external. Arming is blocked at 1% battery or less.
- Calibration wizard for every channel, with cancel, restart, open-existing and a live preview; `RcCalibrationService` for menu screens.
- Camera tilt (`FpvCameraTilt`), propellers (`FpvPropellers`), crash detection with landing tolerance (`FpvCrashDetector`) and `FpvFlightController.Recover`.
- `Audio`: motor sound and wind masking (DroneAudio), the onboard microphone (PortableAudio), touch and hit sounds.
- `RotorWash`: dust, grit and water mist from the propeller wash, grass-bending globals; GeoWorld 1.20+ bridge for water, ice, puddles, snow, rain, leaves and ripples.
- Drone types: strike FPV, P1-SUN, Sting, Zorelit v5 and SPYS, with models, materials, prefabs and the `FpvDroneType` asset.
- Interceptor Drones' flight model (`InterceptorFlightProfile`, `InterceptorFlightModel`) as an alternative to the Betaflight physics; the four interceptor types use it with their Interceptor presets.
- Demo: switch drone types (1-5), respawn (R), every channel routed.
- Pilot's OSD layout, after FPV Defence Simulator:
  - modes standard, own and off;
  - show, hide and move elements; bold font; opacity;
  - settings in `PlayerPrefsFpvOsdSettingsStore`, followed by every `FpvOsdView`.
- Betaflight-style OSD extras, hidden by default: throttle, flight time, current, mAh, pack voltage, charge, link quality, vertical speed, pitch and roll. `FpvOsdData` gains `Pitch` and `Roll`.
- OSD editing:
  - `FpvOsdEditorSession` and `FpvOsdSnapping` work without UI.
  - `FpvOsdEditorScreen` (`Osd.UI`) is the ready UI Toolkit editor; the demo opens it with O and cycles the mode with Shift+O.
  - `FpvOsdPreview` renders the OSD for menus without a drone.

### Changed

- Airframe presets moved from the Airframe Presets sample into `Runtime/DroneTypes`; the sample is gone.
- Gamepad throttle follows the stick position, as in Interceptor Drones.
- `FpvOsdView`:
  - the payload line and the warning now turn their text on and off, so a layout can hide them;
  - `SetSpeedVisible` is removed, because a layout does that;
  - without a camera, the horizon follows the telemetry attitude.

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
