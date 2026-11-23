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
/// exactly 2 with other coaches, so the 60-second demo accept makes it 3. Fictional Greek basketball talk: no real people or clubs,
/// no money figures, no phone numbers or e-mail addresses. Every row is IsDemo.
/// </summary>
public static class MessagesSeeder
{
    public const int Target = 25;
    private const int FeaturedCoachTarget = 7;
    private const int FeaturedAthleteTarget = 2;

    private sealed record Line(MessageSender Sender, string Text, bool Pause);

    private sealed record Script(string Key, Line[] Lines);

    // "C:" coach, "A:" athlete; a "+" (C+: / A+:) marks a pause of a day or more before the line.
    // {name} = the athlete's first name (vocative), {a:women|men} = athlete gender, {time} = a training hour, {inLeague} = coach's league.
    private static Script S(string key, params string[] lines) => new(key, lines.Select(l =>
    {
        var colon = l.IndexOf(':');
        var head = l[..colon];
        return new Line(head[0] == 'C' ? MessageSender.Coach : MessageSender.Athlete, l[(colon + 1)..].Trim(), head.EndsWith('+'));
    }).ToArray());

    private static readonly Script Tuesday = S("tryout-tuesday",
        "C: Γεια σου {name}! Σε παρακολουθούμε φέτος και μας αρέσει πολύ το παιχνίδι σου. Θα ήθελες να έρθεις σε μια προπόνηση μαζί μας;",
        "A: Καλησπέρα κόουτς, ευχαριστώ πολύ για το μήνυμα! Ναι, θα το ήθελα πολύ.",
        "C: Τέλεια! Προπονούμαστε Τρίτη και Πέμπτη στις {time} στο κλειστό μας. Σου κάνει την ερχόμενη Τρίτη;",
        "A: Την Τρίτη μπορώ κανονικά. Να φέρω κάτι συγκεκριμένο;",
        "C: Μόνο αθλητικά και παπούτσια για παρκέ. Έλα είκοσι λεπτά νωρίτερα να σου γνωρίσω {a:τις κοπέλες|τα παιδιά}.",
        "A: Τέλεια, ευχαριστώ! Τα λέμε την Τρίτη στο γήπεδο!");

    private static readonly Script Video = S("full-game-video",
        "C: Καλησπέρα {name}. Είδαμε τα highlights σου και θα θέλαμε να δούμε κάτι περισσότερο πριν κανονίσουμε συνάντηση.",
        "A: Καλησπέρα κόουτς! Χαίρομαι πολύ που σας ενδιαφέρει το προφίλ μου.",
        "C: Έχεις βίντεο από ολόκληρο αγώνα; Θέλω να δω κυρίως την άμυνα και τις επιστροφές.",
        "A: Ο σύλλογός μου βιντεοσκοπεί όλους τους εντός έδρας αγώνες. Θα ζητήσω από τον έφορο να σας στείλει τον τελευταίο.",
        "A+: Ο έφορος μου είπε ότι σας τον έστειλε σήμερα το πρωί. Είναι ο αγώνας της περασμένης Κυριακής.",
        "C: Τον είδα, πολύ καλή δουλειά στην άμυνα στο δεύτερο ημίχρονο. Θα σου πω μέχρι την Παρασκευή για τα επόμενα βήματα.",
        "A: Ευχαριστώ πολύ, περιμένω νέα σας!");

    private static readonly Script Role = S("minutes-role",
        "C: Γεια σου {name}! Ψάχνουμε {a:κάποια|κάποιον} με τα χαρακτηριστικά σου για τον δεύτερο γύρο.",
        "A: Γεια σας κόουτς, ευχαριστώ για το ενδιαφέρον!",
        "A: Μια ερώτηση πριν προχωρήσουμε: τι ρόλο βλέπετε για μένα στην ομάδα;",
        "C: Ειλικρινά; Ξεκινάς από τον πάγκο, αλλά με 15 με 20 λεπτά ανά αγώνα από την αρχή. Θέλω ενέργεια στην άμυνα και γρήγορες αποφάσεις.",
        "A: Αυτό ακούγεται πολύ καλό. Στην ομάδα μου τώρα παίζω κάτω από δέκα λεπτά.",
        "C: Το ξέρω, γι' αυτό σου έγραψα. Τα λεπτά τα κερδίζεις στην προπόνηση, εμείς δίνουμε ευκαιρίες.",
        "A: Ευχαριστώ για την ειλικρίνεια. Θα ήθελα να έρθω σε μια προπόνηση να δω πώς δουλεύετε.");

    private static readonly Script Exams = S("studies-exams",
        "C: Καλησπέρα {name}, μας ενδιαφέρει να σε έχουμε στο ρόστερ από τον Ιανουάριο. Πώς είναι το πρόγραμμά σου;",
        "A: Καλησπέρα! Ευχαριστώ πολύ, χάρηκα με το μήνυμα.",
        "A: Είμαι στο τρίτο έτος στη σχολή και τον Ιανουάριο έχω εξεταστική. Τις προπονήσεις τις βγάζω, αλλά ίσως χάσω μία ή δύο.",
        "C: Κανένα πρόβλημα, οι σπουδές προηγούνται. Έχουμε {a:κι άλλες φοιτήτριες|κι άλλους φοιτητές} στην ομάδα και το προγραμματίζουμε μαζί.",
        "A: Αυτό με καθησυχάζει πολύ. Μετά τις 15 Φεβρουαρίου είμαι εντελώς {a:ελεύθερη|ελεύθερος}.",
        "C: Τέλεια. Έλα να γνωριστούμε πριν την εξεταστική και τα υπόλοιπα τα βρίσκουμε.");

    private static readonly Script Injury = S("injury-check",
        "C: Γεια σου {name}! Θέλουμε να σε δούμε σε προπόνηση. Πριν από αυτό, είσαι εντάξει σωματικά;",
        "A: Γεια σας! Ευχαριστώ πολύ για το ενδιαφέρον.",
        "A: Πέρυσι είχα διάστρεμμα στον αστράγαλο, αλλά έκανα φυσικοθεραπεία και παίζω κανονικά από τον Σεπτέμβριο.",
        "C: Χαίρομαι που το ακούω. Ο φυσικοθεραπευτής μας θα σου κάνει μια σύντομη αξιολόγηση την πρώτη μέρα, το κάνουμε σε όλους.",
        "A: Φυσικά, κανένα πρόβλημα. Μου φαίνεται πολύ σωστό.",
        "C: Ωραία. Σου γράφω μέρα και ώρα μέσα στην εβδομάδα.");

    private static readonly Script Family = S("family-relocation",
        "C: Καλησπέρα {name}. Θα θέλαμε να σε έχουμε στην ομάδα από την επόμενη σεζόν. Ξέρω ότι σημαίνει μετακόμιση, γι' αυτό σου γράφω νωρίς.",
        "A: Καλησπέρα κόουτς, σας ευχαριστώ πολύ! Δεν το περίμενα.",
        "C: Ο σύλλογος βοηθά στην εύρεση σπιτιού κοντά στο γήπεδο και έχουμε {a:δύο κοπέλες|δύο παίκτες} που ήρθαν από άλλη πόλη πέρυσι. Μπορείς να μιλήσεις μαζί τους.",
        "A: Αυτό βοηθάει πολύ. Θα το συζητήσω με την οικογένειά μου και θα σας απαντήσω μέσα στην εβδομάδα.",
        "C: Φυσικά, πάρε τον χρόνο σου. Αν έχουν ερωτήσεις, ευχαρίστως να τους εξηγήσω κι εγώ πώς δουλεύουμε.",
        "A: Ευχαριστώ, θα τους το πω!");

    private static readonly Script Declined = S("declined-politely",
        "C: Γεια σου {name}! Ψάχνουμε {a:μια παίκτρια|έναν παίκτη} με την ενέργειά σου για τον δεύτερο γύρο. Θα ήθελες να το συζητήσουμε;",
        "A: Καλησπέρα κόουτς, ευχαριστώ πολύ που μου γράψατε.",
        "C: Σκεφτόμαστε να σε δοκιμάσουμε σε ρόλο πρώτης αλλαγής. Πες μου αν σε ενδιαφέρει να έρθεις να μας γνωρίσεις.",
        "A+: Το σκέφτηκα πολύ και μίλησα και με το τεχνικό επιτελείο. Φέτος θα μείνω στην ομάδα μου, έχουμε στόχο την άνοδο και θέλω να τον ολοκληρώσω.",
        "A: Σας ευχαριστώ πραγματικά για την πρόταση, σημαίνει πολλά για μένα.",
        "C: Το σέβομαι απόλυτα, καλή επιτυχία στον στόχο σας! Η πόρτα μένει ανοιχτή για το καλοκαίρι.",
        "A: Ευχαριστώ πολύ, καλή συνέχεια και σε εσάς!");

    private static readonly Script Saturday = S("far-away-saturday",
        "C: Καλησπέρα {name}, θα ήθελα να σε καλέσω σε μια προπόνηση γνωριμίας την επόμενη εβδομάδα.",
        "A: Καλησπέρα! Ευχαριστώ πολύ. Μένω αρκετά μακριά, υπάρχει περίπτωση να έρθω Σάββατο;",
        "C: Βεβαίως. Το Σάββατο έχουμε πρωινή προπόνηση στις 10:30, έλα τότε.");

    private static readonly Script Amateur = S("amateur-schedule",
        "C: Γεια σου {name}! Χρειαζόμαστε βάθος στο ρόστερ για τον δεύτερο γύρο. Σε είδαμε στο τουρνουά του Οκτωβρίου και μας έκανες εντύπωση.",
        "A: Γεια σας κόουτς! Ευχαριστώ, εκείνο το τουρνουά ήταν πολύ καλό για μένα.",
        "A: Πώς λειτουργεί η ομάδα; Παίζω ερασιτεχνικά και δουλεύω πρωί.",
        "C: Είμαστε καθαρά ερασιτεχνική ομάδα. Προπονήσεις τρεις φορές την εβδομάδα στις {time}, αγώνες το Σαββατοκύριακο. Οι περισσότεροι δουλεύουν ή σπουδάζουν.",
        "A: Τέλεια, αυτό ταιριάζει ακριβώς στο πρόγραμμά μου.",
        "C: Τότε έλα την Τετάρτη να σε δούμε. Αν σου αρέσει το κλίμα, τα υπόλοιπα είναι απλά.",
        "A: Θα είμαι εκεί!");

    private static readonly Script Thursday = S("thursday-visit",
        "C: Καλησπέρα {name}. Θέλω να σε γνωρίσω από κοντά. Πότε είσαι {a:διαθέσιμη|διαθέσιμος} αυτή την εβδομάδα;",
        "A: Καλησπέρα κόουτς! Ευχαριστώ για την πρόσκληση.",
        "A: Είμαι {a:διαθέσιμη|διαθέσιμος} Πέμπτη και Παρασκευή απόγευμα.",
        "C: Πέμπτη στις {time} λοιπόν, στο κλειστό μας. Θα σε περιμένω στην είσοδο.",
        "A: Τέλεια, θα είμαι εκεί.",
        "C: Ωραία! Φέρε και την κάρτα υγείας αθλητή, τη ζητάει η γραμματεία.");

    private static readonly Script GameVisit = S("watching-the-game",
        "C: Γεια σου {name}! Θα έρθω να δω τον αγώνα σας την Κυριακή. Θα ήθελα να μιλήσουμε λίγο μετά, αν έχεις χρόνο.",
        "A: Γεια σας! Ευχαριστώ πολύ, θα χαρώ να γνωριστούμε.",
        "A: Ο αγώνας είναι στις 17:00 στην έδρα μας.",
        "C: Το είδα στο πρόγραμμα. Θα είμαι στην κερκίδα, μην αγχωθείς!",
        "A: Χαχα, θα προσπαθήσω! Τα λέμε εκεί.",
        "C+: Πολύ ωραίο παιχνίδι σήμερα, ειδικά στο τελευταίο δεκάλεπτο. Ευχαριστώ για την κουβέντα!",
        "A: Εγώ ευχαριστώ που ήρθατε. Περιμένω να μου πείτε για την προπόνηση.");

    private static readonly Script SecondRound = S("second-round-plan",
        "C: Καλησπέρα {name}. Ετοιμαζόμαστε για τον δεύτερο γύρο και θέλουμε ενίσχυση. Θα ήθελα να σε δω σε δράση.",
        "A: Καλησπέρα κόουτς, χαίρομαι πολύ που μου γράψατε!",
        "C: Ξεκινάμε με το νέο ρόστερ αρχές Ιανουαρίου. Μέχρι τότε θα σου στείλω ένα πρόγραμμα φυσικής κατάστασης να το ακολουθήσεις.",
        "A: Πολύ ωραία. Μπορώ να ρωτήσω πόσ{a:ες παίκτριες|οι παίκτες} θα είναι στο ρόστερ;",
        "C: Δώδεκα στο ρόστερ και άλλ{a:ες|οι} τρεις από τις ακαδημίες που προπονούνται μαζί μας.",
        "A: Κατάλαβα. Θα κάνω ό,τι μπορώ για να είμαι {a:έτοιμη|έτοιμος}.");

    private static readonly Script Skills = S("one-on-one-skills",
        "C: Γεια σου {name}! Μου άρεσε πολύ το σουτ σου από μέση απόσταση. Θα ήθελες να κάνουμε μια ατομική προπόνηση;",
        "A: Γεια σας! Ναι, πολύ ευχαρίστως.",
        "C: Με ποιο χέρι νιώθεις πιο σίγουρα στη ντρίμπλα;",
        "A: Με το δεξί σίγουρα. Το αριστερό το δουλεύω από το καλοκαίρι.",
        "C: Φαίνεται στο βίντεο, έχει βελτιωθεί. Στην ομάδα παίζουμε πολύ πικ εν ρολ, οπότε θα το χρειαστείς.",
        "A: Το ξέρω, είναι ο στόχος μου φέτος.",
        "C: Ωραία. Τι ώρες σου ταιριάζουν;",
        "A: Μετά τις έξι κάθε μέρα εκτός Δευτέρας.",
        "C: Τετάρτη στις {time}; Το γήπεδο είναι ελεύθερο μία ώρα πριν την ομαδική.",
        "A: Τέλεια, Τετάρτη!",
        "C: Σημειώθηκε. Φέρε και νερό, θα ιδρώσουμε!",
        "A: Χαχα, εννοείται. Ευχαριστώ πολύ κόουτς!");

    private static readonly Script FollowUp = S("coach-follow-up",
        "C: Γεια σου {name}, ευχαριστώ που αποδέχτηκες το ενδιαφέρον μας! Πότε θα μπορούσες να έρθεις να γνωριστούμε;",
        "A: Γεια σας! Αυτή την εβδομάδα έχω πολύ φόρτο, μπορούμε την επόμενη;",
        "C+: Φυσικά. Η Δευτέρα ή η Τετάρτη στις {time} σου κάνει;",
        "C: Αν θες, φέρε και τα παπούτσια σου να ρίξεις λίγα σουτ μαζί μας.");

    private static readonly Script FastBreak = S("fast-break-team",
        "C: Καλησπέρα {name}! Χτίζουμε ομάδα με έμφαση στο γρήγορο παιχνίδι και πιστεύω ότι ταιριάζεις.",
        "A: Καλησπέρα! Σας ευχαριστώ, μου αρέσει πολύ το γρήγορο παιχνίδι.",
        "A: Πώς είναι μια τυπική εβδομάδα προπονήσεων;",
        "C: Τρεις ομαδικές, μία ατομική για όποιον θέλει και ένα γυμναστήριο. Τη Δευτέρα βλέπουμε μαζί βίντεο από τον προηγούμενο αγώνα.",
        "A: Το βίντεο μου αρέσει πολύ, στην ομάδα μου δεν το κάνουμε.",
        "C: Βοηθάει πολύ, θα δεις. Θες να έρθεις την Πέμπτη να δεις μια προπόνηση από κοντά;",
        "A: Ναι, θα έρθω! Τι ώρα;",
        "C: Στις {time}. Θα σε περιμένω.");

    private static readonly Script Masters = S("masters-move",
        "C: Γεια σου {name}! Είδαμε ότι ψάχνεις ομάδα. Θα σε ενδιέφερε να το συζητήσουμε;",
        "A: Γεια σας! Ναι, πολύ. Από τον Φεβρουάριο μετακομίζω για μεταπτυχιακό, οπότε ψάχνω ομάδα στη νέα πόλη.",
        "C: Τι ωραία σύμπτωση! Το γήπεδό μας είναι δέκα λεπτά από την πανεπιστημιούπολη.",
        "A: Αλήθεια; Αυτό θα ήταν ιδανικό.",
        "C: Έλα όταν τακτοποιηθείς να κάνεις μια προπόνηση. Μέχρι τότε τα λέμε εδώ.",
        "A: Συμφωνώ, ευχαριστώ πολύ!");

    private static readonly Script Question = S("tryout-question",
        "C: Καλησπέρα {name}. Θα χαρούμε να σε δούμε σε μια δοκιμαστική προπόνηση.",
        "A: Καλησπέρα κόουτς! Ευχαριστώ, θα έρθω σίγουρα.",
        "A: Μια ερώτηση: η δοκιμαστική είναι μαζί με την ομάδα ή ατομική;");

    private static readonly Script Welcome = S("tryout-to-welcome",
        "C: Γεια σου {name}! Θα ήθελα να σε καλέσω σε δοκιμαστική προπόνηση με την ομάδα μας.",
        "A: Γεια σας κόουτς! Ευχαριστώ πολύ για την πρόσκληση.",
        "A: Πότε θα ήταν;",
        "C: Δευτέρα στις {time}. Θα κάνουμε κυρίως ασκήσεις και ένα δίτερμα στο τέλος.",
        "A: Εντάξει, θα είμαι εκεί!",
        "C+: Ευχαριστώ που ήρθες σήμερα! Πώς σου φάνηκε;",
        "A: Μου άρεσε πολύ. Η ομάδα είναι πολύ δεμένη.",
        "C+: Η δοκιμαστική πήγε πολύ καλά. Θα θέλαμε να σε έχουμε στην ομάδα από τον επόμενο μήνα.",
        "A: Αλήθεια; Σας ευχαριστώ πάρα πολύ!",
        "A: Πρέπει να κάνω κάτι για τη μεταγραφή; Είναι η πρώτη φορά που αλλάζω ομάδα.",
        "C: Η γραμματεία του συλλόγου θα σε καθοδηγήσει για το δελτίο, είναι απλή διαδικασία.",
        "A: Τέλεια. Ανυπομονώ να ξεκινήσω!",
        "C: Κι εμείς! Καλώς ήρθες στην ομάδα.");

    private static readonly Script Schedule = S("training-days",
        "C: Καλησπέρα {name}! Θα ήθελες να προπονηθείς μαζί μας για λίγες εβδομάδες, να γνωριστούμε;",
        "A: Ναι, πολύ ευχαρίστως! Ποιες μέρες προπονείστε;",
        "C: Δευτέρα, Τετάρτη και Παρασκευή στις {time}. Ξεκίνα όποτε θες.",
        "A: Θα ξεκινήσω από την Τετάρτη. Ευχαριστώ κόουτς!");

    private static readonly Script WhichLeague = S("which-league",
        "C: Γεια σου {name}! Η ομάδα μας ανεβαίνει δυναμικά και θέλουμε {a:παίκτριες|παίκτες} με τη δική σου νοοτροπία.",
        "A: Γεια σας, ευχαριστώ πολύ! Σε ποια κατηγορία αγωνίζεστε;",
        "C: Φέτος είμαστε {inLeague}. Ο στόχος είναι να μείνουμε ψηλά και να δώσουμε χρόνο σε νέα παιδιά.",
        "A: Ωραία. Ποιος θα ήταν ο ρόλος μου;",
        "C: Θα σε δοκιμάσουμε και στις δύο θέσεις που παίζεις. Θέλω να σε δω πρώτα στην προπόνηση.",
        "A: Δίκαιο. Πείτε μου πότε να έρθω.");

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

    private static readonly string[] TrainingTimes = ["18:30", "19:00", "19:30", "20:00", "20:30"];

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
            interest.Note = r.Double() < 0.45 ? Notes[r.Int(0, Notes.Length - 1)] : null;
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
            conversation.Messages.Add(new Message { SenderKind = MessageSender.Coach, Body = interest.Note, SentAt = interest.CreatedAt, ReadAt = interest.SeenAt, IsDemo = true });

        var time = TrainingTimes[r.Int(0, TrainingTimes.Length - 1)];
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
                SenderKind = lines[k].Sender, Body = Fill(lines[k].Text, athlete, interest.Coach!, time), SentAt = times[k], ReadAt = readAt, IsDemo = true,
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
