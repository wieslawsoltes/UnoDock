# Architecture

## Model, presentation, hosting

UnoDock separates layout ownership from visual presentation. `LayoutRoot`, panels, panes, documents, tools, and floating nodes describe the workspace. Controls present those nodes and route interaction through shared docking/activation operations. Application content remains outside that model's lifecycle ownership.

`UnoDock.Core` contains portable contracts/algorithms. `UnoDock` supplies native Uno/WinUI controls, the layout model, serialization, and platform host integration. `UnoDock.Browser` is an optional adapter for separate browser runtimes; the main library does not depend on it.

```text
Application payloads / view models
              │
      LayoutRoot and ownership
              │
       UnoDock presentation
        ┌─────┴──────────┐
 Native desktop      BrowserDockingSession
 floating hosts             │
                   Browser session broker
                     ┌──────┴───────┐
                  Main Uno       Popup Uno
                  runtime        runtime(s)
```

## Preserve one source of truth

All in-host dock/split/float operations use the existing model operations. The browser adapter does not implement a second splitter or selection engine. Its broker owns *which browser runtime* may edit a content record; that runtime's normal UnoDock model owns *where the content is presented inside its local workspace*.

Cross-window transfer uses portable application payloads and increasing ownership leases. It does not share mutable CLR objects or ship UI trees. The target uses an application-provided view factory. This distinction permits real browser windows while retaining the native library's ownership and callback protections.

## Presentation conventions

Fluent modes compose stock controls and use semantic/scoped resources while retaining platform templates, focus visuals, and native state machines. Generic mode retains the independent legacy presentation. A consumer's explicit template or local resource is not silently replaced by a global theme decision.

The Ctrl+Tab navigator retains its public ListBox/container contracts, preview versus commit semantics, command vetoes, and focus model. The property inspector uses a native ListView but retains its registered field engine and draft/conflict policy.

## Failure semantics

Model mutation, activation, resizing, serialization, and browser ownership each have explicit validity boundaries. A callback exception does not universally imply rollback. Post-commit operations may have completed; independent cleanup is attempted before errors are propagated. Application side effects are not arbitrarily undone.

Read [model invariants](model-invariants.md), [docking/sizing](docking-sizing.md), and [browser workspaces](browser-workspaces.md) before integrating application callbacks that change ownership or replace resources during notification.
