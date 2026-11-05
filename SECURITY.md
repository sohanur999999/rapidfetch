# Security Policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Send a private report
through GitHub Security Advisories:

https://github.com/sohanur999999/rapidfetch/security/advisories/new

Include:

- the affected version;
- steps to reproduce;
- the impact;
- any proof-of-concept or logs that help confirm the issue.

You can expect an acknowledgement within 72 hours. Please give us time to
release a fix before publishing details.

## Supported versions

The latest release is supported with security updates. Older builds may not
receive fixes.

## Scope

In scope:

- the RapidFetch desktop app and installer;
- the browser extension;
- the native messaging host;
- the localhost bridge.

Out of scope:

- vulnerabilities in third-party sites, yt-dlp, ffmpeg, Chrome, or Windows;
- social engineering;
- denial-of-service against your own machine.

## Safe defaults

RapidFetch does not run a remote service, does not require an account, and does
not accept remote code. The extension communicates with the desktop app over
native messaging or `127.0.0.1`.
