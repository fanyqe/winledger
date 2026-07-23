# Security Policy

Please report sensitive security issues privately to the repository owner.

WinLedger works with local system configuration data. Do not include secrets, registry exports, logs, or machine-specific reports in public issues unless they have been reviewed and redacted.

Supported security expectations for the current alpha:

- local-only storage;
- no telemetry by default;
- explicit export;
- rollback validation before mutation;
- Startup folder rollback refuses paths outside known Windows Startup folders;
- environment variable summaries redact sensitive-looking values, while exported rollback data may still contain raw local values;
- hosts file rollback refuses paths outside the canonical Windows hosts file and validates exact post-change bytes before writing or deleting;
- firewall rollback validates exact post-change rule state and blocks automatic mutation when duplicate rule names are present;
- installed application rollback emits manual-review warnings instead of running uninstall commands;
- file-system rollback refuses paths outside the monitored root, refuses reparse points, and validates expected current metadata before mutation.

Generated PowerShell rollback scripts embed the structured JSON rollback report and can contain the same local system configuration data as JSON exports. Review them before sharing.

Hosts file reports can contain internal hostnames, IP mappings, and exact file bytes. Review and redact them before sharing.

Firewall reports can contain local executable paths, service names, port exposure, address scopes, and network profiles. Review and redact them before sharing.

Installed application reports can contain installed software inventory, product codes, local installation paths, publisher names, and uninstall commands. Review and redact them before sharing.

File-system reports can contain local paths, filenames, timestamps, hashes, and backed-up file bytes. Review and redact them before sharing.
