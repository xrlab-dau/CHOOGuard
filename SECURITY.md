# Security policy

Report vulnerabilities through a private GitHub security advisory or the team's private channel. Do not open a public issue containing credentials, railway data, or infrastructure details.

## Restricted content

The following must remain outside Git and ordinary CI artifacts:

- raw or insufficiently de-identified railway imagery and video
- sensitive reconstructed geometry
- model checkpoints and access tokens
- Unity license files and account credentials
- private keys, `.env` files, and cloud credentials

Use encrypted approved storage, least-privilege access, retention limits, and audit logs. Generated geometry inherits the sensitivity of its source capture.
