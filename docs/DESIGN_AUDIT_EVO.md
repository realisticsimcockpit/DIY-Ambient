# Audit d'interface — DIY Ambient light EVO

Le panneau actuellement implémenté reste la référence fonctionnelle. Cette proposition est une piste de refonte séparée : elle n'est pas appliquée au plugin.

## Audit du panneau actuel

- Toutes les fonctions sont présentes sur une seule page, mais elles ont presque le même poids visuel.
- L'interrupteur d'éclairage devrait être la commande principale et afficher clairement l'état réellement appliqué.
- Les sauvegardes sont difficiles à comprendre : certains réglages sont automatiques, tandis que la connexion et les écrans ont leur propre bouton.
- Le choix du mode gagnerait à utiliser trois boutons visibles plutôt qu'une liste.
- Les lignes d'écran manquent d'en-têtes pour distinguer moniteur, numéros de LED physiques et configuration des zones.
- Le chargement automatique d'un profil portant le nom du jeu actif mérite une explication directement dans la page.
- La palette doit indiquer visuellement la couleur sélectionnée, avec son nom, sans dépendre uniquement des infobulles.

## Proposition alternative

Une seule page principale, organisée en quatre blocs. Les réglages quotidiens restent ouverts. L'installation et les écrans restent dans le même panneau, mais dans des sections repliables.

```text
DIY Ambient light EVO                              [● ÉCLAIRAGE ON]
by REALISTIC SIMCOCKPIT
youtube.com/@realisticsimcockpit
● COM32 connecté · 3 × 60 LED · Jeu : iRacing · Profil : iRacing

AMBIANCE
[ Blanc ] [ Couleur ] [ Écrans SDR ]
Luminosité  [────────●────] 72 %
Contrôles du mode sélectionné uniquement

TÉLÉMÉTRIE
LED réservées [────●────] 20     10 à gauche + 10 à droite
[ Tester le drapeau jaune pendant 3 s ]

PROFIL
Jeu détecté : iRacing             Chargement automatique : actif
[ Profil : iRacing ▾ ] [ Charger ] [ Enregistrer les réglages ]

▸ INSTALLATION ET ALIMENTATION
  Port [ COM32 ▾ ]  Bandes [ 3 × 60 — 180 LED ▾ ]
  Alimentation : 5 V / 15 A (75 W) · estimation max : 11,0 A / 55 W
  À la fermeture : (●) Éteindre  ( ) Conserver la dernière couleur

▸ ÉCRANS ET ZONES
  Position | Écran          | LED physiques | Zones
  Gauche   | [DISPLAY2 ▾]   | [ 1] à [20]   | [Configurer]
  Centre   | [DISPLAY1 ▾]   | [21] à [40]   | [Configurer]
  Droite   | [DISPLAY3 ▾]   | [41] à [60]   | [Configurer]
```

## Principes proposés

- Respecter le thème de SimHub et ne jamais forcer du texte noir.
- Garder un accent bleu/cyan; réserver le vert à l'état actif, le jaune au drapeau et le rouge aux erreurs.
- Afficher en tête les quatre informations utiles : état, port, jeu et profil.
- Désactiver le test jaune quand aucune LED de télémétrie n'est réservée.
- Remplacer l'option de fermeture ambiguë par deux choix explicites : éteindre ou conserver la dernière couleur.
- Garder l'éditeur plein écran uniquement pour le placement graphique des zones.
