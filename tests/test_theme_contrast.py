"""Accessibility gate of the design system: every text/background pair of both themes meets WCAG 2.1 AA (4.5:1 for text, 3:1 for
graphical objects), computed from the colour tokens in src/Mkr.Desktop/Resources/Themes. A theme edit that breaks contrast fails here.
Also: both themes define exactly the same tokens, and no window uses a literal colour or a StaticResource brush (theme switching)."""
import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src" / "Mkr.Desktop"
NS = {"x": "http://schemas.microsoft.com/winfx/2006/xaml"}


def tokens(name):
    tree = ET.parse(ROOT / "Resources" / "Themes" / f"{name}.xaml")
    key = "{http://schemas.microsoft.com/winfx/2006/xaml}Key"
    return {element.attrib[key]: element.attrib["Color"] for element in tree.getroot()}


def luminance(hex_color):
    channels = [int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4 for c in channels]
    return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]


def contrast(first, second):
    a, b = sorted((luminance(first), luminance(second)), reverse=True)
    return (a + 0.05) / (b + 0.05)


TEXT_PAIRS = [
    ("MkrText", "MkrSurface"), ("MkrText", "MkrCanvas"), ("MkrText", "MkrSurfaceAlt"), ("MkrMuted", "MkrSurface"), ("MkrMuted", "MkrCanvas"),
    ("MkrMuted", "MkrSurfaceAlt"), ("MkrAccent", "MkrSurface"), ("MkrAccent", "MkrCanvas"), ("MkrOnAccent", "MkrAccent"),
    ("MkrText", "MkrAccentPale"), ("MkrAccent", "MkrAccentPale"), ("MkrNavyText", "MkrNavy"), ("MkrNavyMuted", "MkrNavy"),
    ("MkrNavyText", "MkrNavySoft"), ("MkrNavyMuted", "MkrNavySoft"),
    ("MkrOnStatus", "MkrSuccess"), ("MkrOnStatus", "MkrDanger"), ("MkrOnStatus", "MkrWarning"),
    ("MkrText", "MkrSuccessPale"), ("MkrText", "MkrDangerPale"), ("MkrText", "MkrWarningPale"),
    ("MkrPlotLabel", "MkrSurface"),
]
GRAPHIC_PAIRS = [(f"MkrSignal{i}", "MkrSurface") for i in range(1, 5)] + [("MkrBorderStrong", "MkrSurface"), ("MkrSuccess", "MkrSurface"),
                                                                           ("MkrDanger", "MkrSurface"), ("MkrWarning", "MkrSurface")]


class Themes(unittest.TestCase):
    def test_both_themes_define_the_same_tokens(self):
        self.assertEqual(set(tokens("Light")), set(tokens("Dark")))

    def test_text_pairs_reach_aa(self):
        for theme in ("Light", "Dark"):
            palette = tokens(theme)
            for foreground, background in TEXT_PAIRS:
                with self.subTest(theme=theme, pair=f"{foreground} sur {background}"):
                    ratio = contrast(palette[foreground], palette[background])
                    self.assertGreaterEqual(ratio, 4.5, f"{ratio:.2f}:1")

    def test_signals_and_status_colours_reach_3_to_1_on_the_surface(self):
        for theme in ("Light", "Dark"):
            palette = tokens(theme)
            for foreground, background in GRAPHIC_PAIRS:
                with self.subTest(theme=theme, pair=f"{foreground} sur {background}"):
                    ratio = contrast(palette[foreground], palette[background])
                    self.assertGreaterEqual(ratio, 3.0, f"{ratio:.2f}:1")

    def test_the_four_signal_colours_are_distinct_in_each_theme(self):
        for theme in ("Light", "Dark"):
            palette = tokens(theme)
            colours = {palette[f"MkrSignal{i}"] for i in range(1, 5)}
            self.assertEqual(len(colours), 4)


class NoLiteralColours(unittest.TestCase):
    def test_windows_use_tokens_only(self):
        offenders = []
        for path in ROOT.glob("*.xaml"):
            text = path.read_text(encoding="utf-8")
            if re.search(r'\{StaticResource Mkr(?!PrimaryButton|NavButton)', text):
                offenders.append(f"{path.name}: StaticResource brush")
            for match in re.finditer(r'(?:Foreground|Background|BorderBrush|Fill|Stroke)="(#[0-9A-Fa-f]{6,8}|White|Black)"', text):
                offenders.append(f"{path.name}: literal {match.group(1)}")
        self.assertEqual(offenders, [])

    def test_the_plots_draw_with_theme_colours(self):
        offenders = []
        for path in ROOT.glob("*Plot.cs"):
            for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                if re.search(r"Brushes\.(DimGray|SlateGray|DarkGray|Gray|DarkSlateGray|SteelBlue|DarkOrange|SeaGreen|MediumSeaGreen|MediumVioletRed)", line):
                    offenders.append(f"{path.name}:{number}")
        self.assertEqual(offenders, [])


if __name__ == "__main__":
    unittest.main()
