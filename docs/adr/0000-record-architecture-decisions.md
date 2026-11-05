# 0000. Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-11-04
- **Book section:** ch01-s03

## Context

The Ohmline sample is built one book section at a time, and a reader can arrive at any tag. Code
shows what was decided; it rarely shows why, which alternatives were rejected, or what the
decision costs. Many of the sample's decisions are also hard to reverse once data exists: a
persisted event name, a shard type name, a serializer id.

## Decision

We will record every architecturally significant decision as an Architecture Decision Record in
`docs/adr/`, in the format Michael Nygard described in "Documenting Architecture Decisions" (2011):
context, decision and consequences.

- Files are named `NNNN-short-title.md`, numbered from `0000` in order. A number is never reused.
- Each record starts from `docs/adr/template.md` and names the book section (`chNN-sMM`) that
  introduces it.
- A record is written in the same commit as the change it explains.
- Once accepted, a record is not edited, apart from its status line and links. A changed decision
  gets a new record, and the old record's status becomes "Superseded by NNNN".

## Consequences

- Every decision in the sample has one place to look, and `git log docs/adr` gives its history.
- Records are append-only, much like the events the sample will persist: the past stays readable
  as it was, and changes of mind are new facts that point back at the old ones.
- Writing a record costs a few minutes per decision. Decisions that are cheap to reverse do not
  need one.
