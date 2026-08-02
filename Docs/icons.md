# Feather Icons Rendering

The project includes the original Feather SVG files (MIT license) under
`Assets/Icons/Feather/`. These SVGs are not rendered directly in Avalonia 12.1.

## Why SVGs are not used at runtime

Avalonia 12.1 does not provide reliable SVG rendering:
- `Avalonia.Svg.Skia` (v11.x) is incompatible with Avalonia 12.x
- `Svg.Controls.Skia.Avalonia` (v12.x) renders inconsistently and does not
  support theme-aware coloring (`currentColor`)
- Theme switching does not re-evaluate SVG resources

Because of these limitations, icons are rendered using Path-based geometries
extracted from the Feather SVGs. This preserves the exact Feather outline style
(stroke-width=2, round caps/joins) and works reliably across themes.

## Why SVGs remain in the project

- Required for MIT license compliance
- Serve as the canonical source of icon geometry
- Allow future migration back to real SVG rendering once Avalonia stabilizes
