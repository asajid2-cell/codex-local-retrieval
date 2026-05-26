# Demo Script

Goal: show that the app opens a local archive, searches inside chats, and copies useful context without changing source files.

## 30-60 Second Flow

1. Open the native app with sanitized sample data.
2. Show the archive list and selected transcript.
3. Switch to `Deep search` and search for `score export`.
4. Open the matching chat.
5. Click `Build restore packet`.
6. Show `Copy chat path` in the quick actions.

## Planned Capture

| asset | destination | screen or workflow | method | privacy checks | validation | placement | caption |
|---|---|---|---|---|---|---|---|
| hero screenshot | `docs/assets/hero-screenshot.png` | archive reader with sanitized sample data | WinUI app screenshot | reviewed; no local user path, no private transcript, no browser chrome | app built from this repo and isolated local app data | README | Native archive reader with local sample data |
| workflow video | `docs/assets/demo-workflow.webm` | deep search to restore packet | screen recording, no audio | same as screenshot plus clipboard not shown | replay steps above | README or release notes | Searching a local archive and building a restore packet |

The user-provided Snipping Tool capture from May 25, 2026 is not included because it contains private local paths and real chat content.
