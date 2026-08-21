# NetCheck architecture

## Design goals

NetCheck is designed around four constraints:

1. Diagnostics must be read-only and work without elevation.
2. One failed or unsupported probe must not abort the assessment.
3. Technical evidence and user-facing diagnosis must remain separate.
4. Windows-specific code must not leak into the domain or presentation logic.

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

## Presentation and menu language

The WPF shell uses MVVM navigation and a shared resource dictionary for the visual system. Dashboard, history, and settings views contain only presentation bindings; diagnostic behavior remains in Core and Infrastructure.

English is the default and remains the language for diagnoses, settings, reports, and technical evidence. The navigation menu can be switched independently between English and German. Its normalized `en` or `de` preference is stored with the local application settings so it survives restarts without changing the process culture or diagnostic output.

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
- NetCheck never attempts an automatic repair or requests elevation.

## Extension points

Add a diagnostic by implementing `IDiagnosticCheck`, giving it a unique ID and order, then registering it in the application composition root. Add a storage or export format behind the existing Core interfaces. Diagnosis changes belong in `DiagnosisAnalyzer` and should include regression tests.
