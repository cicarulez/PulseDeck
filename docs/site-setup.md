# PulseDeck public site

Static site sources are in `site/`, for `https://pulsedeck.davidecappa.it`.
Public support and privacy contact: `support@davidecappa.it`, confirmed by the
project manager. No OAuth credentials belong in this site.

## GitHub Pages

Publish only `site/` as the Pages artifact using a GitHub Actions workflow.
Set the repository's Pages source to GitHub Actions. The site contains a CNAME
file, but also configure the custom domain in the repository Pages settings.
Enable HTTPS after the domain resolves and GitHub issues the certificate.

## DNS

At the DNS provider for `davidecappa.it`, create:

| Type | Name | Value |
| --- | --- | --- |
| CNAME | `pulsedeck` | `cicarulez.github.io` |

Use the DNS provider's default TTL. Do not create a wildcard record or a CNAME
alongside an existing A/AAAA record for the same hostname.
Verify the domain in GitHub Pages using the exact TXT name/value GitHub supplies.
For Google Search Console, add the Domain property `davidecappa.it`, verify using
Google's supplied DNS TXT record, and use a Google account that is an owner/editor
of the OAuth project. GitHub and Google verification records are distinct.

## OAuth configuration

Authorized domain: `davidecappa.it`.
Homepage: `https://pulsedeck.davidecappa.it/`.
Privacy: `https://pulsedeck.davidecappa.it/privacy.html`.
Use a Desktop OAuth client: authorization callbacks stay on the user's loopback
address, not this website. Publish the OAuth project to Production and complete
applicable brand/restricted-scope verification for public distribution.
Reconnect Gmail after the project/client changes to obtain a new authorization.
Pages alone do not remove Testing's seven-day token expiration.

References:
- https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site
- https://support.google.com/cloud/answer/13804266
- https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification
