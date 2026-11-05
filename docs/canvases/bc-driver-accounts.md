# Bounded Context Canvas: Driver Accounts

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1.*

## Name

**Driver Accounts**

## Purpose

Know which id tokens exist and whether each one may start charging right now, so that a card
reported lost stops working everywhere and one token never runs two sessions at once.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Supporting (driver accounts and tokens) | Engagement creator | Product |

## Domain roles

- **Approver:** Charging asks it whether a token may start a session, and it says yes (a token
  lock) or no.
- **Enforcer:** one active session per token, across every site.

## Inbound communication

Every message in these tables is a public contract: it lives in
`Ohmline.DriverAccounts.Contracts`, crosses a serializer and changes only by adding a version.

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Driver app, support staff (HTTP) | `RegisterToken`, `BlockToken` | Command | `Ohmline.DriverAccounts.Contracts` |
| Charging (journey, subscribers) | `LockForSession`, `UnlockFromSession` | Command | `Ohmline.DriverAccounts.Contracts` |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (journey) | `TokenLocked`, `TokenLockRefused` | Event (reply) | `Ohmline.DriverAccounts.Contracts` |

## Ubiquitous language

| Term | Meaning here |
|---|---|
| Id token (token) | An RFID card or app credential, identified by its UID |
| Token lock | A token held by one running session; a locked token cannot start another |
| Blocked token | A token that may not start any session, for example a card reported lost |
| Driver | An opaque `DriverRef`; Driver Accounts is the only context that may ever map it to a person |

## Business decisions

- A token belongs to one account.
- A token has at most one active session; a second `LockForSession` for another session is
  refused.
- A blocked token is refused immediately, but a session it already started is allowed to finish.
- Unlocking a token that is not locked is not an error; the request is simply already satisfied.

## Assumptions

- Token UIDs are unique across card vendors once normalized to upper-case hexadecimal.
- A lock that is never released (a lost `UnlockFromSession`) is rare and is cleaned up by the
  charging journey's timeout.

## Verification metrics

- Zero tokens with two active sessions.
- A blocked token is refused at every site within 5 s of being blocked.

## Open questions

- Should a fleet card be allowed two sessions at once (two vehicles, one card)? Today's answer
  is no.
- How long may a lock stay held before support is alerted?
