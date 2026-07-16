#!/usr/bin/env python3
"""Speedscope inclusive-time analyzer for the transposed-grid trace gate.

Given a speedscope JSON (as produced by `dotnet-trace collect --format Speedscope`)
this prints the ABSOLUTE inclusive time (ms) spent in a fixed set of frames so the
headline number is unambiguous and identical baseline-vs-after. It also reports whether
the attach/styling frames appear anywhere under a `Realize` stack (mechanism presence
check), and per-frame shares of the `MeasureOverride` total for context ONLY -- shares are
NOT the gate (the fix shrinks numerator and denominator together; see the plan's Testing
Strategy).

Each match key is a substring tested against the frame name. Keys for methods that several
types declare (MeasureOverride, Realize) carry the declaring type as the qualifier; keys for
methods with a single relevant declaration (OnAttachedToVisualTreeCore, ApplyStyling) are the
bare method name (leading dot), which is already unambiguous:

    measure_override  -> "TransposedColumnsPanel.MeasureOverride"
    realize           -> "TransposedColumnsPanel.Realize"
    attached          -> ".OnAttachedToVisualTreeCore"      (Visual, recursive cascade)
    style_attach      -> "StyleBase.Attach"
    apply_styling     -> ".ApplyStyling"                    (StyledElement)
    acquire_and_bind  -> "TransposedColumnCellsHost.AcquireAndBind"

The attach/styling SUM the gate watches is attached + style_attach + apply_styling.

dotnet-trace emits "evented" profiles (open/close events); inclusive time is computed via a
stack walk over the SET of frames on the stack, so a recursive frame (same frame nested) is
counted once per time interval, never double-counted.

Synthetic leaf frames ("CPU_TIME", "UNMANAGED_CODE_TIME", "?!?", "(unmanaged)") never
match a real key; their time flows to the nearest real ancestor automatically because
the ancestor is still on the stack while the synthetic frame is the leaf. This is the
same reattribution the prior manual analysis applied.
"""

import json
import sys

MATCH_KEYS = {
    "measure_override": "TransposedColumnsPanel.MeasureOverride",
    "realize": "TransposedColumnsPanel.Realize",
    "attached": ".OnAttachedToVisualTreeCore",
    "style_attach": "StyleBase.Attach",
    "apply_styling": ".ApplyStyling",
    "acquire_and_bind": "TransposedColumnCellsHost.AcquireAndBind",
}

ATTACH_STYLING_KEYS = ("attached", "style_attach", "apply_styling")

SYNTHETIC_LEAF_MARKERS = ("CPU_TIME", "UNMANAGED_CODE_TIME", "?!?", "(unmanaged)")


def is_synthetic(name):
    return any(marker in name for marker in SYNTHETIC_LEAF_MARKERS)


def key_for(frame_name):
    """Return the first match key whose substring occurs in frame_name, else None."""
    for key, needle in MATCH_KEYS.items():
        if needle in frame_name:
            return key
    return None


def _unit_scale_to_ms(unit):
    # speedscope units: "nanoseconds" | "microseconds" | "milliseconds" | "seconds" | "none".
    return {
        "nanoseconds": 1e-6,
        "microseconds": 1e-3,
        "milliseconds": 1.0,
        "seconds": 1000.0,
    }.get(unit, 1.0)


def analyze_profile(profile, frame_names):
    """Return (inclusive_ms_by_key, realize_stack_key_presence).

    inclusive_ms_by_key: match-key -> total inclusive milliseconds.
    realize_stack_key_presence: set of attach/styling keys seen on any stack that also
    contains a Realize frame (mechanism presence check).
    """
    scale = _unit_scale_to_ms(profile.get("unit", "none"))
    inclusive = {key: 0.0 for key in MATCH_KEYS}
    realize_presence = set()

    def account(stack_keys, weight):
        # stack_keys: set of match-keys present on the current stack.
        for key in stack_keys:
            inclusive[key] += weight * scale
        if "realize" in stack_keys:
            for key in ATTACH_STYLING_KEYS:
                if key in stack_keys:
                    realize_presence.add(key)

    ptype = profile.get("type")
    if ptype != "evented":
        raise ValueError(f"Unsupported speedscope profile type: {ptype!r} (dotnet-trace emits 'evented')")

    stack = []
    last_at = profile.get("startValue", 0)
    for event in profile["events"]:
        at = event["at"]
        if at > last_at and stack:
            stack_keys = set()
            for frame_index in stack:
                key = key_for(frame_names[frame_index])
                if key is not None:
                    stack_keys.add(key)
            account(stack_keys, at - last_at)
        etype = event["type"]
        if etype == "O":
            stack.append(event["frame"])
        elif etype == "C":
            # Close the matching frame; tolerate imperfect nesting by popping the last
            # occurrence of the frame index.
            frame = event["frame"]
            for i in range(len(stack) - 1, -1, -1):
                if stack[i] == frame:
                    del stack[i]
                    break
        last_at = at

    return inclusive, realize_presence


def load(path):
    with open(path, "r", encoding="utf-8") as handle:
        doc = json.load(handle)
    frame_names = [frame.get("name", "") for frame in doc["shared"]["frames"]]
    return doc, frame_names


def analyze_file(path):
    doc, frame_names = load(path)
    total_inclusive = {key: 0.0 for key in MATCH_KEYS}
    total_presence = set()
    for profile in doc["profiles"]:
        inclusive, presence = analyze_profile(profile, frame_names)
        for key, value in inclusive.items():
            total_inclusive[key] += value
        total_presence |= presence
    return total_inclusive, total_presence


def print_report(path):
    inclusive, presence = analyze_file(path)

    attach_styling_sum = sum(inclusive[key] for key in ATTACH_STYLING_KEYS)
    measure = inclusive["measure_override"]
    realize = inclusive["realize"]

    def share(value):
        return f"{100.0 * value / measure:6.1f}%" if measure > 0 else "   n/a"

    print(f"file: {path}")
    print("--- absolute inclusive time (ms) ---")
    print(f"  MeasureOverride (TransposedColumnsPanel)      {measure:12.1f}")
    print(f"  Realize         (TransposedColumnsPanel)      {realize:12.1f}   share {share(realize)}")
    print(f"  OnAttachedToVisualTreeCore (Visual, cascade)  {inclusive['attached']:12.1f}   share {share(inclusive['attached'])}")
    print(f"  StyleBase.Attach                              {inclusive['style_attach']:12.1f}   share {share(inclusive['style_attach'])}")
    print(f"  ApplyStyling    (StyledElement)               {inclusive['apply_styling']:12.1f}   share {share(inclusive['apply_styling'])}")
    print(f"  AcquireAndBind  (TransposedColumnCellsHost)   {inclusive['acquire_and_bind']:12.1f}   share {share(inclusive['acquire_and_bind'])}")
    print(f"  ATTACH/STYLING SUM (attached+attach+styling)  {attach_styling_sum:12.1f}   share {share(attach_styling_sum)}")
    print("--- attach/styling frames present under a Realize stack ---")
    for key in ATTACH_STYLING_KEYS:
        state = "PRESENT" if key in presence else "absent"
        print(f"  {MATCH_KEYS[key]:34s} {state}")
    return inclusive, presence


def _selftest():
    """Hand-written evented speedscope with known times, including a recursive frame, so inclusive
    time is verified NOT double-counted. Three sequential intervals:

        [0,100]   Realize -> ApplyStyling(outer) -> ApplyStyling(inner, recursive)
        [100,140] Realize -> AcquireAndBind
        [140,150] MeasureOverride

    Expected inclusive:
        MeasureOverride  = 10
        Realize          = 140 (100 + 40, two separate spans)
        ApplyStyling     = 100 (recursive: the nested span is counted ONCE)
        AcquireAndBind   = 40
        attach/styling sum (attached 0 + style_attach 0 + apply_styling 100) = 100
    Presence: apply_styling PRESENT under Realize; attached/style_attach absent.
    """
    doc = {
        "shared": {
            "frames": [
                {"name": "SemiStep.UI.RecipeGrid.Transposed.TransposedColumnsPanel.Realize(int32)"},
                {"name": "Avalonia.StyledElement.ApplyStyling()"},
                {"name": "SemiStep.UI.RecipeGrid.Transposed.TransposedColumnCellsHost.AcquireAndBind()"},
                {"name": "SemiStep.UI.RecipeGrid.Transposed.TransposedColumnsPanel.MeasureOverride(Avalonia.Size)"},
            ]
        },
        "profiles": [
            {
                "type": "evented",
                "unit": "milliseconds",
                "startValue": 0,
                "endValue": 150,
                "events": [
                    {"type": "O", "frame": 0, "at": 0},     # Realize
                    {"type": "O", "frame": 1, "at": 0},     # ApplyStyling outer
                    {"type": "O", "frame": 1, "at": 50},    # ApplyStyling inner (recursive)
                    {"type": "C", "frame": 1, "at": 80},    # close inner
                    {"type": "C", "frame": 1, "at": 100},   # close outer
                    {"type": "C", "frame": 0, "at": 100},   # close Realize
                    {"type": "O", "frame": 0, "at": 100},   # Realize (second span)
                    {"type": "O", "frame": 2, "at": 100},   # AcquireAndBind
                    {"type": "C", "frame": 2, "at": 140},   # close AcquireAndBind
                    {"type": "C", "frame": 0, "at": 140},   # close Realize
                    {"type": "O", "frame": 3, "at": 140},   # MeasureOverride
                    {"type": "C", "frame": 3, "at": 150},   # close MeasureOverride
                ],
            }
        ],
    }

    frame_names = [f["name"] for f in doc["shared"]["frames"]]
    inclusive, presence = analyze_profile(doc["profiles"][0], frame_names)

    expected = {
        "measure_override": 10.0,
        "realize": 140.0,
        "attached": 0.0,
        "style_attach": 0.0,
        "apply_styling": 100.0,
        "acquire_and_bind": 40.0,
    }
    for key, want in expected.items():
        got = inclusive[key]
        assert abs(got - want) < 1e-9, f"self-test FAIL {key}: got {got}, want {want}"

    assert presence == {"apply_styling"}, f"self-test FAIL presence: {presence}"

    print("self-test PASS")


def main(argv):
    if len(argv) == 2 and argv[1] == "--selftest":
        _selftest()
        return 0
    if len(argv) != 2:
        print("usage: speedscope-shares.py <speedscope.json>", file=sys.stderr)
        print("       speedscope-shares.py --selftest", file=sys.stderr)
        return 2
    print_report(argv[1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
