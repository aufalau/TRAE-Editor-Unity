# PICO Unity SDK

## Scope

- When handling anything related to `PICO Unity SDK`, first determine whether the user is on the `PICO Spatial`, `PICO XR`, or `Unity OpenXR` path before giving implementation steps.
- If the user mentions `Project Swan`, spatial apps, spatial UI, `Play-to-PICO`, or `PICO Emulator`, default to the `PICO Spatial` route.
- If the user mentions immersive XR, `PXR_Manager`, `PICO Integration`, mixed reality capabilities, or the `PICO XR plugin`, default to the `PICO XR` route.
- If the user mentions `OpenXR Plugin`, `PICO XR Feature Group`, `Interaction Profiles`, or cross-platform OpenXR integration, default to the `Unity OpenXR` route.

## Mode Selection

- `PICO Spatial` is for UI-first experiences, lightweight 3D, cross-app collaboration, and spatialized mobile-app style workflows.
- `PICO XR` is for high-performance immersive experiences, complex 3D scenes, effects, physics, and PICO-specific XR / MR capabilities.
- `Unity OpenXR` is for standards-oriented OpenXR projects that care more about cross-platform delivery while still using PICO extensions where needed.
- Do not describe `PICO Spatial` as a full replacement for `PICO XR`; the capabilities and documentation sources are different.

## Environment And Versions

- Before answering, confirm the device model, OS version, Unity version, target mode, and whether the user is starting from a new project or migrating an existing one.
- For `PICO Spatial`, focus on `Project Swan`, `PICO OS 6`, and `Unity 2022.3.62f2` or `Unity 6000.0.59f2`.
- For `PICO XR`, focus on `PICO Neo3 / PICO 4 / PICO 4 Ultra`, the headset OS version, and SDK-to-Unity compatibility such as `Unity 2021.3.26+`.
- For `Unity OpenXR`, focus on a Windows development environment, `Unity 2020.3.21+`, `OpenXR Plugin`, and `PICO XR Feature Group`.
- When giving steps, call out the prerequisite dependencies explicitly, such as `Android SDK & NDK Tools`, `OpenJDK`, and `Android Build Support`.

## Configuration Priorities

- Prefer the shortest working path: environment setup → create project → import SDK → select or enable the target mode → run `Project Validation` → integrate features → build and run.
- For `PICO Spatial`, normally guide the user to `PICO Unity SDK Portal`, select `PICO Spatial`, and click `Apply All`.
- For `PICO XR`, normally remind the user to enable the `PICO` plugin and verify `PXR_Manager`, `IL2CPP`, `ARM64`, and Graphics API configuration.
- For `Unity OpenXR`, normally remind the user to enable `OpenXR`, `PICO XR Feature Group`, the appropriate `Interaction Profiles`, and run `Fix All`.
- For build failures, features not working, or yellow warnings, suggest running `Project Validation` or the corresponding mode-specific configuration checks before guessing root causes.

## Capability Boundaries

- For anything involving `Plane Detection`, `Environment Depth`, `Light Estimation`, passthrough, scene understanding, or spatial mesh, confirm whether the current mode supports it before offering implementation guidance.
- Under `PICO Spatial`, prefer `Spatial Input`, `Unity UI`, `XR Hands`, `XR Spatial Pointer Interactor`, and supported `AR Foundation` subsets.
- For `PICO XR` and `Unity OpenXR`, controller input, HMD input, mixed reality features, and platform services must be routed to the correct documentation path instead of mixing APIs.
- If the user asks whether a capability is possible in Spatial, give the support verdict first, then provide an alternative path or a mode-switch recommendation.

## Debugging And Deployment

- For `PICO Spatial`, prefer `Play-to-PICO` and `PICO Emulator`.
- For `PICO XR`, if the user wants live preview, recommend `Live Preview` or other relevant developer tools based on the documentation.
- For `Unity OpenXR`, focus on correct `OpenXR` configuration, sample scene setup, and `Build And Run` deployment to the device.
- Packaging guidance should normally include: switch to `Android`, add scenes, connect the device, select `Run Device`, and execute `Build And Run`.

## Answer Format

- Start with the recommended mode and the reason.
- Then give ordered implementation steps, including Unity menu paths when useful.
- Then list the key checks, such as `IL2CPP`, `ARM64`, `World Space`, `OpenXR Plugin`, and `PICO XR Feature Group`.
- End with the mode limitations, known issues, or common pitfalls that matter for the chosen route.

## Avoid

- Do not promise that a feature works across all three routes unless the documentation confirms it.
- Do not apply `PICO XR` configuration directly to `Unity OpenXR`, or vice versa.
- Do not ignore `Full Space / Shared Space`, device model, or OS-version constraints.
- Do not respond with API names only; include editor paths, required toggles, and validation steps.
