# Project Overview

Codex Local Retrieval exists because local session archives can outlive the app sidebar that originally displayed them. The app gives those files a read-only browser: search the index, inspect messages, copy useful code, and build a handoff prompt for continuing work elsewhere.

The main design choice is separation between source data and app metadata. Source JSONL files and local Codex state are treated as inputs. Pins, collections, renamed titles, tags, and UI settings belong to the app store under `%LocalAppData%`.

The current public build includes sanitized sample data so contributors can build and test the app without exposing private conversations. Real user archives should stay outside the repository.

Optional AI support is deliberately provider-based rather than account-based. The app stores provider metadata in local app settings and stores API keys through Windows credentials. Ask Archive retrieves local excerpts first, then sends only those excerpts to the configured OpenAI-compatible provider.

## Tradeoffs

- Local-first storage avoids server risk, but it does not sync between machines.
- The app can search indexed content quickly, but the public import UI is not finished.
- Restore packets are useful for handoff, but they can contain private context and should be reviewed before sharing.
- A portable ZIP is easy to publish and inspect, but it is not as polished as a signed MSIX installer.
