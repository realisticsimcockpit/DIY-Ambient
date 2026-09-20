# Preuves de l’audit 0.1.1

Les fichiers `.log` sont les sorties réelles des commandes exécutées.

- `source-checks.log` : 17 contrôles lexicaux/de contrat Python, **pas une compilation C#**.
- `reference-model-checks.log` : 7 modèles Python indépendants, dont 10 000 trames ; **pas une exécution du plugin**.
- `firmware-compile.log` : compilation g++ du banc hôte incluant exactement le sketch utilisateur, avec Serial/FastLED factices. Un avertissement sur le `memset` et le CRGB du double de test est conservé.
- `firmware-parser-checks.log` : 194 cas du parseur ; **aucune simulation des temps UART, interruptions ou puissance**.
- `environment.json` : outils présents/absents et empreinte du sketch identique au fichier envoyé.

Aucun journal de compilation C#, PowerShell, WPF, SimHub ou d’essais matériels n’est fourni, car ils n’ont pas été exécutés. `tests/CoreTests.cs` contient 53 cas qui restent à compiler et exécuter sur Windows.
