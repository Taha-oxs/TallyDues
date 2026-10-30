"""Checks a .tdl file after Claude edits it. Exit 2 shows the problems to Claude."""
import json
import re
import sys

DEFINITION = re.compile(r"^\s*\[\s*([^:\]]+?)\s*:\s*([^\]]+?)\s*\]")
EMPTY_FORMULA = re.compile(r'^\s*[^:;]+:\s*""\s*$')


def check(path):
    with open(path, "rb") as f:
        raw = f.read()
    problems = []
    seen = {}
    section = ""
    for number, line in enumerate(raw.split(b"\n"), 1):
        if any(byte > 127 for byte in line):
            problems.append(f"line {number}: non-ASCII character (the installer refuses this file)")
        text = line.decode("ascii", "replace")
        match = DEFINITION.match(text)
        if match:
            kind, name = match.group(1).lower(), match.group(2).lower()
            section = kind
            # [System: ...] blocks repeat legitimately; # and ! change existing definitions.
            if kind != "system" and not kind.startswith(("#", "!")):
                if (kind, name) in seen:
                    problems.append(f"line {number}: duplicate definition, first at line {seen[(kind, name)]}: {text.strip()}")
                seen.setdefault((kind, name), number)
        elif section == "system" and EMPTY_FORMULA.match(text):
            problems.append(f"line {number}: empty formula, a suspected cause of silent failure")
    return problems


def main():
    if len(sys.argv) > 1:
        path = sys.argv[1]
    else:
        path = (json.load(sys.stdin).get("tool_input") or {}).get("file_path", "")
    if not path.lower().endswith(".tdl"):
        return 0
    problems = check(path)
    if problems:
        print(f"TDL check failed for {path}:", *problems, sep="\n", file=sys.stderr)
        return 2
    return 0


sys.exit(main())
