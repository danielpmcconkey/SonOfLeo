#!/usr/bin/env bash
# Enforces: every Src file's `open` block follows build order.
# External opens (System, NodaTime, Npgsql, ...) come first. Internal opens follow in the
# order the compiler meets them: project build order below, then <Compile Include> order
# within a project. A namespace open sorts before the modules inside it; an open of a
# nested module sorts with the file that declares it.
set -u
cd "$(dirname "$0")/.."

python3 - <<'PY'
import os, re, sys

internal_roots = ("App.", "Business.", "Ui.")
status = 0

projects = sorted(d for d in os.listdir("Src") if os.path.isfile(f"Src/{d}/{d}.fsproj"))
refs = {}
for proj in projects:
    text = open(f"Src/{proj}/{proj}.fsproj").read()
    refs[proj] = set(re.findall(r'ProjectReference Include="[^"]*?([A-Za-z.]+)\.fsproj"', text))

def deps_of(proj, seen=None):
    seen = set() if seen is None else seen
    for r in refs.get(proj, ()):
        if r not in seen:
            seen.add(r)
            deps_of(r, seen)
    return seen

deps = {proj: deps_of(proj) for proj in projects}

rank = {}      # declared module/namespace name -> (project index, file index)
files = []     # (path, project index, file index)
for p, proj in enumerate(projects):
    fsproj = f"Src/{proj}/{proj}.fsproj"
    if not os.path.isfile(fsproj):
        continue
    text = open(fsproj).read()
    for f, inc in enumerate(re.findall(r'Compile Include="([^"]+)"', text)):
        path = f"Src/{proj}/{inc.replace(chr(92), '/')}"
        if not os.path.isfile(path):
            continue
        files.append((path, p, f))
        for line in open(path):
            m = re.match(r'(module|namespace)\s+(?:rec\s+)?([A-Za-z0-9_.]+)\s*$', line)
            if m:
                rank.setdefault(m.group(2), (p, f))
                break

def must_precede(a, b):
    # True when open a has to come before open b
    pa, pb = projects[a[0]], projects[b[0]]
    if pa == pb:
        return a[1:] < b[1:]
    return pa in deps[pb]

def rank_of(name):
    # exact or enclosing declared module (nested modules live in their file)
    parts = name.split(".")
    for i in range(len(parts), 0, -1):
        key = ".".join(parts[:i])
        if key in rank:
            return rank[key] + (1,)
    # a namespace: before the first module declared inside it
    inside = [r for n, r in rank.items() if n.startswith(name + ".")]
    if inside:
        return min(inside) + (0,)
    return None

for path, _, _ in files:
    seen_internal = False
    earlier = []
    for n, line in enumerate(open(path), 1):
        m = re.match(r'\s*open\s+(?:type\s+)?([A-Za-z0-9_.]+)', line)
        if not m:
            continue
        name = m.group(1)
        if not name.startswith(internal_roots):
            if seen_internal:
                print(f"{path}:{n}: external open {name} after an internal open")
                status = 1
            continue
        seen_internal = True
        r = rank_of(name)
        if r is None:
            print(f"{path}:{n}: open {name} names no Src module or namespace")
            status = 1
            continue
        for prev_r, prev_name in earlier:
            if must_precede(r, prev_r):
                print(f"{path}:{n}: open {name} builds before {prev_name}, which is opened above it")
                status = 1
                break
        earlier.append((r, name))

if status:
    print("Order open blocks as the compiler meets them: externals, then dependencies before dependents, then files in <Compile Include> order.")
sys.exit(status)
PY
