## ADDED Requirements

### Requirement: Home strip honours the viewer's filters

The home feed strip SHALL apply the same hide, block and report filters as `/feed` for the signed-in viewer.

#### Scenario: Blocked author
- **WHEN** a coach blocks the author of a post in the strip
- **THEN** the home strip no longer shows that author's posts

#### Scenario: Hidden post
- **WHEN** a coach hides a post shown in the strip
- **THEN** the home strip no longer shows that post

### Requirement: Relative time has one implementation

PostCard and the Sent page SHALL format relative time with one helper, wording "πριν από {n} λ./ώ./ημ.".

#### Scenario: Wording
- **WHEN** a time 5 minutes, 3 hours or 2 days ago is formatted
- **THEN** it reads "πριν από 5 λ.", "πριν από 3 ώ.", "πριν από 2 ημ."

### Requirement: Self-follow is rejected

The follow service and `/feed/follow` SHALL refuse a follow whose follower and target are the same actor.

#### Scenario: Self-follow
- **WHEN** an actor follows their own key, through the service or `/feed/follow`
- **THEN** the follow is refused and no row is created
