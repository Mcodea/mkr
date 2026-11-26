"""Central texts: every key used by the screens exists in French and English, with the same placeholders."""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src"
TEXTS = ROOT / "Mkr.Core" / "Texts"


def load(language):
    texts = {}
    for path in sorted(TEXTS.glob(f"*.{language}.json")):
        texts.update(json.loads(path.read_text(encoding="utf-8")))
    return texts


def code_keys():
    """Keys defined in Localization.cs (inline French/English dictionaries)."""
    text = (ROOT / "Mkr.Core" / "Localization.cs").read_text(encoding="utf-8")
    french, english = text.split("private static readonly Dictionary<string, string> En = new()")
    pattern = re.compile(r'\["([\w.]+)"\]\s*=')
    return (set(pattern.findall(french.split("Fr = new()")[1])), set(pattern.findall(english)))


def placeholders(text):
    return sorted(set(re.findall(r"\{(\d+)", text)))


class Texts(unittest.TestCase):
    def test_french_and_english_files_have_the_same_keys(self):
        fr, en = load("fr"), load("en")
        self.assertEqual(sorted(set(fr) ^ set(en)), [])

    def test_placeholders_match(self):
        fr, en = load("fr"), load("en")
        bad = [k for k in fr if k in en and placeholders(fr[k]) != placeholders(en[k])]
        self.assertEqual(bad, [])

    def test_no_empty_text(self):
        for language in ("fr", "en"):
            empty = [k for k, v in load(language).items() if not v.strip()]
            self.assertEqual(empty, [], language)

    def test_every_used_key_is_defined(self):
        fr_inline, _ = code_keys()
        known = set(load("fr")) | fr_inline
        used = set()
        for path in (ROOT / "Mkr.Desktop").glob("*.xaml"):
            used |= set(re.findall(r"\{local:Loc ([\w.]+)\}", path.read_text(encoding="utf-8")))
        for path in (ROOT / "Mkr.Desktop").glob("*.cs"):
            used |= set(re.findall(r'Loc\.T\("([\w.]+)"', path.read_text(encoding="utf-8")))
        used = {k for k in used if not k.endswith(".")}          # keys built from a prefix and a number are checked below
        self.assertEqual(sorted(used - known), [])
        for prefix, count in (("screen.", 10), ("stage.", 7)):
            self.assertEqual([f"{prefix}{i}" for i in range(count) if f"{prefix}{i}" not in known], [])


if __name__ == "__main__":
    unittest.main()
