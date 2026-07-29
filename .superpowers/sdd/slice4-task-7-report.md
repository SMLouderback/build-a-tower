# Task 7 Report: Camera scrollbars

## Status

Complete; interactive Unity verification remains pending.

## Changes

- Added bottom horizontal and right vertical `OnGUI` scrollbars that pan the orthographic camera.
- Bounds use the active tower grid's X span and room Y extents with five-cell padding, falling back to X `-5..40` and Y `-5..30`.
- Preserved RMB/MMB pan and scroll-wheel zoom, clamping the camera to its visible orthographic bounds.
- Exposed scrollbar screen rects and exclude them from build input in `BuildController`.

## Verification

- Added `TowerGridTests.Scrollbar_center_range_keeps_camera_view_inside_padded_bounds`.
- Attempted the focused Unity EditMode test, but `Unity.exe` is not available on PATH, so neither the expected red run nor final green run could be observed.
- Manual verification in the open Unity editor remains required: pan to tall and wide tower extents, then exercise both scrollbars.
