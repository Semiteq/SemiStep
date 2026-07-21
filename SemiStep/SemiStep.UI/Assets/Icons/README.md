# Toolbar icons

256x256 PNG with alpha, referenced from the recipe toolbar as
`avares://SemiStep/Assets/Icons/<name>.png`. The toolbar renders each at 16x16
(set in `RecipeToolBar.axaml`), so the larger source just gives crisper Hi-DPI
downscaling.

| File | Action |
| --- | --- |
| `add.png` | Add step |
| `delete.png` | Delete step |
| `copy.png` | Copy steps |
| `cut.png` | Cut steps |
| `paste.png` | Paste steps |
| `undo.png` | Undo |
| `redo.png` | Redo |

To change an icon, copy a new PNG with alpha over the same filename — no code
change needed.
