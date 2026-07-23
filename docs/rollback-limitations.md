# Rollback Limitations

WinLedger does not claim perfect machine restoration.

Registry rollback supports:

- restoring a previous registry value;
- deleting a newly created registry value;
- recreating a removed registry value;
- validating that the current value still matches the post-change snapshot before writing;
- applying a selected rollback operation from the WPF app or CLI;
- exporting a reviewable `.reg` rollback file for supported value-level operations;
- exporting a PowerShell `.ps1` rollback script that delegates execution to WinLedger CLI.

The initial registry rollback does not automatically restore or delete whole registry keys. Key-level rollback is marked for manual review because keys may contain unrelated values created after the tracked session. `.reg` exports cannot validate that the current registry still matches the tracked post-change state before import; use the JSON-backed WinLedger rollback command or the generated `.ps1` script when validation-first rollback is required.

Some registry changes may require restarting Windows or restarting an affected process before behavior changes.

Service rollback supports:

- restoring a previous service start mode;
- restoring a previous delayed automatic start setting;
- validating that the current service configuration still matches the post-change snapshot before writing.

Service rollback does not automatically create, delete, start, stop, or reconfigure service executable paths, accounts, or dependencies. Those changes are marked for manual review or unavailable because the safe recovery path depends on external files, credentials, dependent services, and current machine state.

Scheduled task rollback supports:

- deleting a newly created scheduled task;
- restoring a previous enabled or disabled state;
- validating that the current task definition still matches the post-change snapshot before writing.

Scheduled task rollback does not automatically recreate removed tasks or restore action, trigger, run-as user, privilege level, or full XML definition changes. Those changes may require credentials, protected folders, external executables, or context that cannot be safely reconstructed without manual review.

Startup rollback supports:

- deleting a newly created current-user or common Startup folder entry;
- validating that the current file path, size, timestamp, and entry metadata still match the post-change snapshot before deleting.

Startup rollback does not automatically modify registry Run keys, scheduled tasks, or Windows services. Those sources are grouped in the Startup view for visibility, but their rollback paths remain tied to their native subsystem reports because each source has different validation and privilege requirements.

Environment rollback supports:

- deleting a newly created user or machine environment variable;
- restoring a removed or modified environment variable;
- restoring the full previous PATH value after entry-level PATH changes;
- validating that the current variable still matches the post-change snapshot before writing.

Environment rollback does not patch individual PATH entries in place. It restores the full previous variable value to avoid corrupting duplicate entries, ordering dependencies, or concurrent edits. Machine environment rollback requires administrator rights. Existing running processes may keep their old environment block until they are restarted, the user signs out, or Windows is restarted.

Hosts file rollback supports:

- restoring the previous full hosts file bytes;
- deleting a newly created hosts file;
- validating that the current hosts file still matches the post-change snapshot before writing or deleting.

Hosts file rollback does not patch individual lines in place. The whole tracked previous file is restored to avoid corrupting concurrent edits, duplicate mappings, comments, line endings, or encoding-sensitive content. Hosts file rollback requires administrator rights. DNS cache, browser cache, and long-running applications may need to be refreshed before all name resolution behavior reflects the restored file.

Firewall rollback supports:

- deleting a newly created firewall rule;
- restoring the previous enabled or disabled state;
- validating that the current rule still matches the post-change snapshot before writing.

Firewall rollback does not automatically recreate removed rules or restore action, direction, ports, protocol, profiles, address scope, application path, service name, interface type, edge traversal, description, or grouping changes. Those changes are marked for manual review because the safe recovery path may depend on duplicate rule names, application identity, service ownership, and current network policy. Firewall rollback requires administrator rights.

Installed application rollback supports:

- generating manual-review warnings for every detected registration or metadata change;
- exporting the expected post-change registration and AppX/MSIX package metadata for review.

Installed application rollback does not automatically uninstall applications, remove AppX/MSIX packages, recreate removed registrations, delete registry keys, or run uninstall commands in the current alpha. Those operations can affect shared files, services, drivers, licensing state, vendor maintenance data, deployment state, and package ownership outside the captured registration. A reviewer should use the report as evidence, then choose the vendor-supported uninstall, repair, or package deployment path when needed.

File-system rollback supports:

- deleting a newly created file;
- deleting a newly created empty directory;
- restoring a deleted file when the baseline snapshot contains backup bytes;
- restoring a modified file when the baseline snapshot contains backup bytes;
- validating that the current file-system entry still matches the tracked post-change snapshot before writing or deleting.

File-system rollback does not automatically restore files without backup data, delete non-empty directories, follow reparse points, or mutate paths outside the monitored root. Hash-backed renames are detected for reporting but require manual review in the current alpha. Large files are not backed up unless the configured size limit allows it. The current alpha uses selected-root scanning; NTFS USN Journal integration remains future work.
