# loop

## MODIFIED Requirements

### Requirement: Inbox cards put the athlete's decision first

`/me/inbox` SHALL render each pending interest as a card with a coach scene photo (pool by coach id), the coach's name, role, club and league, the note, a teal "Αποδοχή" button (`bp-btn-athlete`) and a ghost "Όχι τώρα". An accepted interest SHALL show a teal "Συνδεθήκατε" banner, a teal "Στείλτε μήνυμα" button (`bp-btn-athlete`) that opens the conversation, and the club contact as a secondary ghost "Στείλτε email" `mailto:` link, with no note that the contact is fictional.

#### Scenario: Pending
- **WHEN** an athlete with a pending interest opens /me/inbox
- **THEN** the accept button has class `bp-btn-athlete` and the card shows a stock coach photo

#### Scenario: Accepted
- **WHEN** the athlete accepts
- **THEN** the card shows "Συνδεθήκατε", a teal link to `/messages/with/{interestId}` and a ghost `mailto:` link to the `.invalid` club address
