# Lucide Icons Rendering

The project includes the original Lucide SVG files under
`Assets/Icons/Lucide/`. These SVGs are not rendered directly in Avalonia 12.1.

## Why SVGs are not used at runtime

Avalonia 12.1 does not provide reliable SVG rendering:
- `Avalonia.Svg.Skia` (v11.x) is incompatible with Avalonia 12.x
- `Svg.Controls.Skia.Avalonia` (v12.x) renders inconsistently and does not
  support theme-aware coloring (`currentColor`)
- Theme switching does not re-evaluate SVG resources

Because of these limitations, icons are rendered using Path-based geometries
extracted from the Lucide SVGs. This preserves the exact Lucide outline style
(stroke-width=2, round caps/joins) and works reliably across themes.

## Why SVGs remain in the project

- Required for license compliance (ISC / MIT for Feather-derived icons)
- Serve as the canonical source of icon geometry
- Allow future migration back to real SVG rendering once Avalonia stabilizes

## Icon keys

All icons are defined centrally in `Styles/LucideIcons.axaml` with the
`lucide-` prefix and are referenced via `{StaticResource ...}` across all views.
The array bracket (`array-bracket`) is a custom geometry and not part of Lucide.

## License

Lucide icons are released under the ISC license (see `LICENSES/LUCIDE.txt`).
Lucide is a fork of Feather: icons derived from Feather additionally retain the
MIT attribution of their original author (Cole Bemis).
