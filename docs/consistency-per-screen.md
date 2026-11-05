# Consistency per screen: how stale may each screen be?

A read model is behind the journal by the projection's lag: usually a few hundred milliseconds,
and longer when a projection restarts or stops (section 13.7). This page decides, screen by
screen, what each one reads and what its user sees while it is behind (section 14.2). A new
screen gets a row here before it gets an endpoint.

## The rule

**Choose by what a wrong answer costs the person looking at the screen.** If acting on a stale
value strands someone or costs them money, read the aggregate. If they have just changed
something and will look for it, read the read model at least at the version they caused. If
nobody has just changed anything, read the read model as it is, and push changes when they
happen.

## The screens

| Screen | Read source | Guarantee | While it is stale, the user sees | Owner |
|---|---|---|---|---|
| Operator: site live board | `GET /sites/{id}/live-board` (`site_live_board`) | Eventual, pushed | The board as it was, with the time of its last change; nobody acts on one connector's status within a second | Sessions, with Energy |
| Driver: the active session | `GET /sessions/{id}?minVersion=n` after a command, then pushes (`GET /sessions/{id}/events`, or the hub's `session-{id}` group) | Read-your-writes, then pushed | "Updating…" beside the last known state while the read answers 202; never the old state as if it were the new one | Sessions |
| Driver: the receipt of an ended session | `GET /sessions/{id}` (`session_history`); the price from Billing (Chapter 16) | Eventual | "Your receipt is being prepared" until the session reads `ended` with its energy | Sessions, then Finance IT |
| Driver: is this connector free, before reserving | `GET /charge-points/{id}`: the aggregate | Up to date, for one charge point | Nothing stale; the reservation command checks again and may still be rejected | Sessions |
| Driver: session history | `GET /tokens/{uid}/sessions` (`session_history`) | Eventual | The list without a session that started a moment ago; "updating…" while the app has a command in flight | Sessions |
| Driver app: session cards and recent sessions | `GET /app/v1/tokens/{uid}/cards` and the change feed (section 14.6), then pushes to the hub's `token-{uid}` group | Eventual, pushed, with a catch-up feed | The cards it has, marked "updating…" until the feed has caught up after a reconnect | Sessions |
| Operations: projection status | `GET /projections/status` | Up to date: it reads the offsets and the journal, not a read model | Nothing stale; a stream that is behind shows as events behind and its lag | Sessions |

The site live board's push is the design, not yet the code: the board has no topic of its own in
Chapter 14, so the operator console polls it every few seconds until it gets one.

## The three tools, and what each costs

| Tool | Guarantee | Cost | Use it for |
|---|---|---|---|
| Ask the aggregate (`GET /charge-points/{id}`) | Up to date | One entity per question; wakes a passivated entity and replays it; a list is a fan-out of Asks | A decision about one thing whose staleness has a price: is the connector free |
| The read model with a minimum version (`?minVersion=n`) | Your own write, or an honest 202 | A few one-row reads every 50 ms while the projection is behind; a request held open up to the timeout (2 s) | The screen a user lands on right after a command |
| The read model as it is | Eventual | One query, whatever the number of entities | Lists, boards, history: everything else |

A push (Server-Sent Events or SignalR, sections 14.4 and 14.5) does not change the guarantee of
the screen it updates. It removes polling. It never replaces the read on (re)connect: pushes that
were sent while the client was away are gone.

## The words on the screen

- **Say "updating…", not nothing.** A read that answered 202 shows the last state it has with
  "updating…" beside it, and asks again after `Retry-After`.
- **Never fake certainty.** Do not show the state the client expects as if the server had
  confirmed it. An optimistic value is allowed only with a visible pending mark that the read
  model, not the command's 202, removes (Chapter 19).
- **Show the age when it matters.** A board that has not changed for a while says when it last
  did, so an operator can tell a quiet site from a stopped projection.
- **Do not spin for ever.** After a few 202s in a row the screen says that the history is late and
  offers to try again; the projection status says why.
