using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Features.Loop;
using Bogus;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Demo;

public record MessagesSeedSet(IReadOnlyList<Interest> NewInterests, IReadOnlyList<Conversation> Conversations);

/// <summary>
/// Seeded conversations (messages package). Pure function of (demo data set after the interest lifecycle, reference time, featured
/// personas, seed): one seeded Randomizer, never the clock. Every conversation belongs to an accepted interest: the generated ones that
/// are already accepted, plus new accepted interests added here. The featured coach gets 7 (2 unread for her/him), the featured athlete
/// exactly 2 with other coaches, so the 60-second demo accept makes it 3. Fictional basketball talk in Greek and English (BodyEn): no real
/// people or clubs, no money figures, no phone numbers or e-mail addresses. Every row is IsDemo.
/// </summary>
public static class MessagesSeeder
{
    public const int Target = 25;
    private const int FeaturedCoachTarget = 7;
    private const int FeaturedAthleteTarget = 2;

    private sealed record Line(MessageSender Sender, string Text, string TextEn, bool Pause);

    private sealed record Script(string Key, Line[] Lines);

    // "C:" coach, "A:" athlete; a "+" (C+: / A+:) marks a pause of a day or more before the line. "Greek || English".
    // {name} = the athlete's first name (Greek: vocative, English: Latin name), {a:women|men} = athlete gender, {time} = a training hour,
    // {inLeague} = coach's league.
    private static Script S(string key, params string[] lines) => new(key, lines.Select(l =>
    {
        var colon = l.IndexOf(':');
        var head = l[..colon];
        var texts = l[(colon + 1)..].Split(" || ");
        return new Line(head[0] == 'C' ? MessageSender.Coach : MessageSender.Athlete, texts[0].Trim(), texts[1].Trim(), head.EndsWith('+'));
    }).ToArray());

    private static readonly Script Tuesday = S("tryout-tuesday",
        "C: Γεια σου {name}! Σε παρακολουθούμε φέτος και μας αρέσει πολύ το παιχνίδι σου. Θα ήθελες να έρθεις σε μια προπόνηση μαζί μας; || Hi {name}! We've been watching you this season and we really like your game. Would you like to come to a practice with us?",
        "A: Καλησπέρα κόουτς, ευχαριστώ πολύ για το μήνυμα! Ναι, θα το ήθελα πολύ. || Hi coach, thanks so much for the message! Yes, I'd love to.",
        "C: Τέλεια! Προπονούμαστε Τρίτη και Πέμπτη στις {time} στο κλειστό μας. Σου κάνει την ερχόμενη Τρίτη; || Great! We practice Tuesdays and Thursdays at {time} in our gym. Does this coming Tuesday work for you?",
        "A: Την Τρίτη μπορώ κανονικά. Να φέρω κάτι συγκεκριμένο; || Tuesday works for me. Should I bring anything?",
        "C: Μόνο αθλητικά και παπούτσια για παρκέ. Έλα είκοσι λεπτά νωρίτερα να σου γνωρίσω {a:τις κοπέλες|τα παιδιά}. || Just workout clothes and court shoes. Come twenty minutes early so I can introduce you to {a:the girls|the guys}.",
        "A: Τέλεια, ευχαριστώ! Τα λέμε την Τρίτη στο γήπεδο! || Perfect, thanks! See you Tuesday at the gym!");

    private static readonly Script Video = S("full-game-video",
        "C: Καλησπέρα {name}. Είδαμε τα highlights σου και θα θέλαμε να δούμε κάτι περισσότερο πριν κανονίσουμε συνάντηση. || Hi {name}. We watched your highlights and we'd like to see a bit more before we set up a meeting.",
        "A: Καλησπέρα κόουτς! Χαίρομαι πολύ που σας ενδιαφέρει το προφίλ μου. || Hi coach! I'm really glad you're interested in my profile.",
        "C: Έχεις βίντεο από ολόκληρο αγώνα; Θέλω να δω κυρίως την άμυνα και τις επιστροφές. || Do you have video of a full game? I mainly want to see your defense and how you get back in transition.",
        "A: Ο σύλλογός μου βιντεοσκοπεί όλους τους εντός έδρας αγώνες. Θα ζητήσω από τον έφορο να σας στείλει τον τελευταίο. || My club films all our home games. I'll ask our team manager to send you the latest one.",
        "A+: Ο έφορος μου είπε ότι σας τον έστειλε σήμερα το πρωί. Είναι ο αγώνας της περασμένης Κυριακής. || Our team manager says he sent it to you this morning. It's last Sunday's game.",
        "C: Τον είδα, πολύ καλή δουλειά στην άμυνα στο δεύτερο ημίχρονο. Θα σου πω μέχρι την Παρασκευή για τα επόμενα βήματα. || Watched it. Really good defensive work in the second half. I'll get back to you by Friday about next steps.",
        "A: Ευχαριστώ πολύ, περιμένω νέα σας! || Thanks so much, looking forward to hearing from you!");

    private static readonly Script Role = S("minutes-role",
        "C: Γεια σου {name}! Ψάχνουμε {a:κάποια|κάποιον} με τα χαρακτηριστικά σου για τον δεύτερο γύρο. || Hi {name}! We're looking for someone with your skill set for the second half of the season.",
        "A: Γεια σας κόουτς, ευχαριστώ για το ενδιαφέρον! || Hi coach, thanks for reaching out!",
        "A: Μια ερώτηση πριν προχωρήσουμε: τι ρόλο βλέπετε για μένα στην ομάδα; || One question before we go any further: what role do you see for me on the team?",
        "C: Ειλικρινά; Ξεκινάς από τον πάγκο, αλλά με 15 με 20 λεπτά ανά αγώνα από την αρχή. Θέλω ενέργεια στην άμυνα και γρήγορες αποφάσεις. || Honestly? You'd start off the bench, but with 15 to 20 minutes a game from day one. I want energy on defense and quick decisions.",
        "A: Αυτό ακούγεται πολύ καλό. Στην ομάδα μου τώρα παίζω κάτω από δέκα λεπτά. || That sounds really good. Right now I'm playing under ten minutes a game.",
        "C: Το ξέρω, γι' αυτό σου έγραψα. Τα λεπτά τα κερδίζεις στην προπόνηση, εμείς δίνουμε ευκαιρίες. || I know, that's why I wrote to you. You earn your minutes in practice, and we give people a real shot.",
        "A: Ευχαριστώ για την ειλικρίνεια. Θα ήθελα να έρθω σε μια προπόνηση να δω πώς δουλεύετε. || Thanks for being straight with me. I'd like to come to a practice and see how you work.");

    private static readonly Script Exams = S("studies-exams",
        "C: Καλησπέρα {name}, μας ενδιαφέρει να σε έχουμε στο ρόστερ από τον Ιανουάριο. Πώς είναι το πρόγραμμά σου; || Hi {name}, we'd like to have you on the roster from January. What does your schedule look like?",
        "A: Καλησπέρα! Ευχαριστώ πολύ, χάρηκα με το μήνυμα. || Hi! Thank you so much, I was really happy to get your message.",
        "A: Είμαι στο τρίτο έτος στη σχολή και τον Ιανουάριο έχω εξεταστική. Τις προπονήσεις τις βγάζω, αλλά ίσως χάσω μία ή δύο. || I'm in my third year at university and I have exams in January. I can make practices, but I might miss one or two.",
        "C: Κανένα πρόβλημα, οι σπουδές προηγούνται. Έχουμε {a:κι άλλες φοιτήτριες|κι άλλους φοιτητές} στην ομάδα και το προγραμματίζουμε μαζί. || No problem at all, school comes first. We have other student-athletes on the team and we plan around it together.",
        "A: Αυτό με καθησυχάζει πολύ. Μετά τις 15 Φεβρουαρίου είμαι εντελώς {a:ελεύθερη|ελεύθερος}. || That's a big relief. After February 15 I'm completely free.",
        "C: Τέλεια. Έλα να γνωριστούμε πριν την εξεταστική και τα υπόλοιπα τα βρίσκουμε. || Perfect. Come meet us before exams and we'll figure out the rest.");

    private static readonly Script Injury = S("injury-check",
        "C: Γεια σου {name}! Θέλουμε να σε δούμε σε προπόνηση. Πριν από αυτό, είσαι εντάξει σωματικά; || Hi {name}! We'd like to see you at practice. Before that, are you fully healthy?",
        "A: Γεια σας! Ευχαριστώ πολύ για το ενδιαφέρον. || Hi! Thanks so much for your interest.",
        "A: Πέρυσι είχα διάστρεμμα στον αστράγαλο, αλλά έκανα φυσικοθεραπεία και παίζω κανονικά από τον Σεπτέμβριο. || I sprained my ankle last year, but I did physio and I've been playing normally since September.",
        "C: Χαίρομαι που το ακούω. Ο φυσικοθεραπευτής μας θα σου κάνει μια σύντομη αξιολόγηση την πρώτη μέρα, το κάνουμε σε όλους. || Glad to hear it. Our physio does a quick assessment on everyone's first day, so we'll do one with you too.",
        "A: Φυσικά, κανένα πρόβλημα. Μου φαίνεται πολύ σωστό. || Of course, no problem. Makes total sense.",
        "C: Ωραία. Σου γράφω μέρα και ώρα μέσα στην εβδομάδα. || Great. I'll send you a day and time later this week.");

    private static readonly Script Family = S("family-relocation",
        "C: Καλησπέρα {name}. Θα θέλαμε να σε έχουμε στην ομάδα από την επόμενη σεζόν. Ξέρω ότι σημαίνει μετακόμιση, γι' αυτό σου γράφω νωρίς. || Hi {name}. We'd like to have you on the team next season. I know it means moving, so I'm reaching out early.",
        "A: Καλησπέρα κόουτς, σας ευχαριστώ πολύ! Δεν το περίμενα. || Hi coach, thank you so much! I didn't expect this.",
        "C: Ο σύλλογος βοηθά στην εύρεση σπιτιού κοντά στο γήπεδο και έχουμε {a:δύο κοπέλες|δύο παίκτες} που ήρθαν από άλλη πόλη πέρυσι. Μπορείς να μιλήσεις μαζί τους. || The club helps you find a place near the gym, and we have {a:two girls|two guys} who moved here from another city last year. You can talk to them.",
        "A: Αυτό βοηθάει πολύ. Θα το συζητήσω με την οικογένειά μου και θα σας απαντήσω μέσα στην εβδομάδα. || That really helps. I'll talk it over with my family and get back to you this week.",
        "C: Φυσικά, πάρε τον χρόνο σου. Αν έχουν ερωτήσεις, ευχαρίστως να τους εξηγήσω κι εγώ πώς δουλεύουμε. || Of course, take your time. If they have questions, I'm happy to explain how we work.",
        "A: Ευχαριστώ, θα τους το πω! || Thanks, I'll tell them!");

    private static readonly Script Declined = S("declined-politely",
        "C: Γεια σου {name}! Ψάχνουμε {a:μια παίκτρια|έναν παίκτη} με την ενέργειά σου για τον δεύτερο γύρο. Θα ήθελες να το συζητήσουμε; || Hi {name}! We're looking for a player with your energy for the second half of the season. Would you like to talk about it?",
        "A: Καλησπέρα κόουτς, ευχαριστώ πολύ που μου γράψατε. || Hi coach, thanks so much for writing to me.",
        "C: Σκεφτόμαστε να σε δοκιμάσουμε σε ρόλο πρώτης αλλαγής. Πες μου αν σε ενδιαφέρει να έρθεις να μας γνωρίσεις. || We're thinking of trying you as our first player off the bench. Let me know if you'd like to come and meet us.",
        "A+: Το σκέφτηκα πολύ και μίλησα και με το τεχνικό επιτελείο. Φέτος θα μείνω στην ομάδα μου, έχουμε στόχο την άνοδο και θέλω να τον ολοκληρώσω. || I thought about it a lot and talked it over with our coaching staff. I'm staying with my team this year. We're going for promotion and I want to see it through.",
        "A: Σας ευχαριστώ πραγματικά για την πρόταση, σημαίνει πολλά για μένα. || I really appreciate the offer, it means a lot to me.",
        "C: Το σέβομαι απόλυτα, καλή επιτυχία στον στόχο σας! Η πόρτα μένει ανοιχτή για το καλοκαίρι. || I completely respect that. Good luck with the promotion push! The door stays open for the summer.",
        "A: Ευχαριστώ πολύ, καλή συνέχεια και σε εσάς! || Thanks so much, all the best to you too!");

    private static readonly Script Saturday = S("far-away-saturday",
        "C: Καλησπέρα {name}, θα ήθελα να σε καλέσω σε μια προπόνηση γνωριμίας την επόμενη εβδομάδα. || Hi {name}, I'd like to invite you to an intro practice next week.",
        "A: Καλησπέρα! Ευχαριστώ πολύ. Μένω αρκετά μακριά, υπάρχει περίπτωση να έρθω Σάββατο; || Hi! Thanks so much. I live pretty far away, any chance I could come on a Saturday?",
        "C: Βεβαίως. Το Σάββατο έχουμε πρωινή προπόνηση στις 10:30, έλα τότε. || Of course. We have a morning practice on Saturday at 10:30 am, come then.");

    private static readonly Script Amateur = S("amateur-schedule",
        "C: Γεια σου {name}! Χρειαζόμαστε βάθος στο ρόστερ για τον δεύτερο γύρο. Σε είδαμε στο τουρνουά του Οκτωβρίου και μας έκανες εντύπωση. || Hi {name}! We need more depth on the roster for the second half of the season. We saw you at the October tournament and you made an impression.",
        "A: Γεια σας κόουτς! Ευχαριστώ, εκείνο το τουρνουά ήταν πολύ καλό για μένα. || Hi coach! Thanks, that tournament went really well for me.",
        "A: Πώς λειτουργεί η ομάδα; Παίζω ερασιτεχνικά και δουλεύω πρωί. || How does the team work? I play as an amateur and I work mornings.",
        "C: Είμαστε καθαρά ερασιτεχνική ομάδα. Προπονήσεις τρεις φορές την εβδομάδα στις {time}, αγώνες το Σαββατοκύριακο. Οι περισσότεροι δουλεύουν ή σπουδάζουν. || We're a fully amateur team. Practice three times a week at {time}, games on weekends. Most of us work or study.",
        "A: Τέλεια, αυτό ταιριάζει ακριβώς στο πρόγραμμά μου. || Perfect, that fits my schedule exactly.",
        "C: Τότε έλα την Τετάρτη να σε δούμε. Αν σου αρέσει το κλίμα, τα υπόλοιπα είναι απλά. || Then come by on Wednesday so we can see you play. If you like the vibe, the rest is easy.",
        "A: Θα είμαι εκεί! || I'll be there!");

    private static readonly Script Thursday = S("thursday-visit",
        "C: Καλησπέρα {name}. Θέλω να σε γνωρίσω από κοντά. Πότε είσαι {a:διαθέσιμη|διαθέσιμος} αυτή την εβδομάδα; || Hi {name}. I'd like to meet you in person. When are you free this week?",
        "A: Καλησπέρα κόουτς! Ευχαριστώ για την πρόσκληση. || Hi coach! Thanks for the invite.",
        "A: Είμαι {a:διαθέσιμη|διαθέσιμος} Πέμπτη και Παρασκευή απόγευμα. || I'm free Thursday and Friday afternoon.",
        "C: Πέμπτη στις {time} λοιπόν, στο κλειστό μας. Θα σε περιμένω στην είσοδο. || Thursday at {time} then, at our gym. I'll meet you at the entrance.",
        "A: Τέλεια, θα είμαι εκεί. || Perfect, I'll be there.",
        "C: Ωραία! Φέρε και την κάρτα υγείας αθλητή, τη ζητάει η γραμματεία. || Great! Bring your athlete health card too, the front office needs it.");

    private static readonly Script GameVisit = S("watching-the-game",
        "C: Γεια σου {name}! Θα έρθω να δω τον αγώνα σας την Κυριακή. Θα ήθελα να μιλήσουμε λίγο μετά, αν έχεις χρόνο. || Hi {name}! I'm coming to watch your game on Sunday. I'd like to talk for a bit afterwards, if you have time.",
        "A: Γεια σας! Ευχαριστώ πολύ, θα χαρώ να γνωριστούμε. || Hi! Thanks so much, I'd be glad to meet you.",
        "A: Ο αγώνας είναι στις 17:00 στην έδρα μας. || The game is at 5 pm at our home court.",
        "C: Το είδα στο πρόγραμμα. Θα είμαι στην κερκίδα, μην αγχωθείς! || Saw it on the schedule. I'll be in the stands, don't be nervous!",
        "A: Χαχα, θα προσπαθήσω! Τα λέμε εκεί. || Haha, I'll try! See you there.",
        "C+: Πολύ ωραίο παιχνίδι σήμερα, ειδικά στο τελευταίο δεκάλεπτο. Ευχαριστώ για την κουβέντα! || Really good game today, especially the last ten minutes. Thanks for the chat!",
        "A: Εγώ ευχαριστώ που ήρθατε. Περιμένω να μου πείτε για την προπόνηση. || Thank you for coming. Looking forward to hearing about practice.");

    private static readonly Script SecondRound = S("second-round-plan",
        "C: Καλησπέρα {name}. Ετοιμαζόμαστε για τον δεύτερο γύρο και θέλουμε ενίσχυση. Θα ήθελα να σε δω σε δράση. || Hi {name}. We're getting ready for the second half of the season and want to strengthen the roster. I'd like to see you in action.",
        "A: Καλησπέρα κόουτς, χαίρομαι πολύ που μου γράψατε! || Hi coach, I'm really glad you wrote to me!",
        "C: Ξεκινάμε με το νέο ρόστερ αρχές Ιανουαρίου. Μέχρι τότε θα σου στείλω ένα πρόγραμμα φυσικής κατάστασης να το ακολουθήσεις. || We start with the new roster in early January. Until then I'll send you a conditioning program to follow.",
        "A: Πολύ ωραία. Μπορώ να ρωτήσω πόσ{a:ες παίκτριες|οι παίκτες} θα είναι στο ρόστερ; || Sounds great. Can I ask how many players will be on the roster?",
        "C: Δώδεκα στο ρόστερ και άλλ{a:ες|οι} τρεις από τις ακαδημίες που προπονούνται μαζί μας. || Twelve on the roster, plus three more from the academy who practice with us.",
        "A: Κατάλαβα. Θα κάνω ό,τι μπορώ για να είμαι {a:έτοιμη|έτοιμος}. || Got it. I'll do everything I can to be ready.");

    private static readonly Script Skills = S("one-on-one-skills",
        "C: Γεια σου {name}! Μου άρεσε πολύ το σουτ σου από μέση απόσταση. Θα ήθελες να κάνουμε μια ατομική προπόνηση; || Hi {name}! I really liked your mid-range shot. Would you like to do a one-on-one session?",
        "A: Γεια σας! Ναι, πολύ ευχαρίστως. || Hi! Yes, I'd love to.",
        "C: Με ποιο χέρι νιώθεις πιο σίγουρα στη ντρίμπλα; || Which hand are you more confident dribbling with?",
        "A: Με το δεξί σίγουρα. Το αριστερό το δουλεύω από το καλοκαίρι. || My right, for sure. I've been working on my left since the summer.",
        "C: Φαίνεται στο βίντεο, έχει βελτιωθεί. Στην ομάδα παίζουμε πολύ πικ εν ρολ, οπότε θα το χρειαστείς. || It shows in the video, it's gotten better. We run a lot of pick and roll, so you'll need it.",
        "A: Το ξέρω, είναι ο στόχος μου φέτος. || I know, that's my goal this year.",
        "C: Ωραία. Τι ώρες σου ταιριάζουν; || Great. What times work for you?",
        "A: Μετά τις έξι κάθε μέρα εκτός Δευτέρας. || Any day after six, except Mondays.",
        "C: Τετάρτη στις {time}; Το γήπεδο είναι ελεύθερο μία ώρα πριν την ομαδική. || Wednesday at {time}? The court is free an hour before team practice.",
        "A: Τέλεια, Τετάρτη! || Perfect, Wednesday it is!",
        "C: Σημειώθηκε. Φέρε και νερό, θα ιδρώσουμε! || Done. Bring water, we're going to sweat!",
        "A: Χαχα, εννοείται. Ευχαριστώ πολύ κόουτς! || Haha, of course. Thanks so much, coach!");

    private static readonly Script FollowUp = S("coach-follow-up",
        "C: Γεια σου {name}, ευχαριστώ που αποδέχτηκες το ενδιαφέρον μας! Πότε θα μπορούσες να έρθεις να γνωριστούμε; || Hi {name}, thanks for accepting our interest! When could you come by so we can meet?",
        "A: Γεια σας! Αυτή την εβδομάδα έχω πολύ φόρτο, μπορούμε την επόμενη; || Hi! This week is really busy for me, could we do next week?",
        "C+: Φυσικά. Η Δευτέρα ή η Τετάρτη στις {time} σου κάνει; || Of course. Does Monday or Wednesday at {time} work for you?",
        "C: Αν θες, φέρε και τα παπούτσια σου να ρίξεις λίγα σουτ μαζί μας. || If you want, bring your shoes and get some shots up with us.");

    private static readonly Script FastBreak = S("fast-break-team",
        "C: Καλησπέρα {name}! Χτίζουμε ομάδα με έμφαση στο γρήγορο παιχνίδι και πιστεύω ότι ταιριάζεις. || Hi {name}! We're building a team around fast-break basketball and I think you'd be a great fit.",
        "A: Καλησπέρα! Σας ευχαριστώ, μου αρέσει πολύ το γρήγορο παιχνίδι. || Hi! Thank you, I love playing fast.",
        "A: Πώς είναι μια τυπική εβδομάδα προπονήσεων; || What does a typical practice week look like?",
        "C: Τρεις ομαδικές, μία ατομική για όποιον θέλει και ένα γυμναστήριο. Τη Δευτέρα βλέπουμε μαζί βίντεο από τον προηγούμενο αγώνα. || Three team practices, one individual session for anyone who wants it, and one gym session. On Mondays we watch film of the last game together.",
        "A: Το βίντεο μου αρέσει πολύ, στην ομάδα μου δεν το κάνουμε. || I'd love the film sessions, we don't do that on my team.",
        "C: Βοηθάει πολύ, θα δεις. Θες να έρθεις την Πέμπτη να δεις μια προπόνηση από κοντά; || It helps a lot, you'll see. Want to come by Thursday and watch a practice up close?",
        "A: Ναι, θα έρθω! Τι ώρα; || Yes, I'll come! What time?",
        "C: Στις {time}. Θα σε περιμένω. || At {time}. See you then.");

    private static readonly Script Masters = S("masters-move",
        "C: Γεια σου {name}! Είδαμε ότι ψάχνεις ομάδα. Θα σε ενδιέφερε να το συζητήσουμε; || Hi {name}! We saw you're looking for a team. Would you be interested in talking?",
        "A: Γεια σας! Ναι, πολύ. Από τον Φεβρουάριο μετακομίζω για μεταπτυχιακό, οπότε ψάχνω ομάδα στη νέα πόλη. || Hi! Yes, very much. I'm moving in February for my master's, so I'm looking for a team in the new city.",
        "C: Τι ωραία σύμπτωση! Το γήπεδό μας είναι δέκα λεπτά από την πανεπιστημιούπολη. || What a coincidence! Our gym is ten minutes from campus.",
        "A: Αλήθεια; Αυτό θα ήταν ιδανικό. || Really? That would be perfect.",
        "C: Έλα όταν τακτοποιηθείς να κάνεις μια προπόνηση. Μέχρι τότε τα λέμε εδώ. || Come do a practice once you've settled in. Until then, let's keep talking here.",
        "A: Συμφωνώ, ευχαριστώ πολύ! || Sounds good, thanks so much!");

    private static readonly Script Question = S("tryout-question",
        "C: Καλησπέρα {name}. Θα χαρούμε να σε δούμε σε μια δοκιμαστική προπόνηση. || Hi {name}. We'd be happy to see you at a tryout practice.",
        "A: Καλησπέρα κόουτς! Ευχαριστώ, θα έρθω σίγουρα. || Hi coach! Thanks, I'll definitely be there.",
        "A: Μια ερώτηση: η δοκιμαστική είναι μαζί με την ομάδα ή ατομική; || One question: is the tryout with the whole team or one-on-one?");

    private static readonly Script Welcome = S("tryout-to-welcome",
        "C: Γεια σου {name}! Θα ήθελα να σε καλέσω σε δοκιμαστική προπόνηση με την ομάδα μας. || Hi {name}! I'd like to invite you to a tryout practice with our team.",
        "A: Γεια σας κόουτς! Ευχαριστώ πολύ για την πρόσκληση. || Hi coach! Thanks so much for the invite.",
        "A: Πότε θα ήταν; || When would it be?",
        "C: Δευτέρα στις {time}. Θα κάνουμε κυρίως ασκήσεις και ένα δίτερμα στο τέλος. || Monday at {time}. Mostly drills, with a scrimmage at the end.",
        "A: Εντάξει, θα είμαι εκεί! || Sounds good, I'll be there!",
        "C+: Ευχαριστώ που ήρθες σήμερα! Πώς σου φάνηκε; || Thanks for coming today! How did it feel?",
        "A: Μου άρεσε πολύ. Η ομάδα είναι πολύ δεμένη. || I really liked it. The team is really close.",
        "C+: Η δοκιμαστική πήγε πολύ καλά. Θα θέλαμε να σε έχουμε στην ομάδα από τον επόμενο μήνα. || The tryout went really well. We'd like to have you on the team starting next month.",
        "A: Αλήθεια; Σας ευχαριστώ πάρα πολύ! || Really? Thank you so much!",
        "A: Πρέπει να κάνω κάτι για τη μεταγραφή; Είναι η πρώτη φορά που αλλάζω ομάδα. || Do I need to do anything for the transfer? It's my first time changing teams.",
        "C: Η γραμματεία του συλλόγου θα σε καθοδηγήσει για το δελτίο, είναι απλή διαδικασία. || The club office will walk you through the registration, it's a simple process.",
        "A: Τέλεια. Ανυπομονώ να ξεκινήσω! || Perfect. Can't wait to get started!",
        "C: Κι εμείς! Καλώς ήρθες στην ομάδα. || Neither can we! Welcome to the team.");

    private static readonly Script Schedule = S("training-days",
        "C: Καλησπέρα {name}! Θα ήθελες να προπονηθείς μαζί μας για λίγες εβδομάδες, να γνωριστούμε; || Hi {name}! Would you like to practice with us for a few weeks so we can get to know each other?",
        "A: Ναι, πολύ ευχαρίστως! Ποιες μέρες προπονείστε; || Yes, I'd love to! Which days do you practice?",
        "C: Δευτέρα, Τετάρτη και Παρασκευή στις {time}. Ξεκίνα όποτε θες. || Monday, Wednesday and Friday at {time}. Start whenever you like.",
        "A: Θα ξεκινήσω από την Τετάρτη. Ευχαριστώ κόουτς! || I'll start on Wednesday. Thanks, coach!");

    private static readonly Script WhichLeague = S("which-league",
        "C: Γεια σου {name}! Η ομάδα μας ανεβαίνει δυναμικά και θέλουμε {a:παίκτριες|παίκτες} με τη δική σου νοοτροπία. || Hi {name}! Our team is on the rise and we want players with your mindset.",
        "A: Γεια σας, ευχαριστώ πολύ! Σε ποια κατηγορία αγωνίζεστε; || Hi, thanks so much! Which league do you play in?",
        "C: Φέτος είμαστε {inLeague}. Ο στόχος είναι να μείνουμε ψηλά και να δώσουμε χρόνο σε νέα παιδιά. || This year we're {inLeague}. The goal is to finish near the top and give young players minutes.",
        "A: Ωραία. Ποιος θα ήταν ο ρόλος μου; || Nice. What would my role be?",
        "C: Θα σε δοκιμάσουμε και στις δύο θέσεις που παίζεις. Θέλω να σε δω πρώτα στην προπόνηση. || We'll try you at both positions you play. I want to see you in practice first.",
        "A: Δίκαιο. Πείτε μου πότε να έρθω. || Fair enough. Let me know when to come.");

    // Featured coach: the first two end with the athlete and stay unread for the coach (minutes and hours ago).
    private static readonly (Script Script, int EndMinutesAgo, bool Unread)[] FeaturedCoachSlots =
    [
        (Tuesday, 4, true), (Role, 130, true), (Skills, 1_350, false), (Family, 2_950, false), (Welcome, 5_900, false),
        (Declined, 11_200, false), (Exams, 15_600, false),
    ];

    // Featured athlete: one unread coach message waiting for her, one she answered and the coach read.
    private static readonly (Script Script, int EndMinutesAgo, bool Unread)[] FeaturedAthleteSlots =
    [
        (Thursday, 48, true), (Masters, 4_300, false),
    ];

    private static readonly Script[] OtherScripts =
        [Video, Injury, Saturday, Amateur, GameVisit, SecondRound, FollowUp, FastBreak, Question, Schedule, WhichLeague, Thursday, Masters, Role, Family, Exams];

    private static readonly int[] OtherEndMinutesAgo =
        [22, 75, 190, 320, 610, 1_280, 1_520, 2_100, 2_880, 3_600, 4_450, 5_750, 7_300, 8_800, 11_000, 13_900, 15_800];

    private static readonly string[] Notes =
    [
        "Θα θέλαμε να σας δούμε σε μια προπόνηση.",
        "Το προφίλ σας μας ενδιαφέρει. Θα χαρούμε να γνωριστούμε.",
        "Παρακολουθούμε τις εμφανίσεις σας φέτος και θα θέλαμε να μιλήσουμε.",
        "Ψάχνουμε ενίσχυση για τον δεύτερο γύρο και ταιριάζετε σε αυτό που χρειαζόμαστε.",
    ];

    private static readonly string[] NotesEn =
    [
        "We'd love to see you at one of our practices.",
        "Your profile caught our eye. We'd be glad to meet you.",
        "We've been following your games this season and would like to talk.",
        "We're adding players for the second half of the season, and you're what we need.",
    ];

    private static readonly string[] TrainingTimes = ["18:30", "19:00", "19:30", "20:00", "20:30"];
    private static readonly string[] TrainingTimesEn = ["6:30 pm", "7 pm", "7:30 pm", "8 pm", "8:30 pm"];

    // Vocative of the seed's men's first names (-ης -> -η and -ας -> -α are regular; -ος is not, so it is listed).
    private static readonly Dictionary<string, string> MaleVocative = new()
    {
        ["Γιώργος"] = "Γιώργο", ["Νίκος"] = "Νίκο", ["Χρήστος"] = "Χρήστο", ["Αθανάσιος"] = "Αθανάσιε", ["Στέλιος"] = "Στέλιο",
        ["Σπύρος"] = "Σπύρο", ["Πέτρος"] = "Πέτρο", ["Αλέξανδρος"] = "Αλέξανδρε", ["Ευάγγελος"] = "Ευάγγελε", ["Λάμπρος"] = "Λάμπρο",
        ["Κωνσταντίνος"] = "Κωνσταντίνε", ["Σταύρος"] = "Σταύρο", ["Μάριος"] = "Μάριε", ["Τάσος"] = "Τάσο", ["Παύλος"] = "Παύλο",
        ["Χαράλαμπος"] = "Χαράλαμπε", ["Κίμων"] = "Κίμωνα", ["Νικόλαος"] = "Νικόλαε", ["Θεόδωρος"] = "Θεόδωρε",
    };

    // Plausible Greek waking hours for seeded times only (UTC+2, winter time); the pages use Messages:TimeZone.
    private static readonly TimeSpan GreekOffset = TimeSpan.FromHours(2);

    public static MessagesSeedSet Build(DemoDataSet data, DateTimeOffset now, FeaturedOptions? featured, int seed)
    {
        var r = new Randomizer(seed + 211);
        var fCoach = featured is { CoachFirstName.Length: > 0 }
            ? data.Coaches.FirstOrDefault(c => c.FirstName == featured.CoachFirstName && c.LastName == featured.CoachLastName)
            : null;
        // Without configuration a stand-in keeps the shape (one busy coach) so the data does not depend on appsettings.
        fCoach ??= data.Coaches.First(c => c.Role == CoachRole.Head && c.Club!.Gender == Gender.Female);
        var fAthlete = featured is { AthleteSlug.Length: > 0 } ? data.Athletes.FirstOrDefault(a => a.Slug == featured.AthleteSlug) : null;

        var taken = data.Interests.Select(i => (i.Coach!, i.Athlete!)).ToHashSet();
        var busy = new HashSet<Athlete>();   // at most one seeded conversation per athlete (the featured athlete excepted)
        var newInterests = new List<Interest>();
        var conversations = new List<Conversation>();

        var accepted = data.Interests.Where(i => i.Reply == InterestReply.Accepted).ToList();
        foreach (var i in accepted) busy.Add(i.Athlete!);

        Interest NewInterest(Coach coach, Athlete athlete)
        {
            var interest = new Interest { Coach = coach, Athlete = athlete, Reply = InterestReply.Accepted, IsDemo = true };
            taken.Add((coach, athlete));
            if (athlete != fAthlete) busy.Add(athlete);
            newInterests.Add(interest);
            return interest;
        }

        bool Eligible(Coach coach, Athlete a) =>
            a.Gender == coach.Club!.Gender && a.CurrentClub != coach.Club && !taken.Contains((coach, a)) && !busy.Contains(a) && a.Stats.Count > 0;

        // 1. Featured athlete: exactly two conversations, none with the featured coach (her 60-second accept adds the third).
        var plan = new List<(Interest Interest, Script Script, int EndMinutesAgo, bool Unread)>();
        var athleteSlots = new Queue<(Script, int, bool)>(FeaturedAthleteSlots);
        if (fAthlete is not null)
        {
            foreach (var i in accepted.Where(i => i.Athlete == fAthlete && athleteSlots.Count > 0))
            {
                var (s, end, unread) = athleteSlots.Dequeue();
                plan.Add((i, s, end, unread));
            }
            var coaches = r.Shuffle(data.Coaches.Where(c => c != fCoach && c.Club!.Gender == fAthlete.Gender && c.Club != fAthlete.CurrentClub
                && !taken.Contains((c, fAthlete)))).ToList();
            foreach (var coach in coaches.Take(athleteSlots.Count))
            {
                var (s, end, unread) = athleteSlots.Dequeue();
                plan.Add((NewInterest(coach, fAthlete), s, end, unread));
            }
            busy.Add(fAthlete);
        }

        // 2. Featured coach: seven conversations, the two most recent unread for her/him.
        var coachSlots = new Queue<(Script, int, bool)>(FeaturedCoachSlots);
        foreach (var i in accepted.Where(i => i.Coach == fCoach && i.Athlete != fAthlete && coachSlots.Count > 0))
        {
            var (s, end, unread) = coachSlots.Dequeue();
            plan.Add((i, s, end, unread));
        }
        foreach (var athlete in r.Shuffle(data.Athletes.Where(a => a != fAthlete && Eligible(fCoach, a))).Take(coachSlots.Count).ToList())
        {
            var (s, end, unread) = coachSlots.Dequeue();
            plan.Add((NewInterest(fCoach, athlete), s, end, unread));
        }

        // 3. Everyone else: the remaining generated accepted interests, then new pairs (women's and men's clubs interleaved) up to Target.
        var scripts = new Queue<Script>(r.Shuffle(OtherScripts));
        var ends = new Queue<int>(OtherEndMinutesAgo);
        Script NextScript() { if (scripts.Count == 0) foreach (var s in r.Shuffle(OtherScripts)) scripts.Enqueue(s); return scripts.Dequeue(); }
        int NextEnd() => ends.Count > 0 ? ends.Dequeue() : r.Int(60, 15_000);
        foreach (var i in accepted.Where(i => !plan.Any(p => p.Interest == i)))
        {
            var s = NextScript();
            plan.Add((i, s, NextEnd(), r.Double() < 0.4));
        }
        var women = r.Shuffle(data.Coaches.Where(c => c != fCoach && c.Club!.Gender == Gender.Female)).ToList();
        var men = r.Shuffle(data.Coaches.Where(c => c.Club!.Gender == Gender.Male)).ToList();
        var order = new List<Coach>();
        for (int w = 0, m = 0; w < women.Count || m < men.Count;)
        {
            for (var j = 0; j < 2 && w < women.Count; j++) order.Add(women[w++]);
            if (m < men.Count) order.Add(men[m++]);
        }
        foreach (var coach in order.Concat(order))
        {
            if (plan.Count >= Target) break;
            var athlete = r.Shuffle(data.Athletes.Where(a => Eligible(coach, a))).FirstOrDefault();
            if (athlete is null) continue;
            var s = NextScript();
            plan.Add((NewInterest(coach, athlete), s, NextEnd(), r.Double() < 0.4));
        }

        foreach (var (interest, script, endMinutesAgo, unread) in plan)
            conversations.Add(Conversation(r, interest, script, now, now.AddMinutes(-endMinutesAgo), unread, newInterests.Contains(interest)));

        // Coaches have read every seeded reply: the "Αποστολές (n)" count stays about live replies.
        foreach (var i in newInterests)
        {
            var read = i.RepliedAt!.Value.AddMinutes(r.Int(5, 240));
            i.ReplyReadAt = read < now ? read : now;
        }
        return new MessagesSeedSet(newInterests, conversations);
    }

    private static Conversation Conversation(Randomizer r, Interest interest, Script script, DateTimeOffset now, DateTimeOffset end, bool unread, bool isNew)
    {
        var lines = script.Lines;
        var times = new DateTimeOffset[lines.Length];
        times[^1] = end;
        for (var k = lines.Length - 1; k > 0; k--)
        {
            var gap = lines[k].Pause ? TimeSpan.FromHours(r.Int(20, 52))
                : r.Double() < 0.2 ? TimeSpan.FromHours(r.Int(2, 9))
                : TimeSpan.FromMinutes(r.Int(2, 38));
            times[k - 1] = AwakeOrEarlier(r, times[k] - gap);
        }

        if (isNew)
        {
            interest.RepliedAt = times[0].AddMinutes(-r.Int(3, 90));
            interest.SeenAt = interest.RepliedAt.Value.AddMinutes(-r.Int(1, 600));
            interest.CreatedAt = interest.SeenAt.Value.AddHours(-r.Int(3, 60));
            if (r.Double() < 0.45)
            {
                var k = r.Int(0, Notes.Length - 1);
                (interest.Note, interest.NoteEn) = (Notes[k], NotesEn[k]);
            }
            else (interest.Note, interest.NoteEn) = (null, null);
        }
        else
        {
            // A generated interest was accepted at a fixed time: the chat fits between that and `now`.
            var from = interest.RepliedAt!.Value.AddMinutes(1);
            if (times[0] <= from)
            {
                var span = (end > from ? end : now) - from;
                for (var k = 0; k < times.Length; k++) times[k] = from + span * (k + 1) / (times.Length + 1);
            }
        }

        var athlete = interest.Athlete!;
        var conversation = new Conversation
        {
            Interest = interest, Coach = interest.Coach, Athlete = athlete, CreatedAt = interest.RepliedAt!.Value, IsDemo = true,
        };
        if (!string.IsNullOrEmpty(interest.Note))
            conversation.Messages.Add(new Message
            {
                SenderKind = MessageSender.Coach, Body = interest.Note, BodyEn = interest.NoteEn, SentAt = interest.CreatedAt, ReadAt = interest.SeenAt, IsDemo = true,
            });

        var timeIndex = r.Int(0, TrainingTimes.Length - 1);
        var last = lines[^1].Sender;
        var trailing = lines.Reverse().TakeWhile(l => l.Sender == last).Count();
        for (var k = 0; k < lines.Length; k++)
        {
            DateTimeOffset? readAt;
            var reply = Array.FindIndex(lines, k + 1, l => l.Sender != lines[k].Sender);
            if (unread && k >= lines.Length - trailing) readAt = null;
            else
            {
                var read = times[k].AddMinutes(r.Int(1, 25));
                var limit = reply >= 0 ? times[reply] : now;
                readAt = read < limit ? read : limit;
            }
            conversation.Messages.Add(new Message
            {
                SenderKind = lines[k].Sender, Body = Fill(lines[k].Text, athlete, interest.Coach!, TrainingTimes[timeIndex]),
                BodyEn = FillEn(lines[k].TextEn, athlete, interest.Coach!, TrainingTimesEn[timeIndex]), SentAt = times[k], ReadAt = readAt, IsDemo = true,
            });
        }
        return conversation;
    }

    /// <summary>Nobody writes between 00:00 and 08:00: such a time moves to the previous evening (earlier, so the order holds).</summary>
    private static DateTimeOffset AwakeOrEarlier(Randomizer r, DateTimeOffset t)
    {
        var local = t.ToOffset(GreekOffset);
        if (local.Hour >= 8) return t;
        return new DateTimeOffset(local.Date.AddDays(-1).AddHours(21).AddMinutes(r.Int(0, 150)), GreekOffset).ToUniversalTime();
    }

    private static string Fill(string text, Athlete athlete, Coach coach, string time)
    {
        var female = athlete.Gender == Gender.Female;
        text = Regex.Replace(text, @"\{a:([^|}]*)\|([^}]*)\}", m => female ? m.Groups[1].Value : m.Groups[2].Value);
        return text.Replace("{name}", Vocative(athlete)).Replace("{time}", time).Replace("{inLeague}", InLeague(coach.Club!.League));
    }

    private static string FillEn(string text, Athlete athlete, Coach coach, string time)
    {
        var female = athlete.Gender == Gender.Female;
        text = Regex.Replace(text, @"\{a:([^|}]*)\|([^}]*)\}", m => female ? m.Groups[1].Value : m.Groups[2].Value);
        return text.Replace("{name}", athlete.FirstNameLatin).Replace("{time}", time).Replace("{inLeague}", InLeagueEn(coach.Club!.League));
    }

    private static string Vocative(Athlete a)
    {
        var name = a.FirstName;
        if (a.Gender == Gender.Female) return name;
        if (MaleVocative.TryGetValue(name, out var v)) return v;
        return name.EndsWith('ς') ? name[..^1] : name;   // -ης -> -η, -ας -> -α
    }

    private static string InLeague(League league) => league switch
    {
        League.A1 => "στην Α1",
        League.A2 => "στην Α2",
        League.B => "στη Β΄ Εθνική",
        _ => "στο τοπικό πρωτάθλημα",
    };

    private static string InLeagueEn(League league) => league switch
    {
        League.A1 => "in A1",
        League.A2 => "in A2",
        League.B => "in the B National league",
        _ => "in the regional league",
    };

    /// <summary>Replaces the seeded conversations (the interest delete of the reset already cascaded them) and adds the extra interests.</summary>
    public static async Task ReplaceAsync(BallpassDbContext db, DemoDataSet data, DateTimeOffset now, FeaturedOptions? featured, CancellationToken ct = default)
    {
        await db.Set<Conversation>().Where(c => c.IsDemo).ExecuteDeleteAsync(ct);
        var set = Build(data, now, featured, DemoDataSeeder.Seed);
        db.Interests.AddRange(set.NewInterests);
        db.Set<Conversation>().AddRange(set.Conversations);
        await db.SaveChangesAsync(ct);
    }
}
