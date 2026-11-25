"""Fake engine for contract hardening: answers with the REAL engine's reply, then corrupts one value of its result.

The .NET side must turn every contract violation (missing field, wrong type, null, wrong shape, absurd number) into InvalidDataException
(or a coded CsvInputException), never into NullReferenceException, KeyNotFoundException, JsonException or any other surprise.
Environment: MKR_FAKE_ENGINE_DIR (folder of mkr_engine.py), MKR_FAKE_SEED (integer, makes the corruption reproducible).
"""
import json
import os
import random
import sys

sys.path.insert(0, os.environ["MKR_FAKE_ENGINE_DIR"])
import mkr_engine  # noqa: E402

HOSTILE = [None, True, False, 0, -1, 1, 1e308, -1e308, 1e-300, "", "x", [], {}, [1], {"a": 1}, 2 ** 70]


def leaves(node, path, found):
    if isinstance(node, dict):
        for key, value in node.items():
            found.append(path + (key,))
            leaves(value, path + (key,), found)
    elif isinstance(node, list):
        for index, value in enumerate(node[:50]):                 # the first items are enough and keep the work bounded
            found.append(path + (index,))
            leaves(value, path + (index,), found)


def corrupt(result, rng):
    found = []
    leaves(result, (), found)
    if not found:
        return result
    path = rng.choice(found)
    parent = result
    for key in path[:-1]:
        parent = parent[key]
    action = rng.choice(("replace", "replace", "replace", "drop", "add"))
    last = path[-1]
    if action == "replace":
        parent[last] = rng.choice(HOSTILE)
    elif action == "drop" and isinstance(parent, dict):
        parent.pop(last)
    elif action == "drop":
        parent.pop(last)
    elif isinstance(parent, dict):
        parent["unexpected"] = rng.choice(HOSTILE)
    return result


raw = sys.stdin.buffer.read()
reply = json.loads(mkr_engine.serve(raw))
if "result" in reply:
    reply["result"] = corrupt(reply["result"], random.Random(int(os.environ.get("MKR_FAKE_SEED", "0"))))
sys.stdout.buffer.write(json.dumps(reply, separators=(",", ":"), allow_nan=False).encode("utf-8"))
