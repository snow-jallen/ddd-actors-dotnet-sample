# Contributing: commits and releases

*Section 21.3. Read it with ADR 0016 and `.github/workflows/release-please.yml`.*

From `ch21-s03` on, every commit to `main` that should appear in a release note follows
[Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). The release process
reads the commit messages, so the message is where a change says what kind of change it is.

## The commit message

```text
<type>(<scope>): <what changed, in the imperative, lower case>

<why, in plain prose, wrapped at 100 characters>

<footers>
```

| Type | Use it for | Release |
|---|---|---|
| `feat` | A capability that operators, drivers or another team can use | MINOR |
| `fix` | A defect, in behaviour that was already promised | PATCH |
| `perf` | Faster or cheaper, with the same behaviour | PATCH |
| `docs`, `test`, `refactor`, `build`, `ci`, `chore` | Everything else | none |

**Scopes** are the bounded contexts and the deployables, in their durable spelling:
`charging`, `site-energy`, `tariffs`, `accounts`, `billing`, `gateway`, `mobile`, `deploy`,
`shell` (the actor shell and the shared kernel). One scope per commit; a change that needs two is
usually two commits.

**Breaking changes** are marked with `!` after the scope (`feat(charging)!: ...`) and a
`BREAKING CHANGE:` footer that says what the operator must do. For a service, breaking means
what ADR 0016 says: the release cannot roll over the previous one. A new event version, a new
optional field or a new endpoint is never breaking.

**Footers:**

- `Release-As: 1.0.0` asks the next release PR for exactly that version, once.
- `Book-Section: chNN-sMM` names the book section a commit belongs to (sample repo only).

Examples:

```text
feat(charging): idle fee cap per tariff
fix(gateway): close a charger's socket when its heartbeat times out
feat(site-energy)!: allocate in whole amperes

BREAKING CHANGE: nodes before this release round allocations differently; deploy blue/green.
```

## How a release happens

1. Pull requests are merged to `main` with their Conventional Commit messages kept.
2. On every push to `main`, release-please updates one open **release PR**. It proposes the next
   version from the commits since the last release (`feat` gives MINOR, `fix` and `perf` PATCH,
   `!` MAJOR) and the `CHANGELOG.md` section for it.
3. Merging the release PR commits the new `CHANGELOG.md` and `.release-please-manifest.json` and
   tags that commit `vX.Y.Z`.
4. MinVer reads the version from the tag (section 21.2): assemblies, the cluster's app version
   and the container image tags (section 21.5) all follow from it. No project file holds a
   version number, and no person types one.

`release-please-config.json` uses the `simple` release type, which needs nothing but a changelog:
release-please has no .NET release type, and MinVer does not need it to write the version anywhere.

## The book's history before Conventional Commits

The sample's history uses `chNN-sMM: <section title>` subjects, one commit per book section, and
keeps them for the book's sections after this one too. release-please treats a subject that is
not a Conventional Commit as invisible: it is in no changelog and bumps nothing. Two settings make
this explicit:

- `bootstrap-sha` in `release-please-config.json` is the commit before `ch21-s03`. release-please
  never reads history older than that, so the first release PR does not wade through the
  book's first twenty chapters.
- `.release-please-manifest.json` starts at `0.9.0`: what the sample would have been released as,
  had it been released before. The first release after it is `1.0.0` (section 21.9).
