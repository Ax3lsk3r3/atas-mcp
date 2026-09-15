# Security Policy

## Supported Versions

| Version | Supported |
|---|---|
| 1.0.x | Yes |

---

## Reporting a Vulnerability

If you discover a security vulnerability within ATAS MCP Bridge:
1. Please do not open a public issue.
2. Email the maintainers directly or use GitHub Private Vulnerability Reporting on the repository.
3. Include detailed steps to reproduce the issue.

---

## Trading and Execution Security Best Practices

1. **Local Isolation:** The ATAS MCP Bridge HTTP server binds strictly to `127.0.0.1` / `localhost`. Never expose this port to public networks or open interfaces without authentication and TLS termination.
2. **Account Safeguards:** Always verify tool configurations on paper-trading, demo, or simulated accounts prior to connecting live brokerage accounts.
3. **Execution Limits:** Review orders and lot sizes when using autonomous trading agents to avoid unintended financial risk.
