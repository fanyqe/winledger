# Data Format

WinLedger JSON reports are versioned documents. The initial schema version is `1.0`. Human-readable HTML, plain-text summary, registry `.reg`, and registry PowerShell `.ps1` exports are generated from the same comparison and rollback model. Report output paths use `.html` or `.htm` for HTML, `.txt` or `.text` for plain text, `.reg` for registry rollback files, and `.ps1` for registry rollback scripts.

Top-level registry, services, scheduled tasks, startup, environment, hosts file, firewall, installed applications, and file-system report shape:

```json
{
  "schemaVersion": "1.0",
  "sessionId": "00000000-0000-0000-0000-000000000000",
  "baselineSnapshotId": "00000000-0000-0000-0000-000000000000",
  "comparisonSnapshotId": "00000000-0000-0000-0000-000000000000",
  "comparedAt": "2026-07-23T00:00:00+00:00",
  "changes": [],
  "rollbackPlan": [],
  "warnings": []
}
```

Registry values store both their registry type and a canonical serialized value so diffing does not depend on display text.

Registry `.reg` rollback exports are Windows Registry Editor files generated from value-level rollback operations. They include supported registry value types and comments for warnings or values that cannot be represented safely in `.reg` syntax. They are not the internal source of truth and cannot perform WinLedger's expected-current-state validation during import.

Registry PowerShell rollback exports embed the versioned JSON rollback report and call `registry-rollback-apply` through WinLedger CLI. The script is a transport artifact; the structured JSON report remains the source of truth and the CLI executor performs expected-current-state validation before writing.

Service snapshots store both runtime state and persistent configuration:

- service name and display name;
- start mode;
- executable path;
- service account;
- current state;
- delayed automatic start;
- dependencies;
- optional description.

Service rollback operations are emitted only for start mode and delayed automatic start changes. The operation includes the expected current service state so execution can stop safely when the machine changed again after the comparison snapshot.

Scheduled task snapshots store both operational metadata and the task definition:

- full task path;
- folder path and task name;
- enabled state;
- current scheduler state;
- run-as user;
- privilege level;
- actions;
- triggers;
- definition XML.

Scheduled task rollback operations are emitted only for newly created tasks and enabled-state changes. The operation includes the expected current task definition so execution can stop safely when the task changed again after the comparison snapshot.

Startup snapshots group entries from several native subsystems:

- registry Run and RunOnce value name, location, command, and source view;
- current-user and common Startup folder file path, file size, and last write time;
- scheduled tasks that use logon or boot triggers;
- Windows services with automatic, boot, or system start modes.

Startup rollback operations are emitted only for newly created Startup folder entries. The operation includes the expected current entry metadata so execution can stop safely when the file changed again after the comparison snapshot. Startup entries that originate from registry, scheduled task, or service sources are left for their native subsystem reports and rollback commands.

Environment snapshots store user and machine variables:

- scope;
- variable name;
- raw value;
- registry-backed value type;
- PATH entries when the variable is PATH;
- source registry key.

PATH changes are reported as individual entries for additions, removals, and relative reordering. Environment rollback operations still restore or delete the full variable value because PATH edits can contain duplicate entries and ordering dependencies. JSON reports may contain raw environment values for rollback correctness; sensitive value summaries are redacted in human-readable change text.

Hosts file snapshots store:

- canonical hosts file path;
- whether the file existed at capture time;
- decoded text for line-based comparison;
- exact file bytes as base64 for rollback;
- SHA-256 hash and byte length;
- last write time when available;
- line number and raw line text for each decoded line;
- collection warnings.

Hosts file reports may contain internal hostnames, IP mappings, comments, and exact file bytes. Exports should be treated as local system configuration data and redacted before sharing outside the machine owner context.

Firewall snapshots store:

- rule identity and display name;
- description, grouping, application path, and service name;
- protocol name and raw protocol number;
- local and remote ports;
- direction and action;
- enabled state;
- raw profile bitmask and display profile names;
- local and remote address scope;
- interface types, ICMP types/codes, and edge traversal state;
- duplicate-name marker for rollback safety.

Firewall rollback operations are emitted only for newly created rules and enabled-state changes. The operation includes the expected current rule so execution can stop safely when the rule changed again after the comparison snapshot. Reports can expose local application paths, service names, port exposure, and network profile policy, so they should be reviewed before sharing.

Installed application snapshots store:

- stable registry-backed identity;
- current-user or machine scope;
- 32-bit or 64-bit registry view;
- source type for uninstall registration, MSI-marked product registration, or AppX/MSIX package registration;
- registry key path and key name;
- optional product code;
- display name, version, and publisher;
- install location, install source, and install date;
- uninstall, quiet uninstall, and modify commands;
- estimated size;
- Windows Installer and system component markers;
- release type, comments, and information URL;
- optional AppX/MSIX package full name, family name, package name, publisher identity, resource identity, manifest path, and inbox package marker;
- collection warnings.

Installed application rollback operations are not emitted in the current alpha. Reports include manual-review warnings because application and package removal can depend on vendor uninstallers, package ownership, shared files, services, drivers, deployment state, and current machine state. Reports can expose installed software inventory, AppX/MSIX package inventory, local paths, publisher names, product codes, package identities, manifest paths, and uninstall commands, so they should be reviewed before sharing.

File-system snapshots store:

- monitored roots and capture options;
- default exclusion patterns and whether high-noise paths were included;
- normalized path, root path, and relative path;
- file or directory kind;
- file size when available;
- creation and last-write timestamps;
- attributes;
- optional SHA-256 hash;
- optional small-file backup bytes as base64;
- whether rollback data exists;
- high-noise marker;
- collection warnings.

File-system rollback operations are emitted for created entries and for deleted or modified files when a small-file backup is available. The operation includes the monitored root, target path, expected current entry, restore entry, restore bytes when needed, administrator marker, and restart marker. Reports can expose local paths, file names, timestamps, hashes, and backed-up file contents, so they should be reviewed before sharing.
