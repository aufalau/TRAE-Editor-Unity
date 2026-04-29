# Code Editor Package for TRAE IDE

## \[1.0.5] - 2026-04-23

- Move bundled rules into `rules~` folder to hide from Unity and support extensibility.
- Auto-overwrite project rules when plugin version is updated (version stamp mechanism).
- Auto-copy PICO XR SDK rules when `com.bytedance.pico.xr` package is detected in the project.
## \[1.0.4] - 2026-04-23

- Add first run welcome prompt for new users.
- Add update checker to notify developers of new versions.
- Add analytics opt-out toggle in Preferences.
- Update macOS libapplogrs.dylib for arm64 and x86_64.

## \[1.0.3] - 2026-04-14

- Integrate applog SDK for anonymous usage analytics.

## \[1.0.2] - 2026-04-13

- Add complete Unity code style guide for project rules.
- Support distinguishing between TRAE and TRAE CN editions.
- Add ThirdPartyNotices.md.

## \[1.0.1] - 2026-02-12

- Fix duplicate project opening: for packages with the same name, only open the latest associated one.

## \[1.0.0] - 2026-02-04

- Support navigation, highlighting, and reference linking.
- Support local folders.
- Support automatic project rule configuration.

