# Security Policy

## Reporting a Vulnerability

Please do not report security issues through public GitHub issues.

If you find a vulnerability in this repository, use one of these channels:

- GitHub private vulnerability reporting, if it is enabled for this repository
- Email: `268482255+tatarecode@users.noreply.github.com`

Please include:

- a short description of the issue
- affected file, script, or workflow
- steps to reproduce
- expected impact
- any suggested fix or mitigation, if available

## Supported Versions

Security fixes are primarily applied to the latest state of the `main` branch and the latest published release.

Older releases may not receive backported fixes.

## Scope

This repository contains build scripts, packaging scripts, a Windows GUI, and supporting tools for voice-replacement workflows.

Reports are most helpful when they involve:

- command execution paths
- dependency download and setup flows
- release packaging or publishing scripts
- file overwrite, path traversal, or unsafe path handling
- secret handling or credential exposure

Out-of-scope examples usually include:

- local game mod conflicts
- broken third-party dependencies upstream
- issues that require an already-compromised local machine

## Response

Best effort will be made to confirm the report, assess severity, and prepare a fix.

If the report is valid, the goal is to ship a fix and then disclose the issue publicly after a patched version is available.
