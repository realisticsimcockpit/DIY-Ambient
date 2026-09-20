# Recette — 0.1.1-alpha : sources corrigées, pas DLL validée

## Ce qui a réellement tourné pendant l'audit

| Vérification | Résultat | Portée |
|---|---|---|
| Python `tests/check_sources.py` | 17/17 | Structure, délimiteurs lexicaux, contrats textuels, JSON, scripts |
| Python `tests/check_models.py` | 7/7 | Modèles de référence indépendants, dont 10 000 trames aléatoires |
| C++ `tests/FirmwareHarness.cpp` + firmware original | 194/194 | Simulation du parseur avec Serial/FastLED factices |
| C# `tests/CoreTests.cs` | 53 fournis, **non exécutés ici** | Noyau réel à compiler/exécuter sous Windows |

Journaux : `audit-results/`. Les tests Python ne compilent et n'exécutent pas le C#. Le banc C++ inclut le firmware original inchangé mais n'émule pas l'électronique, les interruptions ou les délais de réception réelle. Il ne teste pas les appels SerialPort du plugin.

**Non validés ici :** compilation C#, scripts PowerShell Windows, SDK réel, chargement SimHub, interface WPF, capture native, communication USB, télémétrie en piste, courant réel. Aucun binaire exploitable en SimHub n'est livré.

## 1 — Construire, sans matériel

1. Extraire l'archive dans un dossier neuf. Exécuter `TESTER.cmd` : attendu **53/53 tests C#**. En cas d'erreur, conserver `artifacts/tests/compile.log` et `run.log`.
2. Exécuter `CONSTRUIRE.cmd` avec le SimHub installé. Attendre une nouvelle `artifacts/plugin/DIYAmbient.Plugin.dll`, version **0.1.1.0**, et un manifeste avec SHA-256. Aucun SDK de substitution.
3. Provoquer volontairement une erreur de chemin SDK : le script doit échouer et ne pas laisser une ancienne DLL locale présentée comme fraîchement construite. Restaurer ensuite le chemin correct et reconstruire.
4. Fermer SimHub avant copie manuelle ou installation explicite. Pas de modification de firmware ni de téléchargement automatique.

## 2 — Interface et modes, aperçu uniquement

- Démarrage OFF, première configuration Aperçu seul. Aucun port ouvert.
- Blanc fixe neutre de départ, Couleur fixe, Image des écrans. Luminosité continue. 0/10/20/60 = 0/5+5/10+10/30+30.
- Modifier le blanc : l'essai doit montrer du blanc, sans télémétrie ; Annuler/Echap/fermeture ne conservent rien. Enregistrer ne doit pas remplacer le mode quotidien ni son nombre de LED d'alertes. Couleurs de l'écran et couleur fixe inchangées par cette correction.
- Lors d'une alerte, observer que les autres LED du même fond ne changent pas de luminosité. Terminer l'effet : fond restitué.
- Tester gauche/droite, puis OFF/ON et ouverture/fermeture de configuration : aucun ancien test ne doit réapparaître.
- Actions SimHub externes : le mode et l'interrupteur affichés doivent rester synchronisés. Rechargement du plugin : arrêt de l'ancien moteur, pas de multiplication des timers ni propriétaires du port.
- Fin/relance SimHub : réglages enregistrés, sortie OFF. La reprise automatique d'une session n'est pas encore une fonctionnalité validée.

## 3 — Mapping et capture des trois écrans

- Confirmer les noms Windows réels ; DISPLAY1 central 21–40, DISPLAY2 1–20, DISPLAY3 41–60 uniquement si ces identifiants correspondent effectivement au montage.
- Déplacer/redimensionner plusieurs rectangles sur chaque écran, y compris sous la barre d'outils déplacée. Tester Annuler et Enregistrer, sauvegarde/relecture, identifications 1/20/21/40/41/60.
- Coordonnées négatives, échelles DPI mixtes, résolutions différentes et plusieurs cartes graphiques : recette native obligatoire.
- Afficher rouge, vert, bleu sur les trois moniteurs. Modifier une image seulement : les autres restent identiques. Une modification de géométrie ne doit pas utiliser une ancienne capture sous un nouveau mapping.
- Débrancher/rebrancher, Alt-Tab, verrouillage, veille, changements de résolution. Les images périmées doivent passer au noir ; pas de capture du principal à la place d'un écran absent.
- Mesurer CPU, RAM, handles GDI et réactivité pendant une session prolongée. La capture est GDI/SDR : ne pas présenter HDR ou plein écran exclusif comme supportés sans essais et adaptation.

## 4 — Matériel, uniquement après vérification électrique

Vérifier alimentation, courant nominal, câbles, protections et distribution. Ne pas alimenter tout le ruban en supposant que l'USB d'une carte suffit. Fixer un budget adapté, puis confirmer. Changer port/budget/autorisation doit remettre OFF et demander une nouvelle confirmation quand applicable.

Le firmware peut flasher R/G/B à pleine valeur pendant un reset : le plafond du plugin ne le contrôle pas. Préparer l'installation en conséquence. Commencer à faible intensité, confirmer ordre RGB et les 60 adresses. Mesurer le courant : les chiffres logiciels sont un modèle, pas une protection matérielle certifiée.

Tester ouverture et délais, conflit Prismatik/autre logiciel, retrait USB, reconnexion, changement de port OFF, édition sans resets répétés, et extinction normale. Contrôler séparément le comportement après arrêt brutal : dernière couleur conservée possible faute de watchdog. Un statut « Envoi » ne constitue pas un ACK des LED.

## 5 — Simulateur réel

Contrôler dans les données normalisées les propriétés exactes des drapeaux et du spotter. Un nom plausible ou un test manuel ne prouve pas leur existence. Les types enums nécessitent un adaptateur documenté, pas une conversion arbitraire.

Vérifier droite/gauche simultanées, N=0/10/60, priorité, retour au fond, pause, fin de jeu et expiration des données. Lancer/fermer deux simulations successivement : pas d'ancien worker en concurrence.

## Critères avant une release utilisable

Compilation C# propre, 53 tests réussis, chargement SDK réel, recette WPF/triples, communication Adalight physique, validation des données de course et de l'alimentation. Conserver les journaux et la version SimHub/Windows/GPU/firmware. DXGI/HDR, lissage et watchdog relèvent de développements distincts, pas de résultats acquis de cet audit.
