## MODIFIED Requirements

### Requirement: Interest lifecycle
The system SHALL record when an athlete saw an interest and how they replied.

#### Scenario: Opening the inbox marks seen
- **WHEN** an athlete opens /me/inbox
- **THEN** their unseen interests get SeenAt once; reopening does not change it

#### Scenario: Reply access control
- **WHEN** POST /me/inbox/{id}/reply is sent
- **THEN** missing antiforgery gives 400, anonymous redirects to /demo, a coach or a different athlete gives 403, only the owner may reply
- **AND** an accepted reply shows the club contact with a reserved `.invalid` address (never a real mailbox), without a "(demo)" tag or a "fictional address" note

### Requirement: Sent page
The system SHALL show a coach every sent interest with its state, as cards in two columns with a horizontal stepper whose coach steps (Στάλθηκε, Είδε) are orange dots and whose athlete step (Απάντηση) is a teal dot, joined by an orange-to-teal connector. The page's one primary action is "Επόμενο ταίριασμα" to `/match`; an empty list shows a photo band with the same action.

#### Scenario: Stepper
- **WHEN** a coach opens /me/sent
- **THEN** each sent interest shows Sent, Seen, Replied with states and timestamps, and the athlete chip links to the profile
- **AND** the nav link shows the number of unread replies

#### Scenario: Stepper colours
- **WHEN** a coach opens /me/sent
- **THEN** the Sent and Seen steps carry the coach (orange) step class and the Replied step carries the athlete (teal) step class

#### Scenario: Empty
- **WHEN** a coach with no sent interest opens /me/sent
- **THEN** the page shows the empty-state photo and a link to `/match`

## ADDED Requirements

### Requirement: Inbox cards put the athlete's decision first

`/me/inbox` SHALL render each pending interest as a card with a coach scene photo (pool by coach id), the coach's name, role, club and league, the note, a teal "Αποδοχή" button (`bp-btn-athlete`) and a ghost "Όχι τώρα". An accepted interest SHALL show a teal "Συνδεθήκατε" banner, the club contact address and a teal "Στείλτε email" `mailto:` link, with no note that the contact is fictional.

#### Scenario: Pending
- **WHEN** an athlete with a pending interest opens /me/inbox
- **THEN** the accept button has class `bp-btn-athlete` and the card shows a stock coach photo

#### Scenario: Accepted
- **WHEN** the athlete accepts
- **THEN** the card shows "Συνδεθήκατε", the `.invalid` club address and a `mailto:` link to it
