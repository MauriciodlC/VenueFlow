# Personas

VenueFlow has four roles. A user account holds exactly one role; a host account is linked to the venues it owns. All data in development, demos and tests is synthetic.

## Guest

Plans a workshop, photo shoot or team session and needs a space for a specific number of people at a specific time.

- **Goals:** describe what they need in plain language, see only slots that can actually be booked, know the full price before committing, get a confirmation they can show at the door.
- **Can:** search published venues, view available slots, request a quote, place a short hold, confirm or cancel their own bookings, join a waitlist, ask questions about a venue's policies.
- **Cannot:** see other guests' bookings, see unpublished venues, change a price.
- **Pain points today:** translating "step-free room for 10 on Saturday afternoon" into rigid filters; finding out about house rules after paying.

## Venue host

Runs one or more small venues (studios, workshop rooms, coworking spaces) and wants bookings without double-booking or manual calendar juggling.

- **Goals:** publish spaces with accurate capacity, schedules and policies; control pricing; see upcoming bookings and check guests in.
- **Can:** create venue drafts, add resources (rooms or classes), set opening rules and closures, set prices, upload policy documents, submit venues for review, manage bookings for their own venues.
- **Cannot:** access another host's venues or bookings, publish without review, silently close a slot that already has bookings.
- **Pain points today:** overlapping requests from several channels; guests asking the same policy questions repeatedly.

## Staff

Works on site for a host: checks guests in and handles problems on the day.

- **Goals:** find today's bookings quickly, verify a confirmation code, record check-in or no-show.
- **Can:** view bookings and check guests in for the venues they are assigned to.
- **Cannot:** change prices, schedules or venue details.

## Admin

Operates the marketplace.

- **Goals:** keep listings trustworthy, resolve disputes, watch the booking funnel.
- **Can:** review and approve or reject venue listings, view audit history, resolve disputes, see aggregate metrics.
- **Cannot:** create bookings on behalf of guests or change a confirmed booking's price snapshot.
- Every admin action is written to the audit log.

## Role summary

| Capability | Guest | Host | Staff | Admin |
|---|---|---|---|---|
| Search published venues | ✓ | ✓ | ✓ | ✓ |
| Hold and book a slot | ✓ | | | |
| Create and edit venue drafts | | own venues | | |
| Set schedules and prices | | own venues | | |
| Check guests in | | own venues | assigned venues | |
| Approve listings | | | | ✓ |
| View audit log | | own venues | | ✓ |
