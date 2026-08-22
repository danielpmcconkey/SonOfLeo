# utilities-auditor

## STALE-README-JSON — stale-reference
- **Location:** Src/README.md line 28 (concerning Src/Utilities/Json.fs)
- **Summary:** Src/README.md identifies the Json module as InterfaceBridge.Json, but the module was moved to Utilities.Json during the classification-rule slice.
- **Resolution:** fix-code

Src/README.md line 28 reads: '| `InterfaceBridge.Json` | `Json.fromJson<'T>` / `Json.toJson<'T>` | Constructing your own `JsonSerializerOptions`. |'. The actual module is Utilities.Json (file: Src/Utilities/Json.fs, module declaration: 'module Utilities.Json'). Git log confirms the move: commit 394dca7 'moving Json into utilities so I can use it with classification rules.' All references in the codebase correctly use Utilities.Json or Utilities.Json.Json (e.g., InterfaceBridge/Routes/*.fs, ModelOrchestrator/ClassificationOrchestration.fs, Model/DataIngestion/Classification/ClassificationRule.fs). The README was never updated after the move. The architecture catalog (CompoundedLearnings/catalogs/architecture.md) and Specs/README.md (line 34) both designate Src/README.md as the authority for infrastructure inventory and silent conventions, enforced by code review. Having this entry wrong misrepresents both the module's location and its dependency layer — implying Json is at the InterfaceBridge level (above Model) rather than at the Utilities level (below Model), and could lead an agent or developer to either look in the wrong place or duplicate the module.

**Action:** Update Src/README.md line 28: change InterfaceBridge.Json to Utilities.Json.

**Why:** Src/README.md is the canonical infrastructure inventory per the architecture catalog and Specs/README.md. When it says a module lives somewhere it does not, agents and developers who follow the README to find or avoid duplicating infrastructure are misdirected. The wrong-layer implication is the sharper edge: someone reading the README could conclude that Model referencing Json is a layer violation, when in fact it is perfectly legal because Json is in Utilities.

---
