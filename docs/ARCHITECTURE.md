# NetCheck architecture

## Design goals

NetCheck is designed around five constraints:

1. Diagnostics must be read-only and work without elevation.
2. One failed or unsupported probe must not abort the assessment.
3. Technical evidence and user-facing diagnosis must remain separate.
4. Windows-specific code must not leak into the domain or presentation logic.
5. Repairs must be evidence-based, explicitly approved, bounded to known actions, and independently reported.

## Layers

```text
NetCheck.App (WPF / MVVM)
        │
        ├──────────────┐
        ▼              ▼
NetCheck.Core ◄── NetCheck.Infrastructure
  models          Windows network APIs
  contracts       registry inspection
  engine          JSON persistence
  diagnosis       report exporters
```

`NetCheck.Core` has no Windows desktop dependency. It owns the diagnostic vocabulary, orchestration contract, ordered execution engine, and root-cause analysis. `NetCheck.Infrastructure` implements the operating-system and I/O boundaries. `NetCheck.App` is the composition root and owns WPF-specific behavior such as dialogs, commands, and visual state.

## Presentation and localization

The WPF shell uses MVVM navigation and a shared resource dictionary for the visual system. Dashboard, history, and settings views contain only presentation bindings; diagnostic behavior remains in Core and Infrastructure.

Matched English and German resource dictionaries provide the complete static interface. Diagnostic reports remain language-neutral in storage, while an application-layer projection localizes diagnoses, technical evidence, repair plans, history, dialogs, and exports. This allows an existing report to be re-rendered immediately after a language switch without coupling diagnostic or repair rules to presentation language.

The normalized `en` or `de` preference is stored with the local application settings so it survives restarts. The selected culture also controls visible dates, numbers, status labels, and exported report headings.

## Diagnostic execution

The engine captures a single network snapshot, orders checks by their declared stage, and publishes start/completion progress for each check. Results are added to a shared diagnostic context so later checks can make safe dependency decisions. For example, gateway and DNS checks skip when no usable local adapter exists, and stability sampling only runs against a public target that already replied.

Every probe executes inside an exception boundary. Cancellation returns a partial report with an explicit `Cancelled` outcome. An unexpected probe exception becomes a warning result with error evidence, while later probes continue.

## Root-cause analysis

`DiagnosisAnalyzer` receives completed evidence; probes do not choose the overall diagnosis. Rules prioritize the closest failing layer:

1. Adapter/link
2. IP assignment
3. Local gateway/route
4. DNS when direct IP access works
5. Captive portal/interception
6. Direct and web internet access
7. Connection stability
8. Non-blocking warnings such as proxy or ICMP behavior

This ordering keeps downstream symptoms from obscuring an earlier root cause.

## Repair execution

`NetworkRepairPlanner` maps correlated diagnostic results to the smallest applicable set of known repair actions. It deliberately returns manual guidance instead of an executable action for physical link failures, managed or static IP configuration, captive portals, isolated ICMP blocking, and connection-quality problems.

The dashboard displays every proposed change before execution. Repairs never begin automatically. When a plan needs administrator rights, the normal `asInvoker` application starts a short-lived elevated copy of itself with an opaque operation identifier. The helper accepts only validated `NetworkRepairActionId` values and invokes fixed Windows executables with structured argument lists; no arbitrary command or shell text crosses the elevation boundary. Results are returned per action through bounded temporary JSON files and then removed.

Repairs that do not require a restart are followed by a fresh diagnostic. Winsock and TCP/IP resets report that Windows must restart before verification.

## Persistence

History stores one JSON file per report under the current user’s local application data directory. Settings use a separate JSON file. Writes use a temporary sibling file followed by an atomic replacement, preventing a process interruption from leaving a partially written file.

History loading isolates malformed or inaccessible report files. A bad report is skipped rather than making history unavailable.

## Export

The exporter supports HTML, JSON, and plain text. HTML values are encoded before insertion. Exports always redact MAC addresses and redact the computer name unless the user explicitly enables it. Files are written atomically.

## Error handling

- Probe exceptions are converted into warning results by the engine.
- Expected file and permission failures are explained in the UI.
- Unhandled WPF, task, and AppDomain exceptions are logged locally.
- Logging failures are swallowed to prevent recursive crashes.
- Repairs are opt-in, show their complete plan, and request elevation only after confirmation.

## Extension points

Add a diagnostic by implementing `IDiagnosticCheck`, giving it a unique ID and order, then registering it in the application composition root. Add a storage or export format behind the existing Core interfaces. Diagnosis changes belong in `DiagnosisAnalyzer` and should include regression tests.
