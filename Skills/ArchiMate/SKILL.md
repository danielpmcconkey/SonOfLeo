# SonOfLeo ArchiMate Skill

You are authoring and maintaining an ArchiMate 3.2 model for the SonOfLeo project.
The model is the architectural backbone — motivation-layer elements (principles,
constraints, requirements, goals) are the source of truth for what matters, and
application-layer elements describe the system that must satisfy them. Relationships
are typed and validated against the ArchiMate specification.

## Files

| What | Where |
|---|---|
| **Model** | `Architecture/SonOfLeo.archimate` |
| **Validator** | `Skills/ArchiMate/validate.py` |
| **Relationship matrix** | `Skills/ArchiMate/references/relationships.xml` |
| **This skill** | `Skills/ArchiMate/SKILL.md` |

## The model's purpose

This is a **constraint model**, not documentation. It exists so that auditors can
check code against typed architectural constraints. The motivation layer holds
Dan's sacred cattle — the principles and constraints that govern every line of code.
The application layer holds the structural elements they govern. Relationships
connect them.

The model is NOT a visualization-first artifact. Views exist for Dan to communicate
design in Archi. The model (elements + relationships) is what agents read and audit
against.

## Authoring rules

### Format

The model uses Archi's native `.archimate` XML format. Key structural rules:

- **Namespace:** `xmlns:archimate="http://www.archimatetool.com/archimate"`,
  `xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"`
- **Element typing:** `xsi:type="archimate:TypeName"` (e.g.,
  `archimate:Principle`, `archimate:ApplicationComponent`)
- **IDs:** `id-` prefix + 32 lowercase hex chars (UUID without dashes). Generate
  with: `id-` + `uuid.uuid4().hex`
- **Elements** live in their layer folder (motivation, application, etc.)
- **Relationships** live in the Relations folder as `<element>` tags with
  `xsi:type`, `source`, and `target` attributes
- **Views** live in the Views folder. Dan creates and positions views in Archi.
  Agents may create view structures (which elements appear) but Dan owns layout.

### Relationship validity

Before creating any relationship, check legality against the matrix in
`references/relationships.xml`. The matrix maps source concept → target concept →
allowed relationship type codes:

| Code | Relationship |
|---|---|
| `a` | AccessRelationship |
| `c` | CompositionRelationship |
| `f` | FlowRelationship |
| `g` | AggregationRelationship |
| `i` | AssignmentRelationship |
| `n` | InfluenceRelationship |
| `o` | AssociationRelationship |
| `r` | RealizationRelationship |
| `s` | SpecializationRelationship |
| `t` | TriggeringRelationship |
| `v` | ServingRelationship |

If a relationship's code letter is not in the allowed string for that source →
target pair, **the relationship is illegal**. Do not create it.

**Normalization:** Junction types (AndJunction, OrJunction) normalize to "Junction"
for lookup. Relationship-on-relationship connections normalize to "Relationship".

### Key motivation-layer patterns

The direction that earns its keep for auditing:

- **Core element realizes motivation element.** An `ApplicationFunction` or
  `ApplicationComponent` has a `RealizationRelationship` to a `Principle`,
  `Constraint`, or `Requirement`. This means "this function is governed by this
  constraint." The auditor checks if the code conforms.
- **Motivation elements influence each other.** `InfluenceRelationship` between
  `Principle` → `Goal`, `Constraint` → `Principle`, etc.
- **Composition within motivation.** A `Principle` can be composed of
  sub-principles. A `Requirement` can be composed of sub-requirements.
- **Association** is the fallback. If no stronger typed relationship is legal,
  `AssociationRelationship` is almost always valid — but it carries no semantics.
  Prefer a typed relationship when one exists.

### Application-layer conventions

Each relationship type carries one meaning, so an auditor can ask one question of one type:

| Relationship | Between | Means |
|---|---|---|
| Composition | component → component | *contains*: `Src` → tier (`App`, `Business`, `Ui`) → project → (sub-folder) → module |
| Serving | component → component | *is used by*: the provider serves the consumer (`ExecuteReader` serves `Account`). Module-level edges come from `open`s and qualified references; project-level edges from `ProjectReference`s |
| Serving | system software → component | a library (`NodaTime`, `Npgsql`, …) is used by a module |
| Realization | component → function | the component implements the capability. A capability is defined once and may be realized by many components |
| Composition | function → function | capability taxonomy; each function has exactly one parent group, matching its folder |

Components carry a `path` property naming the source they model (a `.fs` file, a `.fsproj`, or a
directory), which is the key for code-to-model auditing. There is one component per `.fsproj` and
one per compiled `.fs` file.

### Properties

Use `<property key="..." value="..."/>` on elements for metadata the model schema
doesn't capture:

- `enforcement` — how strictly this principle/constraint is enforced
  (`absolute`, `strong`, `advisory`)
- `source` — where this principle is documented in prose
  (e.g., `standing-corrections`, `CompoundedLearnings/architecture.md`)
- `auditable` — whether an automated auditor can check this (`true`/`false`)
- `audit-strategy` — how an auditor would check this (free text, brief)

### Sub-folders

Use sub-folders within layer folders for organisation. They are user-defined and
carry no ArchiMate semantics. Good groupings for SonOfLeo:

- **Motivation:** `Principles/`, `Constraints/`, `Requirements/`, `Goals/`
- **Application components:** mirror `Src/` — `Src/<tier>/<project>/[<sub-folder>]`
- **Application functions:** by capability group; the folder name is the group function it belongs to

### Validation

After any model change, run the validator:

```bash
python3 Skills/ArchiMate/validate.py
```

This must return `VALID — no issues found.` before the change is committed.
The validator checks:
- Every relationship's source/target/type against the ArchiMate 3.2 matrix
- Broken references (source or target ID not found in the model)

The validator's results should match Archi's built-in validation
(Tools → Validate Model, or Ctrl+Shift+V). If they diverge, the validator is
wrong — fix it.

## View authoring

Dan is a C4-influenced architect. His preferences:

- **Context diagrams are king.** High-level views showing system boundaries,
  actors, and the key interactions between them.
- **Process flows are valued.** Triggering and flow relationships between
  behaviour elements, laid out to show sequence.
- **Use case models are not wanted.** Don't create them.
- **Dan owns layout.** Agents create view structures (add elements and
  connections to views). Dan opens the model in Archi and positions everything.
  Don't spend effort on coordinate geometry — it will be redone.

When creating a view:

1. Create an `ArchimateDiagramModel` element in the Views folder.
2. Add `DiagramObject` children referencing model elements by ID.
3. Add `sourceConnection` elements referencing model relationships.
4. Use rough grid positions (increment by 200 on x/y) so elements don't stack.
5. Dan will adjust in Archi.

## What this skill does NOT cover

- **Convention validation** (Dan's naming rules, structural patterns) — that's
  Stage 3, the auditor.
- **Dan's full modelling preferences** — Stage 2 conversation, to be captured
  in `references/preferences.md`.
- **Code-to-model auditing** — Stage 3, a separate tool that reads the model
  and checks the codebase against it.
