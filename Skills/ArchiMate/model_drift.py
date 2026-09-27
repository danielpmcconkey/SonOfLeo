#!/usr/bin/env python3
"""
Model drift: what exists in Src that the ArchiMate model doesn't describe, and what the model
describes that Src no longer has. Mechanical and report-only; run from the repo root.

    python3 Skills/ArchiMate/model_drift.py          # human-readable report
    python3 Skills/ArchiMate/model_drift.py --json   # the same findings as JSON

Exit code 0 when there is no drift, 1 when there is.

Source files are taken from each .fsproj's Compile Include list, never by globbing, so build
output under obj/ and bin/ is never read. Module references are found from `open` statements
and qualified names; F# core modules that share a name with a SonOfLeo module (Result, List,
...) only count when the SonOfLeo module is opened or named in full.
"""
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

MODEL = 'Architecture/SonOfLeo.archimate'
XSI = '{http://www.w3.org/2001/XMLSchema-instance}type'
FSHARP_CORE_MODULES = {'Result', 'List', 'Option', 'Array', 'Seq', 'Map', 'Set', 'String', 'ValueOption'}
OWN_ROOTS = ('App', 'Business', 'Ui')


def load_model():
    root = ET.parse(MODEL).getroot()
    elements = {e.get('id'): e for e in root.iter('element')}
    kind = lambda i: elements[i].get(XSI).split(':')[1]
    return elements, kind


def component_paths(elements, kind):
    paths = {}
    for i, e in elements.items():
        if kind(i) == 'ApplicationComponent':
            p = e.find("property[@key='path']")
            paths[i] = p.get('value') if p is not None else None
    return paths


def read_projects():
    compiled, project_refs, projects = [], [], sorted(glob.glob('Src/**/*.fsproj', recursive=True))
    for proj in projects:
        text = open(proj, encoding='utf-8-sig').read()
        folder = os.path.dirname(proj)
        norm = lambda f: os.path.normpath(os.path.join(folder, f.replace('\\', '/')))
        compiled += [norm(f) for f in re.findall(r'<Compile Include="([^"]+)"', text)]
        project_refs += [(norm(f), proj) for f in re.findall(r'<ProjectReference Include="([^"]+)"', text)]
    return projects, compiled, project_refs


def strip_code(text):
    text = re.sub(r'\(\*.*?\*\)', '', text, flags=re.S)
    text = re.sub(r'//.*', '', text)
    return re.sub(r'"(?:\\.|[^"\\])*"', '""', text)


def module_map(compiled):
    modules = {}
    for f in compiled:
        text = open(f, encoding='utf-8-sig').read()
        m = re.search(r'^\s*module\s+(?:rec\s+)?([A-Za-z0-9_.]+)\s*(?://.*)?$', text, re.M)
        if m:
            modules[m.group(1)] = f
    return modules


def module_references(compiled, modules):
    uses, external_opens = {}, {}
    for f in compiled:
        code = strip_code(open(f, encoding='utf-8-sig').read())
        opens = re.findall(r'^\s*open\s+([A-Za-z0-9_.]+)', code, re.M)
        mine = [name for name, g in modules.items() if g == f]
        my_parent = mine[0].rsplit('.', 1)[0] if mine and '.' in mine[0] else ''
        found = set()
        for name, g in modules.items():
            if g == f:
                continue
            parent, short = (name.rsplit('.', 1) if '.' in name else ('', name))
            if name in opens or re.search(r'(?<![A-Za-z0-9_.])' + re.escape(name) + r'\.', code):
                found.add(g)
            elif short not in FSHARP_CORE_MODULES and (parent in opens or parent == my_parent) \
                    and re.search(r'(?<![A-Za-z0-9_.])' + re.escape(short) + r'\.[A-Za-z]', code):
                found.add(g)
        uses[f] = found
        for o in opens:
            if not o.startswith(OWN_ROOTS):
                external_opens.setdefault(o, set()).add(f)
    return uses, external_opens


def drift():
    elements, kind = load_model()
    paths = component_paths(elements, kind)
    by_path = {p: i for i, p in paths.items() if p}
    rels = [e for e in elements.values() if e.get('source')]
    serving = {(e.get('source'), e.get('target')) for e in rels if kind(e.get('id')) == 'ServingRelationship'}
    realizes_function = {e.get('source') for e in rels
                         if kind(e.get('id')) == 'RealizationRelationship' and kind(e.get('target')) == 'ApplicationFunction'}
    functions = [i for i in elements if kind(i) == 'ApplicationFunction']
    realized = {e.get('target') for e in rels if kind(e.get('id')) == 'RealizationRelationship'}
    group_functions = {e.get('source') for e in rels
                       if kind(e.get('id')) == 'CompositionRelationship' and e.get('source') in functions}
    system_software = {e.get('name') for i, e in elements.items() if kind(i) == 'SystemSoftware'}

    projects, compiled, project_refs = read_projects()
    modules = module_map(compiled)
    uses, external_opens = module_references(compiled, modules)
    name = lambda i: elements[i].get('name')

    findings = {
        'project with no component': [p for p in projects if p not in by_path],
        'compiled file with no component': [f for f in compiled if f not in by_path],
        'component whose path does not exist': sorted(p for p in by_path if not os.path.exists(p)),
        'file component not in any Compile Include list': sorted(
            p for p in by_path if p.endswith('.fs') and p not in compiled),
        'project reference with no serving edge': [
            f'{a} -> {b}' for a, b in project_refs if (by_path.get(a), by_path.get(b)) not in serving],
        'module reference with no serving edge (provider -> consumer)': sorted(
            f'{g} -> {f}' for f, found in uses.items() for g in found
            if f in by_path and g in by_path and (by_path[g], by_path[f]) not in serving),
        'serving edge with no module reference in the code (provider -> consumer)': sorted(
            f'{paths[a]} -> {paths[b]}' for a, b in serving
            if (paths.get(a) or '').endswith('.fs') and (paths.get(b) or '').endswith('.fs')
            and paths[a] not in uses.get(paths[b], set())),
        'opened namespace with no system software element': sorted(
            f'{o} ({len(fs)} files)' for o, fs in external_opens.items() if o not in system_software),
        'file component realizing no capability': sorted(
            paths[i] for i in paths if (paths[i] or '').endswith('.fs') and i not in realizes_function),
        'capability nothing realizes': sorted(
            name(f) for f in functions if f not in realized and f not in group_functions),
    }
    return {k: v for k, v in findings.items() if v}


if __name__ == '__main__':
    found = drift()
    if '--json' in sys.argv:
        print(json.dumps(found, indent=2))
    elif not found:
        print('NO DRIFT — the model and Src agree.')
    else:
        for heading, items in found.items():
            print(f'\n{heading} ({len(items)}):')
            for item in items:
                print(f'  {item}')
    sys.exit(1 if found else 0)
