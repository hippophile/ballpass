# messages Specification

## Purpose
TBD - created by archiving change messages. Update Purpose after archive.

## Requirements

### Requirement: Messaging only after an accepted interest

The system SHALL allow messages only inside a conversation that belongs to an accepted interest, and only between that interest's
coach and athlete. Accepting an interest SHALL create the conversation; a coach note SHALL become its first coach message.

#### Scenario: Accept creates the conversation
- **WHEN** an athlete accepts an interest that carries a note
- **THEN** exactly one conversation exists for it and its first message is the note, sent by the coach

#### Scenario: No conversation without an accepted interest
- **WHEN** a participant opens `/messages/with/{interestId}` for a pending or declined interest
- **THEN** the response is 404 and no conversation is created

#### Scenario: Only the two participants
- **WHEN** another coach or another athlete opens `/messages/{id}` or posts to `/messages/{id}/send`
- **THEN** the response is 403 and nothing is stored; anonymous users are sent to `/demo`

#### Scenario: Antiforgery
- **WHEN** a send, block or report POST has no valid antiforgery token
- **THEN** the response is 400

#### Scenario: Body validation
- **WHEN** the body is empty (after trimming) or longer than 1000 characters
- **THEN** nothing is stored and the thread shows a validation message

### Requirement: Athlete safety controls

The athlete SHALL be able to report a conversation (stored with a reason) and block it. A blocked conversation is closed: the coach's
send is refused and nothing is stored. Only the athlete may report or block (403 for the coach).

#### Scenario: Block stops the coach
- **WHEN** the athlete blocks a conversation and the coach then sends a message
- **THEN** no message is stored and the coach sees that the conversation is closed

#### Scenario: Report is stored
- **WHEN** the athlete reports a conversation with a reason
- **THEN** one report row with that reason exists for the conversation

### Requirement: Messages pages

`/messages` SHALL show the conversation list and a preview of the latest thread; `/messages/{id}` SHALL show the same layout with
that thread opened and marked read for the viewer. With no conversation the page SHALL show a stock photo and the role's next step.

#### Scenario: List and thread content
- **WHEN** a participant opens `/messages/{id}`
- **THEN** the list shows jersey avatar, name, club, last message snippet, relative time and unread count per conversation
- **AND** the thread shows own bubbles on the right in the role colour, the other side's on the left, day separators ("Σήμερα", "Χθες", date), a header with the other person and a "Σύνδεση από {date}" chip, and a composer (textarea + "Αποστολή")

#### Scenario: Empty
- **WHEN** a user without conversations opens `/messages`
- **THEN** the page shows a stock photo and a link to `/match` (coach) or `/me/inbox` (athlete)

#### Scenario: Opening a thread marks it read
- **WHEN** a participant opens `/messages/{id}`
- **THEN** the other side's unread messages get ReadAt, the participant's own messages are untouched
- **AND** the other side then sees "Διαβάστηκε" under their last message

#### Scenario: Send redirects to the newest message
- **WHEN** a participant sends a valid message
- **THEN** the response redirects to `/messages/{id}#bottom` and the message is the last bubble of the thread

#### Scenario: Unread count in the nav
- **WHEN** a coach or an athlete has unread messages
- **THEN** the primary nav link "Μηνύματα" shows their number

#### Scenario: Mobile
- **WHEN** the viewport is 390 px wide
- **THEN** `/messages` shows only the list, `/messages/{id}` only the thread with a back link, without horizontal scroll

### Requirement: Seeded conversations

The demo reset SHALL seed conversations deterministically (seed 42, times relative to the reset time), each tied to an accepted
interest, with the featured personas (Demo:Featured) taking part. Message bodies contain no email address, phone number or demo wording.

#### Scenario: Seed invariants
- **WHEN** the demo data is reset
- **THEN** there are about 25 conversations of 3 to 14 messages, last activity between a few minutes and three weeks ago, read and unread mixed
- **AND** the featured coach has at least 6 conversations, exactly 2 unread for her or him; the featured athlete exactly 2, none with the featured coach
- **AND** every conversation's interest is accepted, with matching coach and athlete
