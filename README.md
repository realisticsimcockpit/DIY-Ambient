# DIY Ambient light EVO by REALISTIC SIMCOCKPIT

[Chaîne YouTube REALISTIC SIMCOCKPIT](https://www.youtube.com/@realisticsimcockpit)

Le mode **Animations inspirées de WLED** propose quatre effets 1D pour les 60 positions physiques : Colorloop, Rainbow, Fire Flicker et Loading. Loading propose le cycle aléatoire des palettes correspondant à `pal:1` de WLED. La vitesse, l'intensité et la couleur principale sont enregistrées avec les profils de jeu. Les alertes de télémétrie restent prioritaires et le plafond logiciel de 15 A s'applique à chaque image animée.

**0.2.1-alpha • 20 septembre 2026**

Éclairage de cockpit intégré à SimHub : **60 LED Adalight, trois moniteurs indépendants, blanc fixe, couleur fixe, animations, image des écrans et alertes partagées à égalité gauche/droite.**

## Statut réel

La DLL est compilée sous Windows contre les bibliothèques du SimHub installé. Elle a été installée et chargée dans SimHub ; les LED et COM32 ont été confirmés fonctionnels par l'utilisateur. La capture triple écran, chaque animation et la télémétrie de chaque jeu restent à valider en conditions réelles.

L'audit source initial reste dans **`docs/AUDIT_2026-09-20.md`**. La conception alternative de l'interface et l'étude des animations sont conservées séparément dans `docs`.

Le dépôt officiel est privé : `realisticsimcockpit/DIY-Ambient`. Aucun firmware n'est flashé automatiquement. Le [firmware EVO FastLED et sa maintenance facultative](docs/FIRMWARE_EVO.md) sont préparés ; la carte de l'utilisateur n'a pas été flashée.

## Ce qui reste simple

Au quotidien : ON/OFF, **blanc fixe / couleur fixe / animations / image des 3 écrans**, luminosité et **un seul nombre pair de LED d'alertes**. 0 les désactive ; 10 = 5 à gauche + 5 à droite ; 60 = 30 + 30. Hors alerte, les LED retrouvent le fond.

La configuration initiale garde tes plages : **DISPLAY1 central 21–40, DISPLAY2 1–20, DISPLAY3 41–60**. Les rectangles se placent individuellement sur chaque moniteur. Les rôles et positions des rectangles déterminent la répartition gauche/droite ; vérifier les adresses physiques par les tests d'identification.

Une seule DLL au fonctionnement, **aucun programme auxiliaire externe**. L'alpha utilise deux workers internes : capture et sortie série. Ils ne constituent pas une isolation de processus contre un crash natif.

## Principales nouveautés 0.2.x

| Sujet | Changement |
|---|---|
| Luminosité | Plafond stable : une alerte rouge ne fait plus augmenter le gain des autres LED dans la formule. |
| Blanc | Préréglage propre au blanc, sans teinter l'image et les alertes. Les essais ne sont pas sauvegardés automatiquement. |
| Sécurité des transitions | Changer port/budget/autorisation désarme le moteur. Les anciens tests/captures ne sont pas réutilisés après changement d'état. |
| Capture triple | Contextes GDI par moniteur, bitmap natif compatible, synchronisation et nettoyage revus. **Toujours expérimental et non testé sous Windows.** |
| Connexion | Un seul worker peut posséder la sortie ; délai borné avant fermeture après le noir ; le port peut rester ouvert pendant l'édition pour éviter des resets répétés. |
| Construction | Anciennes sorties supprimées avant contrôle, compilation préparée séparément, journaux et manifeste de version/hash. |
| Animations | Colorloop, Rainbow, Fire Flicker et Loading, avec vitesse, intensité et couleur principale. |
| Profils | Enregistrement/chargement et sélection automatique d'un profil portant le nom du jeu SimHub actif. |
| Alimentation | Choix 3 × 60 ou 5 × 60 ; plafond logiciel fixe de 15 A appliqué à chaque image. |
| Arrêt | Noir envoyé à la fermeture par défaut, avec option explicite pour conserver la dernière couleur. |

## Vérifications exécutées et non exécutées

- **30 contrôles Python de sources et modèles réussis** : ils ne remplacent pas la compilation C#.
- **194 cas réussis du parseur du firmware original**, compilé dans un banc C++ avec Serial/FastLED simulés : pas un essai USB, temporel ou électrique.
- **60 tests C# réussis sous Windows** avant chaque build du plugin.

Les journaux se trouvent dans `audit-results/`. Ne pas additionner ces catégories pour prétendre que le plugin a été testé dans SimHub.

## Construire sous Windows

Extraire dans un **dossier neuf**, par exemple `Documents\DIY-Ambient-0.2.1`.

1. Exécuter **`TESTER.cmd`**. Attendu : 60 scénarios C# réussis, sans SimHub ni port série.
2. Exécuter **`CONSTRUIRE.cmd`**. Il refait les tests, utilise le compilateur C# de .NET Framework et les DLL du SimHub installé, puis prépare `artifacts\plugin\DIYAmbient.Plugin.dll`. Aucune dépendance n'est téléchargée.
3. Vérifier `artifacts\plugin\build-manifest.json` et les journaux. Le résultat est une compilation, **pas une certification de fonctionnement**.
4. Fermer SimHub puis copier **cette DLL seulement** à côté de `SimHubWPF.exe`. Relancer SimHub et activer le plugin.

Pour un chemin personnalisé :

```powershell
.\scripts\Build.ps1 -SimHubPath 'D:\SimHub'
```

Installation explicite, SimHub fermé et avec les droits d'écriture nécessaires :

```powershell
.\scripts\Build.ps1 -SimHubPath 'C:\Program Files (x86)\SimHub' -Install
```

Sans `-Install`, aucune installation automatique. L'installation explicite sauvegarde l'ancienne DLL. Aucune DLL SimHub n'est redistribuée. Un projet Visual Studio .NET Framework 4.8 / C# 5 est aussi fourni ; les scripts restent la voie prévue pour imposer les tests avant le build.

## Premier essai

L'état **Éclairage activé** est mémorisé. S'il était actif à la fermeture, le plugin tente de le réactiver à l'ouverture suivante. OFF ferme le port après une tentative d'envoi du noir ; l'extinction physique reste sans accusé de réception.

Tester d'abord l'interface, le blanc et N=0/10/20/60. Configurer les trois moniteurs et vérifier rouge/vert/bleu sur trois écrans différents, en SDR fenêtré/sans bordure. Ne pas considérer un affichage « envoi Adalight » comme un accusé de réception : le firmware n'en fournit pas.

Dans le panneau principal, choisir le port et le nombre de bandes. Fermer Prismatik et les sorties SimHub concurrentes.

Le plafond logiciel est fixé à **15 A** pour l'alimentation 5 V / 75 W déclarée. Le modèle estime 1 mA de repos par LED + jusqu'à 20 mA par composante, hors contrôleur. Il réduit automatiquement 5 × 60 au besoin. Ce calcul n'est pas une mesure de courant ni une protection contre un défaut électrique.

## Limites non résolues

La capture reste **GDI/SDR expérimentale, environ 15 captures/s au maximum**, pas DXGI. HDR, plein écran exclusif, DPI mixtes, écrans pivotés et tenue en session prolongée restent à tester. Un appel GDI bloqué peut retarder le worker de capture complet. Les identifiants DISPLAY ne sont pas des identifiants matériels persistants : revérifier le mapping si Windows les renumérote.

Le spotter, les drapeaux et la pause doivent être validés avec les champs réellement exposés par chaque simulation. Les types inconnus restent indisponibles. Les boutons de test ne prouvent pas le fonctionnement en course.

Le lissage et l'import Prismatik ne sont pas implémentés. Les profils sont chargés automatiquement uniquement lorsqu'un profil porte exactement le nom du jeu fourni par SimHub.

Le réglage du blanc est visuel, pas une mesure colorimétrique. La limitation PC ne mesure pas le courant et ne protège pas d'un défaut électrique. **Les flashes de démarrage du firmware échappent à ce plafond**, et sans watchdog une dernière couleur peut persister après un crash/débranchement. Le noir de fermeture reste une tentative non confirmée.

## Reproduire les contrôles locaux complémentaires

```text
python tests/check_sources.py
python tests/check_models.py
```

Banc facultatif du parseur original, avec un compilateur C++ hôte — ne jamais flasher les doubles de test :

```bash
mkdir -p artifacts/audit
g++ -std=c++11 -O2 -Itests/fake_arduino tests/FirmwareHarness.cpp -o artifacts/audit/firmware-harness
./artifacts/audit/firmware-harness
```

Le test de référence numérique reproduit les formules indépendamment. Le banc C++ compile le sketch fourni inchangé mais remplace Serial/FastLED ; il ne simule ni débit réel, ni interruptions, ni consommation.

## Dossiers

`src/DIYAmbient.Core` : paramètres, couleurs, composition, protocole, politiques d'état et lecture prudente des flags. `src/DIYAmbient.Plugin` : intégration SimHub, WPF, capture, communication et stockage. `tests` : tests C# et contrôles locaux distincts. `audit-results` : preuves exécutées. `changes` : patch depuis 0.1.0. `docs` : audit, architecture, recette, statut et exemple.

Configuration réelle : `%LOCALAPPDATA%\DIY-Ambient\settings.json`. Journal local borné : `diagnostic.log`. Aucun service réseau, aucune image enregistrée par le plugin.

Références techniques et limites du SDK : voir le rapport d'audit.
