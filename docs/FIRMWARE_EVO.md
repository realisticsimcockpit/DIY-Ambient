# Firmware EVO — préparation, sans flash du matériel

`firmware/Adalight_WS2812/Adalight_WS2812.ino` est une modification du **sketch original fourni par l'utilisateur**, pas une réécriture avec un parseur indépendant. L'en-tête d'auteur, FastLED, les boucles de lecture de l'en-tête et des composantes RGB, les 60 adresses WS2812/NEOPIXEL sur **D6** et les **115200 bauds** sont conservés. Le fichier original sur Google Drive reste intact.

Compilation avec **FastLED 3.9.15**, Arduino AVR **1.8.6**. La carte indiquée par l'utilisateur est une Nano WAVGAT ; le marquage exact du microcontrôleur et le bootloader restent à confirmer avant tout téléversement. Seul le programme applicatif est visé, pas le bootloader.

## Extinction autonome

- Démarrage au noir, sans séquence RGB.
- Attentes série bornées à la place des attentes infinies d'origine : seules les 180 valeurs RGB d'une trame complète remplacent l'image affichée.
- Abandon d'une réception partielle après 100 ms sans octet.
- Noir après 1000 ms sans trame complète, même si des octets parasites continuent d'arriver.
- L'option « garder allumé » est envoyée uniquement lors d'une fermeture normale, si l'éclairage était activé et le firmware EVO identifié. Elle n'est pas enregistrée en EEPROM. La prochaine trame Adalight réarme l'extinction automatique ; un redémarrage de la carte revient au noir.

Le contrôleur et les LED doivent rester alimentés pour conserver une couleur après l'arrêt du PC. Un arrêt brutal ne peut pas transmettre la commande de conservation. Le délai logiciel ne protège ni d'un contrôleur figé ni d'une panne électronique. Le protocole Adalight n'a pas de checksum des pixels ; les tests ne prouvent pas l'absence de pertes UART pendant les interruptions FastLED.

## Maintenance depuis le panneau principal

Le panneau principal possède trois onglets : **Installation** (port, bandes, écrans et conservation à l'arrêt), **Personnalisation** (couleurs, animations, télémétrie, profils) et **Firmware**. « Préparer les outils » télécharge les outils officiels Arduino et FastLED, sur demande, dans `%LOCALAPPDATA%\DIY-Ambient\FirmwareTools`. Aucun processus externe n'est nécessaire pendant l'éclairage normal.

« Charger depuis GitHub » récupère le sketch depuis `realisticsimcockpit/DIY-Ambient`, branche `main`, via GitHub CLI connecté à un compte autorisé. Aucun jeton n'est stocké dans le plugin. Le contenu est vérifié contre son identifiant de blob GitHub, affiché dans l'interface. Cette empreinte identifie le contenu ; elle ne constitue pas une signature du programme ni une preuve de sécurité matérielle. Le téléchargement est explicite, jamais au démarrage de SimHub.

« Compiler sans flasher » compile la source GitHub chargée, ou la source embarquée hors ligne, sans ouvrir de port série. Le flash exige une source GitHub chargée et utilise exactement ces mêmes octets jusqu'au prochain téléchargement. Le choix carte/bootloader est obligatoire. Les Nano ATmega328P avec bootloader récent et ancien sont proposés séparément ; aucune détection commerciale WAVGAT n'est assimilée à une preuve de compatibilité.

Le bouton de flash est verrouillé par défaut : choix explicite, case de vérification puis confirmation indiquant le modèle et le port enregistré. Le téléversement recompilera la source, désactivera l'éclairage, réservera exclusivement le port après sa fermeture par le moteur, puis demandera à Arduino CLI de vérifier la mémoire écrite. Aucun effacement de bootloader et aucune option ignorant la signature matérielle. Un échec laisse l'éclairage désactivé. Le firmware précédent n'est pas sauvegardé automatiquement.

Ne pas fermer SimHub ni débrancher pendant un téléversement. Si l'outil dépasse son délai, le plugin continue d'attendre et garde la réservation : il ne tue pas un programme qui pourrait écrire la mémoire flash. Un message l'indique. Les journaux Arduino sont enregistrés dans le journal du plugin. La compilation seule conserve ses sorties sous `FirmwareTools\jobs`.

## Protocole de contrôle EVO v1

En dehors des trames Adalight, la requête `45 76 6F 01 00 A6` (hexadécimal) demande la réponse ASCII `DIYAMBIENT-EVO/1\n`. La commande `45 76 6F 02 01 A4` conserve l'image déjà reçue. Le dernier octet vaut commande XOR argument XOR `A7`. Les anciens firmwares ignorent ces commandes ; leur absence de watchdog reste explicitement signalée. L'identification ne constitue pas un accusé de réception de chaque image.

## Validation

Le banc `tests/EvoReceiverTests.cpp` inclut directement le sketch original modifié : démarrage noir, ordre RGB, échéance, 186 positions de troncature et reprise, conservation/réarmement, rebouclage de `millis()`, bruit continu, extinction au milieu d'une trame et contrôles invalides. Ce sont des tests hôte, pas des essais sur carte.

`tests/FirmwareIntegrationTests.cs` vérifie les sources embarquées, la cession du verrou par le moteur sans port configuré, le rejet d'une seconde maintenance, l'arrêt pendant réservation, la récupération après erreur et le refus d'un modèle absent. L'argument optionnel `--compile` compile les deux variantes Nano depuis les ressources de la DLL ; il n'appelle jamais le téléversement.

**Aucune carte flashée pendant ce développement.** Le watchdog n'est donc pas actif sur la Nano de l'utilisateur tant qu'une installation du firmware n'a pas été explicitement décidée et effectuée.
