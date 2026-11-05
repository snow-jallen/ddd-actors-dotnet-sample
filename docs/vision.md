# Ohmline domain vision statement

*Drafted in section 1.6 and revised in section 5.8, once the subdomains had been classified and
the contexts drawn. `docs/highlighted-core.md` says where in the model the core lives.*

Ohmline turns limited grid connections into reliable, fairly priced charging.

At most of Ohmline's sites, power is scarcer than chargers. A car park whose grid connection
could run four chargers at full speed hosts six or ten, and that is only safe and profitable if
Ohmline shares the site's capacity well. Ohmline's model decides, many times a minute, how much
power each charging session at a site may draw. It respects the grid connection limit, any
curtailment the grid operator has asked for, a safety margin, each connector's minimum and
maximum, and the priority of fleet customers. When a car arrives, a car leaves or the grid
operator asks for power back, every allocation at the site is recomputed within seconds, and the
site never draws more than it is allowed to. No off-the-shelf product does this for Ohmline's mix
of sites, and it is the main reason a site owner chooses Ohmline.

The charging session is where that power becomes a driver's charge, and it is the second half
of the core. A session must be right even when the world is not: a charge point that loses its
connection underground and reports its readings late, a register that jumps backwards, a car
that pauses itself while the site is curtailed. Every reading is kept, nothing reported by a
charge point is silently thrown away, and every session ends in a state that operations,
Billing and the driver agree on.

Drivers return when they can trust what they pay. The price of a session is fixed by the tariff
in force when it starts, a hold on the driver's card covers it in advance, the amount captured
matches the price breakdown to the cent, and the receipt explains every line. Prices are
Ohmline's to set, but they are data, not a competitive edge: the pricing analyst changes them
without a release.

Everything else is done well enough and no better. Card payments are bought from a payment
provider. Grid signals, the legacy asset registry and roaming partners are integrated behind
translators that keep their language out of the model. Chargers are bought from vendors and
spoken to in their own protocol at the edge. Driver accounts and billing are kept deliberately
simple. When a choice must be made between polishing one of these and improving how power is
shared or how sessions survive failure, the core wins.
