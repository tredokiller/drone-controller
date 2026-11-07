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
3. **Arming**: arms only on the arm switch's off-to-on edge with throttle low. Any blocker at that moment means the switch must be cycled. Blockers: throttle not low, rx loss, failsafe, battery at 1% or less, and the project's inhibits: payload armed (as in Interceptor Drones), crashed, held after a respawn, or any other reason.
4. **Rates**: Betaflight RC rate, super rate and expo per axis. RC rate 1.00, super 0.70, expo 0 gives 666.7°/s at full stick.
5. **Flight mode**, on the 3-position switch SB by default (Angle / Horizon / Acro):
   - **Acro**: the stick sets the angular rate, and a released stick holds attitude.
   - **Angle**: the stick sets tilt (up to `AngleLimit`), and self-levelling feeds the same rate loop. Switching works in flight, even when inverted.
   - **Horizon**: acro rates with a pull towards level that fades out with stick deflection (Betaflight `horizon_transition`, 0.75), so the drone levels at centre stick and still flips at full stick.
6. **Rate loop**: angular acceleration toward the commanded rate, with I-term relax and anti-windup. Authority drops with battery sag and payload.
7. **Airframe physics**:
   - Collective thrust along body up, with first-order motor lag and Betaflight-style idle.
   - Quadratic drag along the airflow, using the box-projected drag area. There is no speed limit.
   - Mass is frame + battery + payload (the warhead).
   - The battery drains and its voltage sags under load. Thrust scales with (loaded voltage / full-pack voltage)².
   - Wind and turbulence come from an `IWindProvider`.

### Flight models

Two physics models share the radio, link, failsafe, arming, battery, telemetry and OSD:

- **Betaflight physics** (default): the chain above. Top speed, hover and climb come out of thrust, mass and drag.
- **Interceptor Drones' flight model**: assign an `InterceptorFlightProfile` on `FpvFlightController` (or the drone type). It is Interceptor's `DroneMovementController` Manual mode step for step:
  - linear stick rates (full stick = max rate) with an almost instant response (sensitivity 50);
  - an optional release brake;
  - thrust = throttle × `ThrottlePower` × weight, with no motor lag and no aerodynamic drag;
  - a horizontal thrust assist past 15° of tilt;
  - Rigidbody linear and angular damping;
  - 1.5 g of extra gravity at zero throttle;
  - hard limits on speed and body rates.

  The P1-SUN, Sting, Zorelit v5 and SPYS types use it with their Interceptor presets. Angle and horizon modes work on top of it.

Hover throttle, top speed, climb speed and flight time are **derived** from thrust, mass and drag; they are never set. The airframe and controller inspectors show them, and `FlightTelemetry.HoverThrottleEstimate` gives the live value.

## Quick start

1. Import **Basic Setup** from the Package Manager samples.
2. Run **Tools ▸ UForce ▸ FPV Flight ▸ Build Demo Scene** and press Play. The builder adds the video renderer feature to the project's URP renderers and imports TMP Essential Resources when they are missing.
3. Keys 1–5 pick the drone type (strike FPV, P1-SUN, Sting, Zorelit v5, SPYS); R puts the drone back on the ground.
4. Gamepad and keyboard work as in Interceptor Drones:
   - Gamepad: left stick throttle and yaw, right stick pitch and roll. Throttle follows the stick position: centred is 50%, fully down is 0%.
   - Keyboard: W/S throttle, A/D yaw, arrow keys pitch and roll. The keys move virtual sticks that spring back to centre.
   - Arm: X or gamepad North asks to arm. The drone arms once throttle is fully down; press again to disarm. Mode: M or gamepad West cycles angle, horizon, acro.
   - Camera tilt: V or left trigger. Payload arm: B or LB. Detonate: G or right trigger. Thermal: T or RB.
5. Radiomaster TX12 ("USB Joystick (HID)" on the radio), laid out as in Interceptor Drones:

   | Channel | Control | TX12 |
   |---|---|---|
   | Roll, pitch, yaw, throttle | `stick/x`, `stick/y`, `rx`, `z` | sticks (AETR) |
   | Arm | `ry` | SE |
   | Payload arm | `rz` | SF |
   | Detonate | `button2` | SD (button) |
   | Camera tilt | `slider` | SC, 3 positions (interceptors) |
   | Flight mode | `slider1` | SB, 3 positions: angle, horizon, acro |
   | Thermal | `trigger` | SA (button) |

   If your channels or end points differ, run the calibration wizard (**Window ▸ UForce ▸ FPV Flight Monitor ▸ Calibration**, or your own screen on `RcCalibrationService`).

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
- **Other controls:** `ForceDisarm()`, `ResetState()`, `SetArmingBlocked(bool)`, `SetArmingInhibit(ArmingInhibit, bool)` (payload armed, crashed, external), `Recover(position, rotation)` (back on the ground, held 1 s and until throttle is down, as Interceptor's respawn).
- **Read-outs:** `Telemetry` (allocation-free) and `MotorOutput` for sound and propellers. Events: `OnArmed`, `OnDisarmed`, `OnFailsafeStageChanged`. `FpvFlightController` is an `IFpvMotorSource`; a network drone on another machine can implement it from synced motor power.
- **Input:** `RcInputReader` reads the flight actions (Roll, Pitch, Yaw, Throttle, Arm, FlightMode) and the optional PayloadArm, PayloadDetonate, CameraTilt and Thermal.
  - `Current` is the `RcCommand` for the flight core; `Aux` is an `RcAuxState`: payload armed, camera tilt position, and press counters for detonate, camera tilt and thermal (they only grow, so FixedUpdate and Update both see each press once). `SwitchChanged` reports every switch move.
  - It applies an `RcCalibrationProfile`: per-axis min, center, max, inversion and deadzone; a throttle bottom deadzone; switch positions (latching or button); and the mode mapping.
  - A per-radio calibration saved through `IRcCalibrationStore` overrides the profile.
  - Inversion lives only in the profile. Do not add invert processors to the bindings.
- **Calibration screen:** `RcCalibrationService` owns the store, the "offered" flag, hot-plug detection of radios without a calibration (`UncalibratedControllerConnected`) and the wizard (`StartSession`, `RestartSession`, `CancelSession`, `SaveSession`, `Tick`). The session is UI-free: step, detected control, progress, `CanContinue`, `Continue`, `Skip`, `ToggleInvert` and a live `Preview` of sticks and switches, so any UI (uGUI, UI Toolkit, a web page) can draw it. Suspend readers (`SetSuspended`) while it is open.

## Assemblies

| Assembly | Depends on | Contents |
|---|---|---|
| `UForce.FpvFlight` | UnityEngine (Physics) | Flight core, configs, link and failsafe, wind interface, drone types, camera tilt, propellers, crash detection |
| `UForce.FpvFlight.Audio` | Core | Motor sound (Interceptor's DroneAudio), the onboard microphone (PortableAudio), touch and hit sounds |
| `UForce.FpvFlight.RotorWash` | Core | Rotor downwash: dust and grit from the ground, water mist, grass-bending shader globals |
| `UForce.FpvFlight.RotorWash.GeoWorld` | RotorWash, GeoWorld 1.20+, Enviro 3 | Water, ice, puddles, snow, rain, leaves and ripples from a GeoWorld world |
| `UForce.FpvFlight.DroneTypes.Editor` | Core, Audio, RotorWash, Video, Osd | Rebuilds the drone type prefabs and assets (**Tools ▸ UForce ▸ FPV Flight ▸ Build Drone Types**) |
| `UForce.FpvFlight.RadioInput` | Core, Input System | Radio reader, calibration, calibration session, TX12 layout fix |
| `UForce.FpvFlight.Editor` | Core, RadioInput | Derived-value inspectors, rates graph, FPV Flight Monitor, GameObject menu |
| `UForce.FpvFlight.Tests` | Core, RadioInput | EditMode tests (enable with `"testables": ["com.uforce.fpv-flight"]`) |
| `UForce.FpvFlight.Video` | Core, URP 17 | FPV video ported from Interceptor Drones: fisheye, auto exposure, glow and sun veil, Digital/Analog development, lens water and snow, radio interference and static, night and thermal cameras, camera shake |
| `UForce.FpvFlight.Video.Editor` | Video | Keeps URP's HDR grading variant in player builds |
| `UForce.FpvFlight.Osd` | Core, Video, uGUI/TMP | The interceptor's FPV OSD, burned into the video through its own camera |
| `UForce.FpvFlight.Osd.UI` | Osd, UI Toolkit | The ready OSD editor screen (`FpvOsdEditorScreen`) |
| `UForce.FpvFlight.Osd.Editor` | Osd | Rebuilds the OSD prefab and `FpvOsdDefaults` (**Tools ▸ UForce ▸ FPV Flight ▸ Build OSD Prefab**) |
| `UForce.FpvFlight.Osd.Tests` | Osd | EditMode tests of the OSD layout, editing session and snapping |

`Video` compiles only when URP is installed and `Osd` only when URP and uGUI are, through `versionDefines`. A project without them still gets the flight core.

## FPV video

1. Add `FpvVideoRendererFeature` to the URP renderer of the drone camera.
2. Put `FpvVideoCamera` on the drone camera and give it a profile: `Runtime/Video/Profiles/FpvVideo_Digital` or `FpvVideo_Analog`. These are Interceptor's "Digital FPV" and "Analog" presets.
3. Optional: `FpvLensRain` (with an `IFpvWeatherProvider` for rain, snow and the sun) and `FpvCameraShake` on the same camera.

The video is drawn into the camera's own target after URP post-processing, so a RenderTexture output (a web page, a second monitor) gets the finished picture.

- **Radio link:** `SetSignalLoss(0..1)` drives interference, then static.
- **Camera type:** `SetMode` picks Day, Night (black and white), ThermalWhiteHot or ThermalColor. Thermal cameras show objects with `FpvThermalSignature` as hot. The heat mask is drawn per camera, so other cameras are not affected.
- **Volume:** `_volumeLayer` gives the camera its own URP volume for the profile's colour adjustments.
- **HDR:** while a profile is on, URP colour grading runs in HDR, as in Interceptor, so the exposure keeps clouds in a bright sky.
- **No signal:** `FpvVideoNoSignal.Render(renderTexture)` draws what goggles show without a picture: Interceptor's full static under its analog interference. Call it every frame into a video output while no drone camera renders into it.

## OSD

The MAIN panel of Interceptor Drones: VTX signal, arm state, payload, flight mode, battery cell voltage, altitude, speed, heading with compass tape, artificial horizon and reticle, plus a centre RXLOSS / FAILSAFE warning.

1. Set up the FPV video first: the OSD is composited by `FpvVideoCamera`.
2. Add `FpvOsdCamera` to the drone camera with `Runtime/Osd/Prefabs/FpvOsd.prefab` and a free layer for `_osdLayer`. The video camera stops rendering that layer.
3. Add `FpvOsdFlightSource` to the drone camera or the drone, or implement `IFpvOsdDataSource` yourself. Call `SetPayload(hasPayload, isArmed)` for the payload line.

The OSD is drawn by its own camera into a transparent texture and laid over the finished video, so it stays sharp under lens water and fisheye and fades with the signal. The fonts need TMP Essential Resources (**Window ▸ TextMeshPro ▸ Import TMP Essential Resources**).

The original heading arrow is an MGAssets sprite, so the prefab draws a chevron instead. The UAV-OSD fonts came from Interceptor Drones without a licence file; check their licence before shipping.

### Pilot's OSD layout

As in FPV Defence Simulator, the pilot can show, hide and move OSD elements, make the font bold and fade the OSD (opacity 20–100%), and choose the mode: standard (`Main`), own layout (`Custom`) or off.

- **Elements:** `FpvOsdElementCatalog` lists them. The main group is the interceptor OSD. The extra group is hidden by default and uses the same font:
  - throttle `THR`, flight time `FLY`, current `A`, used `MAH`;
  - pack voltage `V`, charge `BAT %`, link quality `LQ`;
  - vertical speed `VS`, pitch `PIT`, roll `ROL`.

  The horizon and the reticle can be hidden but not moved.
- **Settings:** `FpvOsdSettings` holds the mode and the `FpvOsdLayout`. A layout lists each element's visibility and anchor, plus opacity and bold. `FpvOsdView` reads the settings from `PlayerPrefsFpvOsdSettingsStore.Default`, or from any `IFpvOsdSettingsStore` given with `SetSettingsStore`. It follows the store's `Changed`, so a saved layout reaches OSDs that are already shown. A layout that does not name an element keeps that element's standard state, and extras stay hidden.
- **Editing without UI:**
  - `FpvOsdEditorSession` starts from the saved own layout and shows every change on the view at once.
  - Moving or toggling switches the session to the own layout. `Save` writes it to the store and `Cancel` restores what is saved.
  - Element bounds are normalized to the OSD (origin bottom left).
  - `FpvOsdSnapping` gives a dragged element the snapping of FPV Defence Simulator: within 10 px it snaps to its standard position first, then to the frame edges and centre and to other elements.
- **Ready editor:** `FpvOsdEditorScreen` (assembly `Osd.UI`, UI Toolkit) is the editor screen of FPV Defence Simulator.
  - `FpvOsdEditorScreen.Create()`, then `Open(view, videoCamera)`. The camera renders into a texture while the editor is open.
  - `Closed(isSaved)` fires when the pilot saves or cancels.
  - Runtime UI Toolkit gets pointer input from an EventSystem, and a project on the Input System gets none without one. If the scene has no EventSystem, the editor creates one with `InputSystemUIInputModule` while it is open.
  - The demo opens it with **O**; **Shift+O** cycles the mode.
- **Without a drone:** `FpvOsdPreview.Create(width, height)` renders the real OSD prefab over an FPV frame into an opaque texture. It uses fixed sample telemetry, so a menu can edit the layout on `preview.View`. Size it like the drone video. The prefab and the picture come from `Resources/UForce/FpvFlight/FpvOsdDefaults`, or can be passed in.

## Drone types

`Runtime/DroneTypes` holds five `FpvDroneType` assets, each with its airframe, battery, rates, flight controller profile, camera rig and prefab:

| Type | Prefab | Camera | Top speed (derived) |
|---|---|---|---|
| Strike FPV (Komandor's drone) | `FpvStrike` | fixed 25° uptilt, FOV 90 | 145 km/h empty, about 137 km/h with a 0.6 kg warhead |
| P1-SUN | `FpvInterceptor` | SC: 90°, 45°, 0°, FOV 50 | 300 km/h |
| Sting | `FpvInterceptor` | as P1-SUN | 315 km/h |
| Zorelit v5 | `FpvInterceptor` | as P1-SUN | 416 km/h |
| SPYS | `FpvInterceptor` | as P1-SUN | 450 km/h |

The interceptors share the P1-SUN model, as in Interceptor Drones, and differ in physics and battery. They fly with Interceptor's flight model and their Interceptor presets (`*_InterceptorFlight` assets), so they handle exactly as in Interceptor Drones. The strike FPV flies with the Betaflight physics, tuned realistic but snappy:
- linear rates: 670°/s roll and pitch, 450°/s yaw (RC rate above 2, super rate 0);
- angular acceleration 20000 / 6000 / 22000 °/s²;
- 25 ms motors and a 30 ms rate loop.

It reaches 90% of full roll rate within 0.12 s, or 0.16 s with a 0.6 kg warhead. Real thrust, motor lag and quadratic drag stay, so its top speed still comes out of the physics. `type.Apply(controller)` gives a spawned prefab the type's flight assets, flight model and camera angles. Each prefab is playable on its own: flight controller, crash detector, camera with tilt, FPV video, OSD, onboard microphone, motor and collision sound, rotor wash, propellers and a thermal signature.

The models came from Interceptor Drones: the strike FPV as is, P1-SUN converted from a 66 MB ASCII FBX to binary, textures reduced to 2048 with URP metallic/smoothness maps. There is no licence file for either model, and the motor, servo, touch and hit sounds are MGAssets (Asset Store) clips; check both before shipping outside the company.

## Camera tilt, sound, crash and rotor wash

- **Camera tilt** (`FpvCameraTilt`): uptilt per switch position, eased over 0.15 s with a servo sound while it moves. Drive it with `SetPosition` from a latching switch or `Cycle` from a button.
- **Motor sound** (`FpvMotorAudio`): spool-up on arming, volume and pitch from motor output, wind masking at speed (up to −12 dB and a rising low cut). **Onboard microphone** (`FpvOnboardMicrophone`, on the AudioListener): the camera's microphone with synthesized wind and prop wash, preamp saturation and compression. **Touch and hit** (`FpvCollisionAudio`).
- **Crash** (`FpvCrashDetector`): a hit faster than 1 m/s is a crash unless it is a landing (normal within ~41° of up and at most 10 m/s). The drone disarms, settles over 0.25 s and cannot arm until `Recover`.
- **Rotor wash** (`FpvRotorWash`): dust coloured by the terrain layer below (`FpvRotorWashSurfaceSet`), grit, and water mist; `_RotorWashPosition` and `_RotorWashStrength` for grass shaders. Add `FpvGeoWorldRotorWash` next to it in a GeoWorld 1.20+ world for water, ice, puddles, snow, rain, leaves and ripples.

## Samples

- **Basic Setup**: demo input actions with every channel, default profiles, `FpvFlightDemoDriver` (spawns the drone types and routes the channels), `FpvTelemetryOverlay` and the demo scene builder.
