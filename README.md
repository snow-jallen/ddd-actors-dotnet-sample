# Ohmline

This is the companion sample repo for the book *Domain-Driven Design with the Actor Model on .NET:
functional core, actor shell with C# 15, .NET 11 and Akka.NET*.

Ohmline is a fictional operator of public EV charge points. The sample builds its software one
book section at a time: a pure functional domain core for charge points, charging sessions, site
power budgets and tariffs, wrapped in an Akka.NET actor shell that persists, shards and integrates
it. Start with [the domain primer](docs/ohmline-primer.md) if the domain is new to you.

## How the history maps to the book

- `main` has a linear history. Every book section that changes the sample adds one commit, usually
  exactly one.
- Each of those commits carries an annotated tag named after its section: `chNN-sMM` is the state
  of the sample at the end of section NN.MM. For example, `ch07-s04` is the end of section 7.4.
- Sections that only explain ideas change nothing, so they have no tag. Gaps in the numbering are
  expected: there is no `ch01-s01` because section 1.1 has no code.
- Commit subjects repeat the section title (`ch01-s02: Meet Ohmline: one charge, told end to end`),
  and every commit has a `Book-Section: chNN-sMM` trailer.

## Following along with tags

```bash
git tag --list 'ch*'                    # every section tag, in book order
git checkout ch01-s02                   # the sample as it is at the end of section 1.2
git diff ch01-s02 ch01-s03 --stat       # which files section 1.3 changed
git diff ch01-s02 ch01-s03 -- docs      # the same diff, limited to one folder
git log --format='%h %s' ch01-s02..ch01-s04
git checkout main                       # back to the latest state
```

A checked-out tag leaves you on a detached `HEAD`. To experiment from a tag, create a branch of
your own first: `git switch -c my-experiments ch07-s04`.

The usual pattern is `git diff <previous tag> <tag>`, where the previous tag is the last tag
before the one you are reading. Because tags exist only for sections with code, the previous tag
is not always the previous section.

## Building and testing

You need the .NET SDK named in `global.json` (11.0.100-rc.3 at the time of writing, or a later
11.0 SDK). Later chapters add more requirements:

- Podman or Docker for the integration tests, from Chapter 12. With rootless Podman, export
  `DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock` first.
- The Aspire CLI 13.6 to run the local cluster, from Chapter 12.
- The `maui-android` workload, an Android SDK and a JDK, only to build the driver app's .NET MAUI
  head in `mobile/` (Chapter 19).

From the repo root:

```bash
dotnet build Ohmline.slnx -warnaserror
dotnet test --solution Ohmline.slnx
tools/verify-tags.sh                    # build and test every tag in a temporary worktree
```

`global.json` pins the SDK and selects the Microsoft.Testing.Platform runner, which is why
`dotnet test` takes `--solution`. Until section 2.1 adds the first project, `Ohmline.slnx` is
empty and the two `dotnet` commands fail with "Unable to find a project to restore!";
`tools/verify-tags.sh` reports those tags as skipped.

The Chapter 1 tags (`ch01-s02` to `ch01-s07`) hold documents and setup only, so they are exempt
from the build-and-test check. From `ch02-s01` on, every tag must build with `-warnaserror`, pass
all of its tests and leave no files behind, and `tools/verify-tags.sh` fails any tag that does not.

## Side branches

Later chapters add branches next to `main`. None of them is ever merged.

| Branch | Meaning |
|---|---|
| `wrong/<tag>-<slug>` | A deliberate mistake made at `<tag>`. It builds; a test whose name starts with `Demonstrates_` shows the failure. |
| `alt/<tag>-<slug>` | A design alternative to the choice the book makes at `<tag>`. |
| `upgrade/akka-1.6` | The platform upgrade from Akka.NET 1.5 to 1.6. |

## Tag index

| Tag | Section | What it adds |
|---|---|---|
| `ch01-s02` | 1.2 Meet Ohmline: one charge, told end to end | This README, the licence, git settings, `docs/ohmline-primer.md` |
| `ch01-s03` | 1.3 Models, language and the people who know the business | `docs/glossary.md`, the ADR template, ADR 0000 |
| `ch01-s04` | 1.4 Consistency without distributed transactions | ADR 0001: one writer per aggregate |
| `ch01-s06` | 1.6 Functional core, actor shell | ADR 0002, a first-draft `docs/vision.md` |
| `ch01-s07` | 1.7 Setting up the workbench | `global.json`, `Directory.*.props`, `NuGet.config`, `.editorconfig`, an empty `Ohmline.slnx`, `tools/verify-tags.sh` |

## Licence and disclaimers

The code and documents are released under the [MIT licence](LICENSE).

Every company in the sample is fictional: Ohmline, TallyPay (payments), Northgate Grid (the grid
operator) and AssetTrack (a legacy asset registry). The charger protocol is a simplified subset in
the style of OCPP 1.6J; it makes no claim of conformance to any OCPP version.
