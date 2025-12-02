Konzolni C# projekat koji se zasniva na "rucnom" povezivanju klijenata (dronova) sa serverom.
Poenta je bila da se napravi simulacija nekog realnog sistema za poljoprivredne dronove koji dobijaju svoje obaveze od servera.
Postoje 2 vrste dronova.
SCOUT dronovi "ocitavaju" trenutno stanje zemlje i onda te informacije salju serveru, koji na osnovu datih informacija pravi zadatke za sve dronove. Zadaci za scout dronove se automatski pravi na odredjenom timeru
pa dodaju u listu zadataka i onda dodeljuju prvom slobodnom dronu kome odgovara taj tip zadatka.
WORKER dronovi "izvrsavaju" naredbe kao navodnjavanje, sejanje ili zetva. Dodata nasumicna sansa da se dron pokvari gde se salje ALARM serveru koji pravi zadatak sa prioritetom.
Sama komunikacija je odradjena putem ODP i TCP protokola, ODP je koriscen za statuse dronova i slanje zadataka, dok je TCP koriscen za alarme. Moguca komunikacija sa vise dronova u isto vreme. Nakon zavrsetka rada svaki
dron ispisuje svoj report gde ispise zadatke koje je radio.
Ovaj projekat mi je pod mozda da ga prosirim za diplomski, dodao bih samu klijentsku aplikaciju koja ce da komunicira sa serverom u kojoj se prikazuje real time stanje i pozicija dronova,
bazu podataka sa kojom moze da komunicira samo server, vise zadataka, vise nasumicnih situacija koje proizvode alarme kao i hitnu komunikaciju od strane servera koja instant prekida sve zadatke i zove dronove nazad
na stanicu i slicno.
