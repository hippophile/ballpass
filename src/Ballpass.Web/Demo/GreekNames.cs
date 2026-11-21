namespace Ballpass.Web.Demo;

public record SurnamePair(string Male, string Female, string MaleLatin, string FemaleLatin);

public record FirstName(string Greek, string Latin);

// Genitive = form used in club names ("Μέλισσες Πάτρας").
public record GreekCity(string Name, string NameLatin, string Region, string Genitive);

// Common Greek names for fictional demo data. Latin forms are ELOT-style, lowercase-safe (no accents).
public static class GreekNames
{
    public static bool IsFemaleFirstName(string greekFirstName) =>
        FemaleFirstNames.Any(n => n.Greek == greekFirstName);

    public static IReadOnlyList<FirstName> FemaleFirstNames { get; } =
    [
        new("Μαρία", "Maria"), new("Ελένη", "Eleni"), new("Αικατερίνη", "Aikaterini"), new("Βασιλική", "Vasiliki"),
        new("Σοφία", "Sofia"), new("Αγγελική", "Angeliki"), new("Γεωργία", "Georgia"), new("Δήμητρα", "Dimitra"),
        new("Ευαγγελία", "Evangelia"), new("Κωνσταντίνα", "Konstantina"), new("Αναστασία", "Anastasia"), new("Χριστίνα", "Christina"),
        new("Παναγιώτα", "Panagiota"), new("Ιωάννα", "Ioanna"), new("Αθανασία", "Athanasia"), new("Δέσποινα", "Despoina"),
        new("Ζωή", "Zoi"), new("Ειρήνη", "Eirini"), new("Αλεξάνδρα", "Alexandra"), new("Νικολέτα", "Nikoleta"),
        new("Μαριάννα", "Marianna"), new("Θεοδώρα", "Theodora"), new("Φωτεινή", "Fotini"), new("Σταυρούλα", "Stavroula"),
        new("Πολυξένη", "Polyxeni"), new("Ελπίδα", "Elpida"), new("Κυριακή", "Kyriaki"), new("Μαργαρίτα", "Margarita"),
        new("Ευθυμία", "Efthymia"), new("Άννα", "Anna"), new("Βάσω", "Vaso"), new("Δανάη", "Danai"),
        new("Ραφαέλα", "Rafaela"), new("Στέλλα", "Stella"), new("Τριανταφυλλιά", "Triantafyllia"), new("Χρυσάνθη", "Chrysanthi"),
        new("Μυρτώ", "Myrto"), new("Κλεοπάτρα", "Kleopatra"), new("Ευτυχία", "Eftychia"), new("Ιουλία", "Ioulia"),
        new("Λήδα", "Lida"), new("Φανή", "Fani"),
    ];

    public static IReadOnlyList<FirstName> MaleFirstNames { get; } =
    [
        new("Γιώργος", "Giorgos"), new("Γιάννης", "Giannis"), new("Κώστας", "Kostas"), new("Δημήτρης", "Dimitris"),
        new("Νίκος", "Nikos"), new("Παναγιώτης", "Panagiotis"), new("Βασίλης", "Vasilis"), new("Χρήστος", "Christos"),
        new("Αθανάσιος", "Athanasios"), new("Μιχάλης", "Michalis"), new("Θανάσης", "Thanasis"), new("Αντώνης", "Antonis"),
        new("Στέλιος", "Stelios"), new("Μανώλης", "Manolis"), new("Σπύρος", "Spyros"), new("Πέτρος", "Petros"),
        new("Αλέξανδρος", "Alexandros"), new("Ευάγγελος", "Evangelos"), new("Λάμπρος", "Lampros"), new("Θοδωρής", "Thodoris"),
        new("Ιωάννης", "Ioannis"), new("Κωνσταντίνος", "Konstantinos"), new("Σταύρος", "Stavros"), new("Ανδρέας", "Andreas"),
        new("Μάριος", "Marios"), new("Φώτης", "Fotis"), new("Τάσος", "Tasos"), new("Γρηγόρης", "Grigoris"),
        new("Παύλος", "Pavlos"), new("Λευτέρης", "Lefteris"), new("Άρης", "Aris"), new("Ορέστης", "Orestis"),
        new("Διονύσης", "Dionysis"), new("Ηλίας", "Ilias"), new("Σωτήρης", "Sotiris"), new("Χαράλαμπος", "Charalampos"),
        new("Αργύρης", "Argyris"), new("Κίμων", "Kimon"), new("Νικόλαος", "Nikolaos"), new("Θεόδωρος", "Theodoros"),
        new("Μιλτιάδης", "Miltiadis"), new("Ραφαήλ", "Rafail"),
    ];

    public static IReadOnlyList<SurnamePair> Surnames { get; } =
    [
        new("Παπαδόπουλος", "Παπαδοπούλου", "Papadopoulos", "Papadopoulou"),
        new("Γεωργίου", "Γεωργίου", "Georgiou", "Georgiou"),
        new("Νικολάου", "Νικολάου", "Nikolaou", "Nikolaou"),
        new("Καραγιάννης", "Καραγιάννη", "Karagiannis", "Karagianni"),
        new("Παπαγεωργίου", "Παπαγεωργίου", "Papageorgiou", "Papageorgiou"),
        new("Οικονόμου", "Οικονόμου", "Oikonomou", "Oikonomou"),
        new("Ιωάννου", "Ιωάννου", "Ioannou", "Ioannou"),
        new("Δημητρίου", "Δημητρίου", "Dimitriou", "Dimitriou"),
        new("Παππάς", "Παππά", "Pappas", "Pappa"),
        new("Βασιλείου", "Βασιλείου", "Vasileiou", "Vasileiou"),
        new("Αναγνωστόπουλος", "Αναγνωστοπούλου", "Anagnostopoulos", "Anagnostopoulou"),
        new("Αντωνίου", "Αντωνίου", "Antoniou", "Antoniou"),
        new("Μακρής", "Μακρή", "Makris", "Makri"),
        new("Κωνσταντινίδης", "Κωνσταντινίδου", "Konstantinidis", "Konstantinidou"),
        new("Ιωαννίδης", "Ιωαννίδου", "Ioannidis", "Ioannidou"),
        new("Θεοδωρόπουλος", "Θεοδωροπούλου", "Theodoropoulos", "Theodoropoulou"),
        new("Αλεξίου", "Αλεξίου", "Alexiou", "Alexiou"),
        new("Βλάχος", "Βλάχου", "Vlachos", "Vlachou"),
        new("Παπαναστασίου", "Παπαναστασίου", "Papanastasiou", "Papanastasiou"),
        new("Μαυρίδης", "Μαυρίδου", "Mavridis", "Mavridou"),
        new("Κυριακόπουλος", "Κυριακοπούλου", "Kyriakopoulos", "Kyriakopoulou"),
        new("Σταματόπουλος", "Σταματοπούλου", "Stamatopoulos", "Stamatopoulou"),
        new("Ζαχαρίου", "Ζαχαρίου", "Zachariou", "Zachariou"),
        new("Αθανασίου", "Αθανασίου", "Athanasiou", "Athanasiou"),
        new("Χατζηδάκης", "Χατζηδάκη", "Chatzidakis", "Chatzidaki"),
        new("Λαμπρόπουλος", "Λαμπροπούλου", "Lampropoulos", "Lampropoulou"),
        new("Παπαϊωάννου", "Παπαϊωάννου", "Papaioannou", "Papaioannou"),
        new("Τσιμάς", "Τσιμά", "Tsimas", "Tsima"),
        new("Μανωλάκης", "Μανωλάκη", "Manolakis", "Manolaki"),
        new("Κοντός", "Κοντού", "Kontos", "Kontou"),
        new("Ράλλης", "Ράλλη", "Rallis", "Ralli"),
        new("Σπυρόπουλος", "Σπυροπούλου", "Spyropoulos", "Spyropoulou"),
        new("Δρόσος", "Δρόσου", "Drosos", "Drosou"),
        new("Φωτόπουλος", "Φωτοπούλου", "Fotopoulos", "Fotopoulou"),
        new("Μπακάλης", "Μπακάλη", "Bakalis", "Bakali"),
        new("Γιαννακόπουλος", "Γιαννακοπούλου", "Giannakopoulos", "Giannakopoulou"),
        new("Παπαδάκης", "Παπαδάκη", "Papadakis", "Papadaki"),
        new("Καλογεράκης", "Καλογεράκη", "Kalogerakis", "Kalogeraki"),
        new("Χριστοδούλου", "Χριστοδούλου", "Christodoulou", "Christodoulou"),
        new("Σωτηρίου", "Σωτηρίου", "Sotiriou", "Sotiriou"),
        new("Λιάκος", "Λιάκου", "Liakos", "Liakou"),
        new("Δασκαλάκης", "Δασκαλάκη", "Daskalakis", "Daskalaki"),
        new("Πετρόπουλος", "Πετροπούλου", "Petropoulos", "Petropoulou"),
    ];

    public static IReadOnlyList<GreekCity> Cities { get; } =
    [
        new("Αθήνα", "Athina", "Attica", "Αθηνών"),
        new("Πειραιάς", "Peiraias", "Attica", "Πειραιά"),
        new("Περιστέρι", "Peristeri", "Attica", "Περιστερίου"),
        new("Θεσσαλονίκη", "Thessaloniki", "Central Macedonia", "Θεσσαλονίκης"),
        new("Σέρρες", "Serres", "Central Macedonia", "Σερρών"),
        new("Πάτρα", "Patra", "Western Greece", "Πάτρας"),
        new("Αγρίνιο", "Agrinio", "Western Greece", "Αγρινίου"),
        new("Ηράκλειο", "Irakleio", "Crete", "Ηρακλείου"),
        new("Χανιά", "Chania", "Crete", "Χανίων"),
        new("Λάρισα", "Larisa", "Thessaly", "Λάρισας"),
        new("Βόλος", "Volos", "Thessaly", "Βόλου"),
        new("Ιωάννινα", "Ioannina", "Epirus", "Ιωαννίνων"),
        new("Κομοτηνή", "Komotini", "East Macedonia and Thrace", "Κομοτηνής"),
        new("Ρόδος", "Rodos", "South Aegean", "Ρόδου"),
    ];
}
