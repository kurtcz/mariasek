Mariasek: AI
============

AI simuluje nejistotu tak, že náhodně generuje rozložení karet soupeřů a pro každé rozložení simuluje otevřenou hru (kdy se kouká soupeřům do hypotetických karet).
Zjednodušeně řečeno potom AI zahraje takovou kartu, která mu nejčastěji v simulacích vyšla jako správná. Stěžejní je stihnout dostatek simulací pro zajištění reprezentativního vzorku.

Pravidla pro hru
----------------

Pravidla se dělí do těchto skupin:

1. Bodovaná pravidla
	Bodovaná pravidla jsou pravidla, která vedou k zisku bodů (získání A, X). U některých pravidel hrozí ovšem riziko ztráty. Hrát kartu podle takového pravidla by se mělo pouze pokud bude riziko neúspěchu dostatečně malé. Tuto míru definuje parametr v konfiguračním souboru. V následujícím seznamu budou riziková pravidla označena hvězdičkou (*).

	1.1 Uhrát poslední štych
	1.2 Uhrát vlastní X (*)
	1.3 Uhrát vlastní A pokud nemůže chytit soupeřovu X (*)
	1.4 Uhrát soupeřovu X (*)
	1.5 Uhrát spoluhráčovo A, X (*)

2. Přípravná pravidla
	Přípravná pravidla jsou pravidla, která připravují pozici pro uplatnění bodových pravidel v dalších kolech hry.

	2.1 Vytlačit soupeřovo A (kterým by mi bral X)
	2.2 Vytlačit soupeřův trumf (kterým by mi bral A, X)

3. Ostatní pravidla
	Ostatní pravidla zahrnují pravidla obecně posilující hráčovu pozici.

	3.1 Vytáhnout trumfy (nepatří sem, co s tím?)	
	3.2 Vytlačit soupeřův trumf
	3.3 Zůstat ve štychu
	3.4 Hraj dlouhou barvu (mimo trumf)
	3.5 Hraj cokoli mimo A, X
	3.6 Hraj cokoli

### Účelová funkce

Účelová funkce ohodnocující sílu pozice ve hře by měla zohlednit tyto faktory v následujícím pořadí důležitosti:
1. Součet mých uhraných bodů plus bodů které skoro jistě získám (moje i soupeřovy A, X které bych měl uhrát)
2. Počet karet kterýma soupeř bere moje A, X (sestupně)
3. Rozdíl v počtu trumfů
4. Počet mých trumfů

Pravidla pro kilo
-----------------

Pravidla pro kilo jsou momentálně stejná jako pro hru.

Pravidla pro betl
-----------------

Pravidla pro betl jsou obdobná jak pro volitele tak pro oponenty. Oba tábory se snaží dostat soupeře do štychu.
1. Hraj vítěznou kartu (oponent)
2. Odmazat si vysokou kartu
3. Odmazat si barvu (pro 2., 3. hráče v daném kole)
4. Hraj krátkou barvu

Pravidla pro durch
------------------

Pravidla pro durch se liší pro volitele (vždy 1. na tahu) a pro oponenty (hrají 2., 3. v pořadí)

1. Pravidla pro volitele:
1.1 Hrát od Anejdelší vítěznou barvu
1.2 Hrát nejdelší barvu

2. Pravidla pro oponenty:
2.1 Hrát vítěznou kartu
2.2 Hrát nejmenší kartu v barvě
2.3 Hrát nejmenší kartu v barvě ve které nechytám soupeře
2.4 Hrát nejmenší kartu

### Přesnost simulací

Pro určení míry spolehlivosti simulací (jak moc se generované hry blíží pravděpodobnostnímu rozložení) použijeme výběrovou směrodatnou odchylku pro pravděpodobnost karty
počítáme jako s = SQRT( 1 / (N - 1) * SUM(( Xi - AVG(X) )^2) ) kde Xi je počet karet v simulaci a AVG(X) je pravděpodobnost karty.
Výběrovou směrodatnou odchylku všech karet spočítám jako průměr jednotlivých směrodatných odchylek.
Pro normální rozdělení platí, že:
pravděpodobnost, že se náhodný výběr bude lišit od středních hodnot bude lišit od více než jednu odchylku je < 33%
pravděpodobnost, že se náhodný výběr bude lišit od středních hodnot bude lišit od více než dvě odchylky je < 5%
Můžu proto generovat náhodné karty a sledovat jak mi klesá odchylka a rozhodnout se kdy simulace zastavit:
(pokud vypršel čas nebo pokud se odchylka od průměru (1sigma, 2sigma, 3sigma) dostala pod zvolený práh)