# PulseDeck public site

The homepage, templates and styles are in `site/`; documentation comes from
repository Markdown. The generated site is for `https://pulsedeck.davidecappa.it`.
Public support and privacy contact: `support@davidecappa.it`, confirmed by the
project manager. No OAuth credentials belong in this site.

## GitHub Pages

The workflow `.github/workflows/pages.yml` builds and publishes only
`artifacts/site/`. It runs on homepage, documentation, template and version changes.
All documentation links are rewritten to HTML; source-code links lead to GitHub.
The build checks local page, image and anchor links before publication.

To preview the exact artifact locally:

```sh
python3 -m venv /tmp/pulsedeck-docs-venv
/tmp/pulsedeck-docs-venv/bin/pip install -r scripts/requirements-docs.txt
/tmp/pulsedeck-docs-venv/bin/python scripts/build-site.py
python3 -m http.server 8080 --directory artifacts/site
```

Open `http://localhost:8080`. Generated files stay outside source control.
The checked-in Markdown files remain directly readable on GitHub.

If the public homepage renders the repository README and raw `.md` links instead
of this homepage, check the Pages publishing source: a legacy branch/Jekyll build
can publish a different artifact. Repository settings must use this Actions
workflow. A local commit alone does not update the public site.
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
