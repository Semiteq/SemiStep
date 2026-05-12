---
name: avalonia-docs
description: "Load this skill whenever you need information about Avalonia UI components, controls, layouts, styles, animations, bindings, or any other Avalonia API. Use the GitHub MCP tools to read docs from the AvaloniaUI/avalonia-docs repository. Fall back to WebFetch only if the topic is not covered there."
---

# Skill: Avalonia Docs

## Overview

When you need information about Avalonia -- controls, layouts, data binding, styles, animations,
gestures, or any other framework concept -- read the official documentation from the
`AvaloniaUI/avalonia-docs` GitHub repository using the GitHub MCP tools. Do not decompile NuGet
packages to infer API contracts; the docs are the authoritative reference.

The GitHub MCP server provides the `get_file_contents` tool (from the `repos` toolset) for reading
files and `search_issues` / `list_issues` tools (from the `issues` toolset) for searching issues
and pull requests.

## Repository Layout

```
AvaloniaUI/avalonia-docs  (branch: main)
└── docs/
    ├── basics/          # Core concepts: controls, data binding, styles, assets
    ├── concepts/        # Architecture, compiled bindings, custom controls, MVVM
    ├── get-started/     # Quickstarts and project setup
    ├── guides/          # How-to guides by topic
    ├── reference/
    │   ├── controls/    # Per-control reference pages (DataGrid, TextBox, ComboBox, etc.)
    │   ├── properties/  # Attached properties, resource system
    │   ├── styles/      # Styling and theming reference
    │   └── gestures/    # Pointer and touch gesture reference
    └── tutorials/       # Step-by-step tutorials
```

Key entry points for common topics:

| Topic | Path in repo |
|-------|-------------|
| Control reference index | `docs/reference/controls/index.md` |
| DataGrid | `docs/reference/controls/datagrid/` |
| Data binding basics | `docs/basics/` |
| Compiled bindings | `docs/concepts/` |
| Custom controls | `docs/concepts/` |
| Styles and themes | `docs/reference/styles/` |
| Animations | `docs/reference/animation-settings.md` |
| Gestures | `docs/reference/gestures/` |
| Built-in converters | `docs/reference/built-in-data-binding-converters.md` |

## Lookup Process

Follow these steps in order. Stop as soon as you find sufficient information.

### Step 1: Read the Relevant File via GitHub MCP

Use the `get_file_contents` tool to read files directly from the repository:

- **Owner:** `AvaloniaUI`
- **Repo:** `avalonia-docs`
- **Branch:** `main`
- **Path:** the path within the repo (e.g., `docs/reference/controls/datagrid/datagrid-columns.md`)

Start from the key entry points table above. If you need to discover the exact file name, read the
index file for that section first (e.g., `docs/reference/controls/index.md`), then navigate to the
specific file.

### Step 2: Search Issues and PRs (If Docs Are Incomplete)

If the documentation file does not fully cover the topic -- for example, a known limitation,
a bug workaround, or a feature added in a recent release -- use the `search_issues` tool to
search the `AvaloniaUI/avalonia-docs` repository (or `AvaloniaUI/Avalonia` for the main framework
repo) for relevant issues or merged PRs:

- Search `AvaloniaUI/avalonia-docs` for documentation gaps or recent additions.
- Search `AvaloniaUI/Avalonia` for known bugs, workarounds, or implementation details.

### Step 3: Fall Back to WebFetch

Only if neither the docs file nor issues/PRs provide sufficient information, fetch the online
Avalonia documentation:

- Primary: `https://docs.avaloniaui.net/`
- API reference: `https://api.avaloniaui.net/`

State clearly in your response that you fell back to online docs and why the GitHub MCP sources
did not cover the topic.

## Key Principles

- **GitHub MCP first, always.** Do not skip to WebFetch because it feels faster.
- **No NuGet decompilation.** Do not use decompiler tools or inspect `.nupkg` contents to
  infer API contracts. Use the docs.
- **Cite the file path.** When answering based on the docs, include the path within the repo
  (e.g., `docs/reference/controls/datagrid/datagrid-columns.md`) so the user can verify.
- **Be specific about gaps.** If the docs cover the topic partially, say what they cover
  and what they do not, then supplement with issues or WebFetch for the missing part only.
