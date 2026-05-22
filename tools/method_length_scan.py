# -*- coding: utf-8 -*-
"""Rough C# method length scan: brace-balanced blocks after method-like signatures."""
import re
import sys
from pathlib import Path

SKIP_DIRS = {"obj", "bin", "Properties"}
METHOD_START = re.compile(
    r"^\s*(?:public|private|protected|internal)\s+"
    r"(?:static\s+|async\s+|extern\s+|partial\s+|override\s+|virtual\s+|new\s+)*"
    r"(?:[\w.<>,\[\]]+\s+)+\b(\w+)\s*\("
)


def strip_line_comment(line: str) -> str:
    in_sq = in_dq = False
    out = []
    i = 0
    while i < len(line):
        c = line[i]
        if not in_dq and not in_sq and c == "/" and i + 1 < len(line) and line[i + 1] == "/":
            break
        if c == '"' and not in_sq:
            if in_dq and i > 0 and line[i - 1] == "@":
                pass
            in_dq = not in_dq
        elif c == "'" and not in_dq:
            in_sq = not in_sq
        out.append(c)
        i += 1
    return "".join(out)


def looks_like_method_header(line: str) -> bool:
    s = line.strip()
    if s.startswith("//") or s.startswith("/*"):
        return False
    if re.match(r"^\s*(public|private|protected|internal)\s+(partial\s+)?class\s+", line):
        return False
    if re.match(r"^\s*(public|private|protected|internal)\s+(static\s+)?(?:readonly\s+)?(?:struct|enum|interface)\s+", line):
        return False
    if re.match(r"^\s*(public|private|protected|internal)\s+[\w.<>,\[\]]+\s*\{\s*get;", line):
        return False
    if " operator " in line or line.strip().startswith("public event "):
        return False
    return bool(METHOD_START.match(line))


def find_opening_brace(lines, start_idx: int) -> int:
    """After header lines ending with ), find index of line containing opening { for body."""
    paren = 0
    i = start_idx
    started = False
    while i < len(lines):
        raw = lines[i]
        line = strip_line_comment(raw)
        for c in line:
            if c == "(":
                paren += 1
                started = True
            elif c == ")":
                paren -= 1
        if started and paren <= 0:
            rest = line[line.rfind(")") + 1 :]
            if "{" in rest:
                return i
            j = i + 1
            while j < len(lines) and j < i + 12:
                if "{" in strip_line_comment(lines[j]):
                    return j
                if strip_line_comment(lines[j]).strip() and not strip_line_comment(lines[j]).strip().startswith("where "):
                    if "(" in lines[j]:
                        break
                j += 1
            return j if j < len(lines) else -1
        i += 1
    return -1


def body_length_from_brace(lines, brace_line_idx: int) -> tuple[int, int]:
    """Return (line_count, end_line_index) of method body including braces, or (-1,-1)."""
    if brace_line_idx < 0 or brace_line_idx >= len(lines):
        return -1, -1
    depth = 0
    started = False
    i = brace_line_idx
    while i < len(lines):
        line = strip_line_comment(lines[i])
        for c in line:
            if c == "{":
                depth += 1
                started = True
            elif c == "}":
                depth -= 1
                if started and depth == 0:
                    return i - brace_line_idx + 1, i
        i += 1
    return -1, -1


def scan_file(path: Path) -> list[tuple[int, str, str]]:
    rel = str(path)
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        text = path.read_text(encoding="utf-8-sig", errors="replace")
    lines = text.splitlines()
    results = []
    i = 0
    n = len(lines)
    while i < n:
        if looks_like_method_header(lines[i]):
            name_m = METHOD_START.match(lines[i])
            name = name_m.group(1) if name_m else "?"
            open_brace = find_opening_brace(lines, i)
            if open_brace < 0:
                i += 1
                continue
            length, end_i = body_length_from_brace(lines, open_brace)
            if length > 0:
                results.append((length, name, f"{rel}:{i + 1}"))
                i = end_i + 1
                continue
        i += 1
    return results


def main():
    root = Path(__file__).resolve().parents[1] / "sxtg2"
    if not root.is_dir():
        print("sxtg2 folder not found", file=sys.stderr)
        sys.exit(1)
    all_m: list[tuple[int, str, str]] = []
    for path in sorted(root.rglob("*.cs")):
        parts = set(path.parts)
        if "obj" in parts or "bin" in parts:
            continue
        all_m.extend(scan_file(path))
    all_m.sort(key=lambda x: -x[0])
    threshold = 60
    print(f"Methods with body >= {threshold} lines (heuristic; properties/lambdas may false-positive):\n")
    shown = 0
    for length, name, loc in all_m:
        if length < threshold:
            break
        print(f"{length:4d}  {name:40s}  {loc}")
        shown += 1
        if shown >= 45:
            print("\n... (truncated after 45)")
            break
    if shown == 0:
        print("(none >= threshold; try lowering threshold in script)")


if __name__ == "__main__":
    main()
