# Threat Model

WinLedger snapshots may contain sensitive local information such as file paths, usernames, installed software, network rules, and registry configuration.

Initial controls:

- data is stored locally;
- user SID is hashed in session metadata;
- reports are explicit exports;
- registry rollback validates expected current state before writing;
- startup folder rollback validates expected current file metadata and rejects paths outside known Startup folders before deleting;
- environment rollback validates expected current values before writing and redacts sensitive variable values in summaries;
- hosts file rollback validates expected current file bytes before writing and refuses arbitrary file paths;
- firewall rollback validates expected current rule state before writing and avoids duplicate rule names;
- installed application rollback emits manual-review warnings instead of executable uninstall operations;
- file-system rollback validates expected current metadata before mutation, refuses paths outside the monitored root, and refuses reparse points;
- no telemetry, cloud account, advertisements, hidden network calls, or remote analysis services are added.

Hosts file snapshots can reveal internal hostnames, IP mappings, local development domains, blocked domains, comments, and exact file bytes retained for rollback. Users should review and redact exported reports before sharing them.

Firewall snapshots can reveal local executable paths, service names, allowed ports, blocked ports, address scopes, and network profiles. Users should review and redact exported reports before sharing them.

Installed application snapshots can reveal installed software inventory, AppX/MSIX package inventory, product codes, publisher names, package identities, manifest paths, installation paths, install sources, and uninstall commands. Users should review and redact exported reports before sharing them.

File-system snapshots can reveal local paths, file names, timestamps, hashes, and backed-up file bytes. Users should review and redact exported reports before sharing them.

Future work:

- export redaction options;
- restrictive permissions for rollback backup data;
- elevated helper process with a structured operation contract;
- audit log for privileged rollback execution.
